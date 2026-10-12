using System.Text.Json;
using LockIn.Core;
using LockIn.Infrastructure;

public sealed class SimulationEngine
{
    private readonly LocalStore _store;
    private readonly IClock _clock;
    private readonly ISimulationProvider _provider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _candidateId;
    private Profile? _candidateProfile;
    private DateTimeOffset _candidateExpires;
    private DateTimeOffset _candidateReady;
    private long? _candidateMonotonic;
    private string? _clockSessionId;
    private long? _lastMonotonic;
    private long _cleanupRetryAt;
    private int _cleanupFailures;
    private readonly BrowserRegistry _browsers;
    private string? _candidateBrowser;
    private string? _activatingBrowserSession;

    public SimulationEngine(LocalStore store, IClock clock, ISimulationProvider provider)
    { _store = store; _clock = clock; _provider = provider; _browsers = new BrowserRegistry(store, clock); }

    public async Task ReconcileAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var current = _store.GetCurrentSession();
            if (current is null) return;
            if (current.State == SessionState.Activating)
            {
                if (current.BrowserInstallationId is not null && _activatingBrowserSession == current.Id && _clock.UtcNow < current.StartedAtUtc.AddSeconds(15)) return;
                _store.SaveSession(current with { State = SessionState.Releasing, CompletionReason = _activatingBrowserSession == current.Id ? "browserActivationTimeout" : "interruptedActivation" });
                current = _store.GetCurrentSession();
            }
            var mono = _clock.MonotonicMilliseconds;
            TimeSpan? delta = _clockSessionId == current!.Id && mono is { } value && _lastMonotonic is { } previous && value >= previous
                ? TimeSpan.FromMilliseconds(value - previous) : null;
            _clockSessionId = current.Id;
            _lastMonotonic = mono;
            var next = SessionLogic.Reconcile(current, _clock.UtcNow, delta);
            if (next != current) _store.SaveSession(next);
            if (next.State is SessionState.Releasing or SessionState.CleanupPending)
            {
                if (next.BrowserInstallationId is not null && !next.BrowserReleased)
                {
                    if (next.State != SessionState.CleanupPending) _store.SaveSession(next with { State = SessionState.CleanupPending });
                    return;
                }
                if (Environment.TickCount64 < _cleanupRetryAt) return;
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _provider.ReleaseAsync(next, timeout.Token);
                    _store.SaveSession(SessionLogic.FinishRelease(next, _clock.UtcNow));
                    _cleanupFailures = 0;
                    _cleanupRetryAt = 0;
                }
                catch
                {
                    _store.SaveSession(next with { State = SessionState.CleanupPending });
                    _cleanupRetryAt = Environment.TickCount64 + Math.Min(30000, 1000 * (1 << Math.Min(++_cleanupFailures, 5)));
                }
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<Response> HandleAsync(Request request)
    {
        if (request is null || request.ProtocolVersion != 1 || string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 100 ||
            string.IsNullOrWhiteSpace(request.Type) || request.Type.Length > 80 || request.Payload.ValueKind != JsonValueKind.Object)
            return new Response(1, request?.RequestId ?? "", false, null, "Invalid protocol envelope.");
        try
        {
            await ReconcileAsync();
            await _gate.WaitAsync();
            try
            {
                object? result = request.Type switch
                {
                    "GetCapabilities" => new { simulation = true, apps = false, browserAlpha = true },
                    "GetSessionStatus" => _store.GetCurrentSession(),
                    "ListProfiles" => _store.ListProfiles(),
                    "ListSessions" => _store.ListHistory(),
                    "SaveProfileDraft" => Save(request.Payload),
                    "PrepareSession" => Prepare(request.Payload),
                    "CancelPreparation" => CancelPreparation(request.Payload),
                    "CommitSession" => await CommitAsync(request.Payload),
                    "RequestGuidedEnd" => RequestEnd(),
                    "CancelGuidedEnd" => CancelEnd(),
                    "RecoverSimulation" => Recover(),
                    "RecoverSession" => Recover(),
                    "BeginBrowserPairing" => BeginPairing(),
                    "StageBrowserProfile" => StageBrowser(request.Payload),
                    "GetBrowserConnections" => _browsers.Connections(_store.GetCurrentSession()),
                    "ConfirmBrowserRemoval" => ConfirmBrowserRemoval(request.Payload),
                    "RevokeBrowser" => RevokeBrowser(request.Payload),
                    "PairBrowser" => PairBrowser(request.Payload),
                    "PollBrowser" => PollBrowser(request.Payload),
                    "AcknowledgeBrowserPolicy" => AcknowledgeBrowser(request.Payload),
                    _ => throw new ArgumentException("Unknown command.")
                };
                return new Response(1, request.RequestId, true, result);
            }
            finally { _gate.Release(); }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException)
        { return new Response(1, request.RequestId, false, null, error.Message); }
        catch (Exception error) when (error is Microsoft.Data.Sqlite.SqliteException or InvalidDataException or IOException)
        { return new Response(1, request.RequestId, false, null, "Local storage is unavailable. Preserve the database and consult the recovery guide. No new session was accepted."); }
    }

    private Profile Save(JsonElement payload)
    {
        var input = payload.Deserialize<SaveProfilePayload>(JsonOptions.Default) ?? throw new ArgumentException("Missing profile.");
        if (input.ExpectedRevision < 0) throw new ArgumentException("Invalid profile revision.");
        return _store.SaveProfile(input.Profile, input.ExpectedRevision);
    }

    private object Prepare(JsonElement payload)
    {
        if (_store.GetCurrentSession() is not null) throw new InvalidOperationException("A session is already active.");
        var input = payload.Deserialize<PreparePayload>(JsonOptions.Default) ?? throw new ArgumentException("Missing profile ID.");
        if (string.IsNullOrWhiteSpace(input.ProfileId) || input.ProfileId.Length > 80) throw new ArgumentException("Invalid profile ID.");
        if (_candidateId is not null && _clock.UtcNow <= _candidateExpires) throw new InvalidOperationException("Another countdown is in progress. Cancel it or wait for it to expire.");
        var profile = _store.GetProfile(input.ProfileId) ?? throw new ArgumentException("Profile not found.");
        if (input.ExpectedRevision is { } revision && profile.Revision != revision) throw new InvalidOperationException("Profile changed. Reload it and review the latest revision.");
        if (input.BrowserInstallationId is not null)
        {
            if (profile.CommitmentMode != CommitmentMode.Guided) throw new InvalidOperationException("Browser alpha currently supports Guided sessions only.");
            if (profile.Apps.Count > 0) throw new InvalidOperationException("App enforcement is unavailable. Use a browser-only profile for browser alpha.");
            if (!_browsers.IsReady(input.BrowserInstallationId, profile)) throw new InvalidOperationException("Browser is not ready. Send this profile to the browser and grant its requested site permissions first.");
        }
        _candidateId = Guid.NewGuid().ToString("N");
        _candidateProfile = profile;
        _candidateBrowser = input.BrowserInstallationId;
        _candidateExpires = _clock.UtcNow.AddMinutes(2);
        _candidateReady = _clock.UtcNow.AddSeconds(10);
        _candidateMonotonic = _clock.MonotonicMilliseconds;
        return new Preparation(_candidateId, profile, _candidateReady, _candidateExpires);
    }

    private object CancelPreparation(JsonElement payload)
    {
        var input = payload.Deserialize<CommitPayload>(JsonOptions.Default) ?? throw new ArgumentException("Missing candidate ID.");
        if (input.CandidateId != _candidateId) throw new InvalidOperationException("This countdown is no longer current.");
        _candidateId = null; _candidateProfile = null; _candidateBrowser = null;
        return new { cancelled = true };
    }

    private async Task<Session> CommitAsync(JsonElement payload)
    {
        var input = payload.Deserialize<CommitPayload>(JsonOptions.Default) ?? throw new ArgumentException("Missing candidate ID.");
        if (string.IsNullOrWhiteSpace(input.CandidateId) || input.CandidateId.Length > 80) throw new ArgumentException("Invalid candidate ID.");
        var prior = _store.GetSession(input.CandidateId);
        if (prior is not null) return prior;
        var existing = _store.GetCurrentSession();
        if (existing is not null && existing.Id == input.CandidateId) return existing;
        if (existing is not null) throw new InvalidOperationException("A session is already active.");
        if (input.CandidateId != _candidateId || _candidateProfile is null || _clock.UtcNow > _candidateExpires)
            throw new InvalidOperationException("Preparation expired. Start again.");
        if (_clock.MonotonicMilliseconds is { } mono && _candidateMonotonic is { } preparedMono
            ? mono - preparedMono < 10000
            : _clock.UtcNow < _candidateReady)
            throw new InvalidOperationException("The 10-second countdown has not finished.");
        if (_candidateBrowser is not null && !_browsers.IsReady(_candidateBrowser, _candidateProfile)) throw new InvalidOperationException("Browser connection or permission readiness was lost during countdown.");
        var session = SessionLogic.Start(_candidateProfile, _clock.UtcNow, input.CandidateId) with { BrowserInstallationId = _candidateBrowser };
        _store.SaveSession(session with { State = SessionState.Activating });
        if (_candidateBrowser is not null)
        {
            _activatingBrowserSession = session.Id;
            _candidateId = null; _candidateProfile = null; _candidateBrowser = null;
            return session with { State = SessionState.Activating };
        }
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _provider.ApplyAsync(session, timeout.Token);
            _store.SaveSession(session);
            _clockSessionId = session.Id;
            _lastMonotonic = _clock.MonotonicMilliseconds;
            _candidateId = null; _candidateProfile = null;
            return session;
        }
        catch
        {
            _store.SaveSession(session with { State = SessionState.Releasing, CompletionReason = "activationFailure" });
            _candidateId = null; _candidateProfile = null;
            throw new InvalidOperationException("Simulation activation failed; cleanup is scheduled. Check session status before retrying.");
        }
    }

    private Session RequestEnd()
    {
        var current = _store.GetCurrentSession() ?? throw new InvalidOperationException("No active session.");
        var next = SessionLogic.RequestGuidedEnd(current, _clock.UtcNow);
        _store.SaveSession(next);
        return next;
    }

    private Session CancelEnd()
    {
        var current = _store.GetCurrentSession() ?? throw new InvalidOperationException("No active session.");
        var next = SessionLogic.CancelGuidedEnd(current);
        _store.SaveSession(next);
        return next;
    }

    private Session Recover()
    {
        var current = _store.GetCurrentSession() ?? throw new InvalidOperationException("No active session.");
        // A live browser session is a real restriction, so recovery takes the Guided delay (owner decision R20-3).
        if (current is { State: SessionState.Active, BrowserInstallationId: not null })
        {
            var guided = SessionLogic.RequestGuidedEnd(current, _clock.UtcNow);
            _store.SaveSession(guided);
            return guided;
        }
        var next = current with { State = SessionState.Releasing, CompletionReason = "explicitRecovery" };
        _store.SaveSession(next);
        _cleanupRetryAt = 0;
        return next;
    }

    private object BeginPairing()
    {
        if (_store.GetCurrentSession() is not null) throw new InvalidOperationException("Pair browsers between sessions.");
        return _browsers.BeginPairing();
    }
    private object PairBrowser(JsonElement payload)
    {
        if (_store.GetCurrentSession() is not null) throw new InvalidOperationException("Pair browsers between sessions.");
        return _browsers.Pair(payload.Deserialize<PairBrowserPayload>(JsonOptions.Default)!);
    }
    private object StageBrowser(JsonElement payload)
    {
        var input = payload.Deserialize<PreparePayload>(JsonOptions.Default) ?? throw new ArgumentException("Missing profile.");
        if (string.IsNullOrWhiteSpace(input.ProfileId)) throw new ArgumentException("Missing profile ID.");
        var profile = _store.GetProfile(input.ProfileId) ?? throw new ArgumentException("Profile not found.");
        _browsers.Stage(profile);
        return new { staged = true, requiredOrigins = BrowserRequirements.Origins(profile) };
    }
    private object RevokeBrowser(JsonElement payload)
    {
        var input = payload.Deserialize<BrowserIdPayload>(JsonOptions.Default) ?? throw new ArgumentException("Missing browser.");
        if (!Guid.TryParseExact(input.InstallationId, "N", out _)) throw new ArgumentException("Invalid browser ID.");
        if (_store.GetCurrentSession()?.BrowserInstallationId == input.InstallationId) throw new InvalidOperationException("Release this browser's session before revoking it.");
        _browsers.Revoke(input.InstallationId);
        return new { revoked = true };
    }
    private BrowserPollResult PollBrowser(JsonElement payload)
    {
        var input = payload.Deserialize<BrowserPollPayload>(JsonOptions.Default) ?? throw new ArgumentException("Missing browser heartbeat.");
        _browsers.Heartbeat(input);
        var session = _store.GetCurrentSession();
        if (session?.BrowserInstallationId != input.Auth.InstallationId) session = null;
        BrowserPolicy? policy = session is { State: SessionState.Active or SessionState.Activating }
            ? new(session.Id, session.PolicyHash, session.EndsAtUtc, session.ProfileRevision, session.Policy.Websites, session.Policy.YouTube, session.Policy.StudyResources, session.Policy.DurationSeconds - session.EffectiveElapsedSeconds)
            : null;
        var released = policy is null ? session ?? _store.GetLatestBrowserSession(input.Auth.InstallationId) : null;
        return new(policy, released?.Id, _clock.UtcNow,
            _browsers.StagedProfile is { } staged ? ProfileRules.Hash(staged) : null,
            _browsers.StagedProfile is { } stagedProfile ? BrowserRequirements.Origins(stagedProfile) : [], released?.PolicyHash);
    }
    private object AcknowledgeBrowser(JsonElement payload)
    {
        var input = payload.Deserialize<BrowserAckPayload>(JsonOptions.Default) ?? throw new ArgumentException("Missing browser acknowledgment.");
        _browsers.Authenticate(input.Auth);
        if (input.SessionId is not { Length: > 0 and <= 80 } || input.PolicyHash is not { Length: 64 }) throw new ArgumentException("Invalid browser acknowledgment.");
        var current = _store.GetSession(input.SessionId) ?? throw new InvalidOperationException("Unknown browser session.");
        if (current.BrowserInstallationId != input.Auth.InstallationId || current.PolicyHash != input.PolicyHash) throw new InvalidOperationException("Acknowledgment does not match the session policy.");
        if (input.Released)
        {
            if (current.State is SessionState.Active or SessionState.Activating) throw new InvalidOperationException("The session has not been released by the host.");
            if (!current.BrowserReleased) _store.SaveSession(current with { BrowserReleased = true, CleanupEvidence = "browserAcknowledged" });
        }
        else
        {
            if (current.State is not (SessionState.Activating or SessionState.Active) || _clock.UtcNow >= current.EndsAtUtc) throw new InvalidOperationException("Policy is no longer active.");
            if (current.State == SessionState.Activating) _store.SaveSession(current with { State = SessionState.Active });
        }
        return new { acknowledged = true };
    }

    private Session ConfirmBrowserRemoval(JsonElement payload)
    {
        var input = payload.Deserialize<ConfirmBrowserRemovalPayload>(JsonOptions.Default) ?? throw new ArgumentException("Missing recovery confirmation.");
        var current = _store.GetCurrentSession() ?? throw new InvalidOperationException("No session needs recovery.");
        if (input.SessionId != current.Id || input.Confirmation != "extensionRemoved" || current.BrowserInstallationId is null ||
            current.State != SessionState.CleanupPending || current.BrowserReleased)
            throw new InvalidOperationException("Owner recovery is only available for this browser's unconfirmed cleanup.");
        if (_clock.UtcNow < current.EndsAtUtc)
            throw new InvalidOperationException($"Wait until the original deadline ({current.EndsAtUtc.ToLocalTime():g}) before recording extension removal. Ordinary browser release remains available now.");
        // This is an owner attestation, not an invented browser acknowledgment.
        var recovered = current with { State = SessionState.Recovered, FinishedAtUtc = _clock.UtcNow,
            CompletionReason = "ownerReportedExtensionRemoval", CleanupEvidence = "ownerReportedRemoval" };
        _store.RecordOwnerReportedRemoval(recovered);
        _browsers.ForgetHeartbeat(current.BrowserInstallationId);
        return recovered;
    }
}
