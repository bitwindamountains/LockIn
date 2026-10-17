using System.Text.Json;
using LockIn.Core;
using LockIn.Infrastructure;

namespace LockIn.Core.Tests;

public sealed class BrowserAlphaTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "lockin-browser-" + Guid.NewGuid().ToString("N"));
    private sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow; }
    private readonly Clock clock = new();
    private readonly LocalStore store;
    private SimulationEngine engine;
    private static Profile Profile => new("allow", 1, "Allowed learning", 120, CommitmentMode.Guided, [], [],
        new([new("tag", "visibleHashtags", "education")], [], DestinationKind: "focusPage", MatchingMode: "allowMatchingTags"), []);
    public BrowserAlphaTests()
    {
        store = new LocalStore(Path.Combine(folder, "state.db"));
        store.SaveProfile(Profile, 0);
        engine = new(store, clock, new NoOpSimulationProvider());
    }
    private Task<Response> Send(string command, object payload) => engine.HandleAsync(new(1, Guid.NewGuid().ToString("N"), command, JsonSerializer.SerializeToElement(payload, JsonOptions.Default)));
    private async Task<BrowserAuth> Pair()
    {
        var begin = await Send("BeginBrowserPairing", new { });
        var code = JsonSerializer.SerializeToElement(begin.Result, JsonOptions.Default).GetProperty("code").GetString()!;
        var reply = await Send("PairBrowser", new PairBrowserPayload(code, Guid.NewGuid().ToString("N"), "Test browser"));
        var result = Assert.IsType<PairBrowserResult>(reply.Result);
        return new(result.InstallationId, result.Credential);
    }
    private async Task<Session> Activate(BrowserAuth auth)
    {
        Assert.True((await Send("PollBrowser", new BrowserPollPayload(auth, ProfileRules.Hash(Profile)))).Ok);
        var prepare = await Send("PrepareSession", new PreparePayload(Profile.Id, 1, auth.InstallationId));
        var candidate = Assert.IsType<Preparation>(prepare.Result);
        clock.UtcNow = clock.UtcNow.AddSeconds(10);
        return Assert.IsType<Session>((await Send("CommitSession", new CommitPayload(candidate.CandidateId))).Result);
    }
    [Fact]
    public async Task PairingRejectsWrongExpiredReusedAndOverLimitCodes()
    {
        async Task<string> Begin() => JsonSerializer.SerializeToElement((await Send("BeginBrowserPairing", new { })).Result, JsonOptions.Default).GetProperty("code").GetString()!;
        Task<Response> Try(string code) => Send("PairBrowser", new PairBrowserPayload(code, Guid.NewGuid().ToString("N"), "Test browser"));
        static string Wrong(string code) => code == "111111" ? "222222" : "111111";
        // A malformed code does not count; the fifth attempt may still succeed, and the code is single use.
        var code = await Begin();
        Assert.False((await Try("12345")).Ok);
        for (var i = 0; i < 4; i++) Assert.False((await Try(Wrong(code))).Ok);
        Assert.True((await Try(code)).Ok);
        Assert.False((await Try(code)).Ok);

        // After five wrong attempts the right code is refused.
        code = await Begin();
        for (var i = 0; i < 5; i++) Assert.False((await Try(Wrong(code))).Ok);
        Assert.False((await Try(code)).Ok);

        code = await Begin();
        clock.UtcNow = clock.UtcNow.AddSeconds(121);
        Assert.False((await Try(code)).Ok);

        while (store.ListBrowsers().Count < 10) Assert.True((await Try(await Begin())).Ok);
        Assert.False((await Send("BeginBrowserPairing", new { })).Ok);
    }

    [Fact]
    public async Task BrowserRequiresActivationAndReleaseAcknowledgments()
    {
        var auth = await Pair();
        var session = await Activate(auth);
        Assert.Equal(SessionState.Activating, session.State);
        Assert.False((await Send("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, new string('0', 64)))).Ok);
        Assert.True((await Send("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash))).Ok);
        Assert.Equal(SessionState.Active, store.GetCurrentSession()!.State);
        Assert.False((await Send("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash, true))).Ok);
        clock.UtcNow = session.EndsAtUtc.AddSeconds(1);
        var poll = Assert.IsType<BrowserPollResult>((await Send("PollBrowser", new BrowserPollPayload(auth, null))).Result);
        Assert.Null(poll.Policy);
        Assert.Equal(session.Id, poll.ReleaseSessionId);
        Assert.Equal(SessionState.CleanupPending, store.GetCurrentSession()!.State);
        Assert.True((await Send("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash, true))).Ok);
        await engine.ReconcileAsync();
        Assert.Null(store.GetCurrentSession());
        Assert.Equal(SessionState.Completed, store.GetSession(session.Id)!.State);
    }
    [Fact]
    public async Task RecoverOnActiveBrowserSessionUsesGuidedDelay()
    {
        var auth = await Pair();
        var session = await Activate(auth);
        await Send("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash));
        Assert.True((await Send("RecoverSession", new { })).Ok);
        var pending = store.GetCurrentSession()!;
        Assert.Equal(SessionState.Active, pending.State);
        Assert.NotNull(pending.EarlyEndRequestedAtUtc);
        Assert.NotNull(Assert.IsType<BrowserPollResult>((await Send("PollBrowser", new BrowserPollPayload(auth, null))).Result).Policy);
        clock.UtcNow = clock.UtcNow.AddSeconds(59);
        await engine.ReconcileAsync();
        Assert.Equal(SessionState.Active, store.GetCurrentSession()!.State);
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        await engine.ReconcileAsync();
        var releasing = store.GetCurrentSession()!;
        Assert.Equal((SessionState.CleanupPending, "guidedEnd"), (releasing.State, releasing.CompletionReason));
        // Once release has begun, recovery stays immediate.
        Assert.True((await Send("RecoverSession", new { })).Ok);
        Assert.Equal("explicitRecovery", store.GetCurrentSession()!.CompletionReason);
    }
    [Fact]
    public async Task DamagedOldRecordIsListedAndDoesNotBreakBrowserRelease()
    {
        var auth = await Pair();
        var session = await Activate(auth);
        await Send("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash));
        clock.UtcNow = session.EndsAtUtc.AddSeconds(1);
        await engine.ReconcileAsync();
        // A newer row for the same browser whose snapshot no longer validates.
        store.SaveSession(session with { Id = "damaged", PolicyHash = new string('0', 64), State = SessionState.Completed });
        using (var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(folder, "state.db")};Pooling=False"))
        {
            db.Open();
            using var insert = db.CreateCommand();
            insert.CommandText = "INSERT INTO sessions(id,state,json) VALUES('unreadable','Completed','{')";
            insert.ExecuteNonQuery();
        }
        var history = Assert.IsAssignableFrom<IReadOnlyList<SessionHistoryItem>>((await Send("ListSessions", new { })).Result);
        Assert.Equal([("unreadable", true), ("damaged", true), (session.Id, false)], history.Select(h => (h.Id, h.Damaged)));
        Assert.Equal(("Unknown profile", "Allowed learning"), (history[0].ProfileName, history[1].ProfileName));
        Assert.Equal([false, true, true], history.Select(h => h.TimesKnown));
        var poll = Assert.IsType<BrowserPollResult>((await Send("PollBrowser", new BrowserPollPayload(auth, null))).Result);
        Assert.Equal((session.Id, session.PolicyHash), (poll.ReleaseSessionId, poll.ReleasePolicyHash));
        Assert.Throws<InvalidDataException>(() => store.GetSession("damaged"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnacknowledgedActivationIsCompensated(bool restart)
    {
        var auth = await Pair();
        var session = await Activate(auth);
        if (restart) engine = new(store, clock, new NoOpSimulationProvider());
        else clock.UtcNow = clock.UtcNow.AddSeconds(16);
        await engine.ReconcileAsync();
        Assert.Equal(SessionState.CleanupPending, store.GetCurrentSession()!.State);
        Assert.False((await Send("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash))).Ok);
        Assert.True((await Send("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash, true))).Ok);
        await engine.ReconcileAsync();
        Assert.Equal(SessionState.Recovered, store.GetSession(session.Id)!.State);
    }
    [Fact]
    public async Task CredentialsAndReadinessAreRequiredAndRevocationPersists()
    {
        var auth = await Pair();
        Assert.DoesNotContain(auth.Credential, File.ReadAllText(Path.Combine(folder, "state.db")));
        Assert.False((await Send("PrepareSession", new PreparePayload(Profile.Id, 1, auth.InstallationId))).Ok);
        Assert.False((await Send("PollBrowser", new BrowserPollPayload(auth with { Credential = new string('0', 64) }, null))).Ok);
        await Send("PollBrowser", new BrowserPollPayload(auth, ProfileRules.Hash(Profile)));
        clock.UtcNow = clock.UtcNow.AddSeconds(21);
        Assert.False((await Send("PrepareSession", new PreparePayload(Profile.Id, 1, auth.InstallationId))).Ok);
        Assert.True((await Send("RevokeBrowser", new BrowserIdPayload(auth.InstallationId))).Ok);
        engine = new(store, clock, new NoOpSimulationProvider());
        Assert.False((await Send("PollBrowser", new BrowserPollPayload(auth, null))).Ok);
    }
    [Theory]
    [InlineData("#EDUCATION", "Allow")]
    [InlineData("educationExtra", "Redirect")]
    [InlineData("gaming", "Redirect")]
    [InlineData(null, "Redirect")]
    public void EnteredTagsAreAnExactAllowList(string? tag, string expected) =>
        Assert.Equal(expected, RulePreview.Evaluate(Profile, "abcdefghijk", tag is null ? null : [tag]).Outcome);

    [Fact]
    public void ApprovalOverridesUnavailableTagsAndLegacyHashShapeIsPreserved()
    {
        var profile = Profile with { YouTube = Profile.YouTube with { ApprovedVideoIds = ["abcdefghijk"] } };
        Assert.Equal("Allow", RulePreview.Evaluate(profile, "abcdefghijk", null).Outcome);
        var legacy = profile with { YouTube = Profile.YouTube with { MatchingMode = null } };
        Assert.DoesNotContain("matchingMode", JsonSerializer.Serialize(legacy, JsonOptions.Default));
        Assert.Equal("Redirect", RulePreview.Evaluate(legacy, "abcdefghijk", ["education"]).Outcome);
    }
    [Fact]
    public async Task OwnerRemovalRequiresExpiredPendingSessionAndRevokesPairing()
    {
        var auth = await Pair();
        var session = await Activate(auth);
        var confirmation = new ConfirmBrowserRemovalPayload(session.Id, "extensionRemoved");
        Assert.False((await Send("ConfirmBrowserRemoval", confirmation)).Ok);
        await Send("RecoverSession", new { });
        Assert.False((await Send("ConfirmBrowserRemoval", confirmation)).Ok);
        clock.UtcNow = session.EndsAtUtc.AddSeconds(1);
        Assert.False((await Send("ConfirmBrowserRemoval", confirmation with { SessionId = "wrong" })).Ok);
        Assert.False((await Send("ConfirmBrowserRemoval", confirmation with { Confirmation = "" })).Ok);
        Assert.True((await Send("ConfirmBrowserRemoval", confirmation)).Ok);
        Assert.Null(store.GetCurrentSession());
        var recovered = store.GetSession(session.Id)!;
        Assert.Equal(SessionState.Recovered, recovered.State);
        Assert.False(recovered.BrowserReleased);
        Assert.Equal("ownerReportedRemoval", recovered.CleanupEvidence);
        Assert.Equal("ownerReportedRemoval", Assert.Single(store.ListHistory()).CleanupEvidence);
        engine = new(store, clock, new NoOpSimulationProvider());
        Assert.False((await Send("PollBrowser", new BrowserPollPayload(auth, null))).Ok);
        Assert.Empty(store.ListBrowsers());
    }

    [Fact]
    public async Task FailedRevocationRollsBackOwnerRecovery()
    {
        var auth = await Pair();
        var session = await Activate(auth);
        clock.UtcNow = session.EndsAtUtc.AddSeconds(1);
        await engine.ReconcileAsync();
        using var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(folder, "state.db")};Pooling=False");
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "CREATE TRIGGER fail_revoke BEFORE DELETE ON browser_enrollments BEGIN SELECT RAISE(ABORT, 'injected failure'); END";
        command.ExecuteNonQuery();
        Assert.False((await Send("ConfirmBrowserRemoval", new ConfirmBrowserRemovalPayload(session.Id, "extensionRemoved"))).Ok);
        Assert.Equal(SessionState.CleanupPending, store.GetCurrentSession()!.State);
        Assert.Null(store.GetCurrentSession()!.CleanupEvidence);
        Assert.Single(store.ListBrowsers());
    }

    [Fact]
    public async Task CoverageRequiresFreshExactPolicyReportAndPreservesCleanupEvidence()
    {
        var auth = await Pair();
        var session = await Activate(auth);
        await Send("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash));
        async Task<string> Coverage() => Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<BrowserConnection>>((await Send("GetBrowserConnections", new { })).Result)).Coverage;
        Assert.Equal("awaitingPolicy", await Coverage());
        await Send("PollBrowser", new BrowserPollPayload(auth, null, session.Id, session.PolicyHash));
        Assert.Equal("reportedActive", await Coverage());
        await Send("PollBrowser", new BrowserPollPayload(auth, null, session.Id, session.PolicyHash, "permissionMissing"));
        Assert.Equal("degraded", await Coverage());
        clock.UtcNow = clock.UtcNow.AddSeconds(21);
        Assert.Equal("offline", await Coverage());
        await Send("PollBrowser", new BrowserPollPayload(auth, null, "different-session", session.PolicyHash));
        Assert.Equal("awaitingPolicy", await Coverage());
        clock.UtcNow = session.EndsAtUtc.AddSeconds(1);
        await Send("PollBrowser", new BrowserPollPayload(auth, null));
        Assert.Equal("releasing", await Coverage());
        await Send("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash, true));
        await engine.ReconcileAsync();
        Assert.Equal("browserAcknowledged", store.GetSession(session.Id)!.CleanupEvidence);
        Assert.Equal("idle", await Coverage());
    }

    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
}
