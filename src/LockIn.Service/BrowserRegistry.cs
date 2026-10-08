using System.Security.Cryptography;
using System.Text;
using LockIn.Core;
using LockIn.Infrastructure;

public sealed class BrowserRegistry(LocalStore store, IClock clock)
{
    private string? _pairCode;
    private DateTimeOffset _pairExpires;
    private int _attempts;
    private sealed record HeartbeatState(DateTimeOffset Seen, long Tick, string? ReadyHash, string? SessionId, string? PolicyHash, string? ErrorCode);
    private readonly Dictionary<string, HeartbeatState> _heartbeats = [];
    public Profile? StagedProfile { get; private set; }

    public object BeginPairing()
    {
        if (store.ListBrowsers().Count >= 10) throw new InvalidOperationException("Revoke an old browser before adding another.");
        _pairCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        _pairExpires = clock.UtcNow.AddMinutes(2); _attempts = 0;
        return new { code = _pairCode, expiresAtUtc = _pairExpires };
    }

    public PairBrowserResult Pair(PairBrowserPayload input)
    {
        if (input is null || !Guid.TryParseExact(input.InstallationId, "N", out _) || string.IsNullOrWhiteSpace(input.Label) || input.Label.Length > 80 || input.Code is not { Length: 6 })
            throw new ArgumentException("Invalid browser pairing request.");
        if (_pairCode is null || clock.UtcNow > _pairExpires || ++_attempts > 5 || input.Code != _pairCode)
            throw new InvalidOperationException("Pairing code is invalid or expired. Generate a new code in the desktop.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        store.SaveBrowser(new(input.InstallationId, input.Label.Trim(), Hash(token), clock.UtcNow));
        _pairCode = null;
        return new(input.InstallationId, token);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public BrowserEnrollment Authenticate(BrowserAuth? auth)
    {
        if (auth is null || auth.Credential is not { Length: 64 } || !Guid.TryParseExact(auth.InstallationId, "N", out _)) throw new InvalidOperationException("Browser is not paired.");
        var enrollment = store.ListBrowsers().FirstOrDefault(b => b.InstallationId == auth.InstallationId);
        if (enrollment is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(enrollment.CredentialHash), Encoding.ASCII.GetBytes(Hash(auth.Credential))))
            throw new InvalidOperationException("Browser credential was rejected. Pair again.");
        return enrollment;
    }

    public void Stage(Profile profile) => StagedProfile = ProfileRules.Freeze(profile);
    public void Heartbeat(BrowserPollPayload input)
    {
        Authenticate(input.Auth);
        if (input.AppliedSessionId is { Length: > 80 } || input.AppliedPolicyHash is { Length: not 64 } ||
            input.ErrorCode is not (null or "permissionMissing" or "applyFailed" or "locallyReleased" or "clockDiscrepancy" or "cleanupFailed"))
            throw new ArgumentException("Invalid browser coverage report.");
        _heartbeats[input.Auth.InstallationId] = new(clock.UtcNow, Environment.TickCount64,
            input.ReadyProfileHash is { Length: 64 } ? input.ReadyProfileHash : null,
            input.AppliedSessionId, input.AppliedPolicyHash, input.ErrorCode);
    }
    private bool Fresh(HeartbeatState state) => state.Seen <= clock.UtcNow.AddSeconds(2) &&
        state.Seen >= clock.UtcNow.AddSeconds(-20) && Environment.TickCount64 - state.Tick <= 20000;
    public bool IsReady(string id, Profile profile) => _heartbeats.TryGetValue(id, out var state) &&
        Fresh(state) && state.ReadyHash == ProfileRules.Hash(profile) && store.ListBrowsers().Any(b => b.InstallationId == id);
    public IReadOnlyList<BrowserConnection> Connections(Session? session) => store.ListBrowsers().Select(b =>
    {
        _heartbeats.TryGetValue(b.InstallationId, out var seen);
        var connected = seen is not null && Fresh(seen);
        var ownsSession = session?.BrowserInstallationId == b.InstallationId;
        var coverage = !ownsSession ? "idle" : !connected ? "offline" : seen!.ErrorCode is not null ? "degraded" :
            session!.State is SessionState.Releasing or SessionState.CleanupPending ? "releasing" :
            seen!.SessionId == session!.Id && seen.PolicyHash == session.PolicyHash ? "reportedActive" : "awaitingPolicy";
        return new BrowserConnection(b.InstallationId, b.Label, connected, StagedProfile is not null && IsReady(b.InstallationId, StagedProfile), coverage, seen?.Seen, seen?.ErrorCode);
    }).ToArray();
    public void ForgetHeartbeat(string id) => _heartbeats.Remove(id);
    public void Revoke(string id) { store.RevokeBrowser(id); _heartbeats.Remove(id); }
}
