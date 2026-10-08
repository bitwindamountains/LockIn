using System.Text.Json;
using LockIn.Core;
using LockIn.Infrastructure;
using Microsoft.Data.Sqlite;

namespace LockIn.Core.Tests;

public sealed class ReviewRegressionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "lockin-review-" + Guid.NewGuid().ToString("N"));
    private static Profile Sample() => new("study", 1, "Study", 300, CommitmentMode.Guided, [], [], new([], []), []);
    private static Request Request(string type, object payload) => new(1, Guid.NewGuid().ToString("N"), type, JsonSerializer.SerializeToElement(payload, JsonOptions.Default));
    private LocalStore Store() => new(Path.Combine(_folder, "test.db"));
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        public long? MonotonicMilliseconds { get; set; } = 0;
        public void Advance(int seconds) { UtcNow = UtcNow.AddSeconds(seconds); MonotonicMilliseconds += seconds * 1000; }
    }
    private sealed class FailingProvider(bool apply, bool release) : ISimulationProvider
    {
        public int Released { get; private set; }
        public Task ApplyAsync(Session session, CancellationToken token) => apply ? Task.FromException(new IOException("injected failure")) : Task.CompletedTask;
        public Task ReleaseAsync(Session session, CancellationToken token) { Released++; return release ? Task.FromException(new IOException("injected failure")) : Task.CompletedTask; }
    }

    [Fact]
    public void SnapshotCopiesNestedCollectionsAndRejectsMutation()
    {
        var terms = new List<YouTubeRule> { new("term", "visibleHashtags", "games") };
        var sites = new List<SiteRule> { new("site", "example.com", true) };
        var profile = Sample() with { Websites = sites, YouTube = new(terms, []) };
        var session = SessionLogic.Start(profile, DateTimeOffset.UtcNow, "s1");
        terms.Clear(); sites.Clear();
        Assert.Single(session.Policy.YouTube.Rules);
        Assert.Single(session.Policy.Websites);
        Assert.Throws<NotSupportedException>(() => ((IList<SiteRule>)session.Policy.Websites).Clear());
        Assert.Equal(session.PolicyHash, ProfileRules.Hash(session.Policy));
    }

    [Fact]
    public void NullCollectionsAndUnknownEnumsAreRejectedWithoutNullReferenceExceptions()
    {
        Assert.NotEmpty(ProfileRules.Validate(null));
        Assert.NotEmpty(ProfileRules.Validate(Sample() with { YouTube = null! }));
        Assert.NotEmpty(ProfileRules.Validate(Sample() with { Apps = null! }));
        Assert.NotEmpty(ProfileRules.Validate(Sample() with { Websites = [null!] }));
        Assert.NotEmpty(ProfileRules.Validate(Sample() with { CommitmentMode = (CommitmentMode)77 }));
    }

    [Theory]
    [InlineData("not a URL")]
    [InlineData("https://name:password@example.org/")]
    [InlineData("file:///C:/test")]
    public void InvalidDestinationReturnsAFieldError(string destination)
    {
        var profile = Sample() with { Websites = [new("site", "example.com", true)], YouTube = new([], [], DestinationKind: "customHttps", CustomDestination: destination) };
        Assert.Contains(ProfileRules.ValidateFields(profile), e => e.Field == "Destination");
    }

    [Fact]
    public void DestinationAndStudyResourceConflictsRespectDomainBoundaries()
    {
        var profile = Sample() with { Websites = [new("site", "example.com", true)], YouTube = new([], [], DestinationKind: "customHttps", CustomDestination: "https://study.example.com/") };
        Assert.NotEmpty(ProfileRules.Validate(profile));
        Assert.Empty(ProfileRules.Validate(profile with { Websites = [new("site", "example.com", false)] }));
        Assert.False(ProfileRules.IsBlockedHost("notexample.com", profile.Websites));
        Assert.NotEmpty(ProfileRules.Validate(Sample() with { Websites = [new("yt", "youtube.com", true)] }));
        Assert.Contains(ProfileRules.ValidateFields(profile with { StudyResources = ["https://study.example.com/"] }), e => e.Field == "StudyResources");
    }

    [Fact]
    public void PreviewUsesExactNormalizedTokensAndApprovalPrecedence()
    {
        var profile = Sample() with { YouTube = new([new("games", "visibleHashtags", "games")], ["AbCdEf12345"]) };
        Assert.Equal("Allow", RulePreview.Evaluate(profile, null, ["gaming"]).Outcome);
        Assert.Equal("Redirect", RulePreview.Evaluate(profile, null, ["#ＧＡＭＥＳ"]).Outcome);
        Assert.Equal("Allow", RulePreview.Evaluate(profile, "AbCdEf12345", ["games"]).Outcome);
        Assert.Equal("Coverage warning", RulePreview.Evaluate(profile, null, null).Outcome);
    }

    [Theory]
    [InlineData("https://youtu.be/AbCdEf12345", "AbCdEf12345")]
    [InlineData("https://www.youtube.com/watch?v=AbCdEf12345&list=x", "AbCdEf12345")]
    [InlineData("https://www.youtube.com/shorts/AbCdEf12345", "AbCdEf12345")]
    [InlineData("https://youtube.com.evil.example/watch?v=AbCdEf12345", null)]
    [InlineData("https://youtube.com/watch?v=AbCdEf12345&v=Other123456", null)]
    public void VideoApprovalParsingRejectsLookalikesAndAmbiguity(string input, string? expected) => Assert.Equal(expected, RulePreview.ParseVideoId(input));

    [Fact]
    public void MonotonicElapsedFinishesDespiteSmallRepeatedClockRewinds()
    {
        var now = DateTimeOffset.UtcNow;
        var session = SessionLogic.Start(Sample() with { DurationSeconds = 60 }, now, "s1");
        for (var i = 0; i < 6; i++) session = SessionLogic.Reconcile(session, now.AddSeconds(-1), TimeSpan.FromSeconds(10));
        Assert.Equal(SessionState.Releasing, session.State);
        Assert.Equal("deadline", session.CompletionReason);
        Assert.Equal(60, session.EffectiveElapsedSeconds);
    }

    [Fact]
    public void FinishCannotSkipRelease()
    {
        var session = SessionLogic.Start(Sample(), DateTimeOffset.UtcNow, "s1");
        Assert.Throws<InvalidOperationException>(() => SessionLogic.FinishRelease(session));
    }

    [Fact]
    public void GuidedEndAfterClockRewindStillRequiresTheDelay()
    {
        var now = DateTimeOffset.UtcNow;
        var session = SessionLogic.Start(Sample(), now, "s1");
        session = SessionLogic.Reconcile(session, now.AddSeconds(30), TimeSpan.FromSeconds(30));
        session = SessionLogic.RequestGuidedEnd(session, now.AddMinutes(-5));
        var early = SessionLogic.Reconcile(session, now.AddMinutes(-5), TimeSpan.FromSeconds(59));
        Assert.Equal(SessionState.Active, early.State);
        Assert.Equal("guidedEnd", SessionLogic.Reconcile(early, now.AddMinutes(-5), TimeSpan.FromSeconds(1)).CompletionReason);
    }

    [Fact]
    public async Task HostEnforcesCountdownAndCancellation()
    {
        var store = Store(); store.SaveProfile(Sample(), 0);
        var clock = new Clock(); var engine = new SimulationEngine(store, clock, new NoOpSimulationProvider());
        var preparation = Assert.IsType<Preparation>((await engine.HandleAsync(Request("PrepareSession", new PreparePayload("study", 1)))).Result);
        Assert.False((await engine.HandleAsync(Request("CommitSession", new CommitPayload(preparation.CandidateId)))).Ok);
        clock.UtcNow = clock.UtcNow.AddSeconds(30); // Wall-clock movement cannot skip monotonic countdown.
        Assert.False((await engine.HandleAsync(Request("CommitSession", new CommitPayload(preparation.CandidateId)))).Ok);
        Assert.True((await engine.HandleAsync(Request("CancelPreparation", new CommitPayload(preparation.CandidateId)))).Ok);
        clock.Advance(10);
        Assert.False((await engine.HandleAsync(Request("CommitSession", new CommitPayload(preparation.CandidateId)))).Ok);
        Assert.Empty(store.ListSessions());
    }

    [Fact]
    public async Task DuplicateCommitAfterCompletionReturnsHistoryInsteadOfReactivating()
    {
        var store = Store(); store.SaveProfile(Sample() with { DurationSeconds = 60 }, 0);
        var clock = new Clock(); var engine = new SimulationEngine(store, clock, new NoOpSimulationProvider());
        var preparation = Assert.IsType<Preparation>((await engine.HandleAsync(Request("PrepareSession", new PreparePayload("study")))).Result);
        clock.Advance(10);
        await engine.HandleAsync(Request("CommitSession", new CommitPayload(preparation.CandidateId)));
        clock.Advance(61);
        await engine.ReconcileAsync();
        var reopened = new SimulationEngine(Store(), clock, new NoOpSimulationProvider());
        var response = await reopened.HandleAsync(Request("CommitSession", new CommitPayload(preparation.CandidateId)));
        Assert.True(response.Ok);
        Assert.Equal(SessionState.Completed, Assert.IsType<Session>(response.Result).State);
        Assert.Null(store.GetCurrentSession());
        Assert.Single(store.ListSessions());
    }

    [Fact]
    public async Task MalformedRequestAndStaleProfileRevisionReturnErrors()
    {
        var store = Store(); store.SaveProfile(Sample(), 0);
        var engine = new SimulationEngine(store, new Clock(), new NoOpSimulationProvider());
        Assert.False((await engine.HandleAsync(new(1, null!, null!, default))).Ok);
        Assert.False((await engine.HandleAsync(Request("SaveProfileDraft", new { profile = (object?)null, expectedRevision = 0 }))).Ok);
        Assert.False((await engine.HandleAsync(Request("PrepareSession", new PreparePayload("study", 9)))).Ok);
    }

    [Fact]
    public async Task ActivationFailureIsCompensatedAndCleanupFailureRemainsVisible()
    {
        var store = Store(); store.SaveProfile(Sample(), 0);
        var clock = new Clock(); var provider = new FailingProvider(true, true);
        var engine = new SimulationEngine(store, clock, provider);
        var preparation = Assert.IsType<Preparation>((await engine.HandleAsync(Request("PrepareSession", new PreparePayload("study")))).Result);
        clock.Advance(10);
        Assert.False((await engine.HandleAsync(Request("CommitSession", new CommitPayload(preparation.CandidateId)))).Ok);
        await engine.ReconcileAsync();
        Assert.Equal(1, provider.Released);
        var response = await engine.HandleAsync(Request("GetSessionStatus", new { }));
        Assert.True(response.Ok);
        Assert.Equal(SessionState.CleanupPending, Assert.IsType<Session>(response.Result).State);
        Assert.Equal(1, provider.Released); // Backoff prevents every status poll from retrying cleanup.
        await new SimulationEngine(Store(), clock, new NoOpSimulationProvider()).ReconcileAsync();
        Assert.Equal(SessionState.Recovered, store.ListSessions()[0].State);
    }

    [Fact]
    public async Task CommittedSessionHasAnExplicitSimulationRecoveryRoute()
    {
        var store = Store(); var clock = new Clock();
        store.SaveSession(SessionLogic.Start(Sample() with { CommitmentMode = CommitmentMode.Committed }, clock.UtcNow, "s1"));
        var engine = new SimulationEngine(store, clock, new NoOpSimulationProvider());
        Assert.False((await engine.HandleAsync(Request("RequestGuidedEnd", new { }))).Ok);
        Assert.True((await engine.HandleAsync(Request("RecoverSimulation", new { }))).Ok);
        await engine.ReconcileAsync();
        Assert.Equal(SessionState.Recovered, store.ListSessions()[0].State);
    }

    [Fact]
    public void DatabaseRejectsASecondLiveSession()
    {
        var store = Store();
        store.SaveSession(SessionLogic.Start(Sample(), DateTimeOffset.UtcNow, "s1"));
        Assert.Throws<SqliteException>(() => store.SaveSession(SessionLogic.Start(Sample(), DateTimeOffset.UtcNow, "s2")));
    }

    [Fact]
    public void CorruptDatabaseIsNotReplacedWithAnEmptyStore()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "test.db");
        var bytes = "This is deliberately not a SQLite database."u8.ToArray();
        File.WriteAllBytes(path, bytes);
        Assert.Throws<SqliteException>(() => Store());
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void VersionOneMigrationCreatesBackupAndPreservesProfiles()
    {
        var store = Store();
        store.SaveProfile(Sample(), 0);
        var path = Path.Combine(_folder, "test.db");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            db.Open(); using var command = db.CreateCommand();
            command.CommandText = "DROP INDEX idx_one_live_session; UPDATE schema_version SET version=1;";
            command.ExecuteNonQuery();
        }
        var migrated = Store();
        Assert.Equal("Study", migrated.GetProfile("study")!.Name);
        Assert.True(File.Exists(path + ".v1.backup"));
        using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path + ".v1.backup", Pooling = false }.ToString());
        backup.Open(); using var check = backup.CreateCommand();
        check.CommandText = "SELECT version FROM schema_version";
        Assert.Equal(1L, check.ExecuteScalar());
    }

    [Fact]
    public void FutureDatabaseVersionIsRejected()
    {
        Store();
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(_folder, "test.db"), Pooling = false }.ToString()))
        {
            db.Open(); using var command = db.CreateCommand();
            command.CommandText = "UPDATE schema_version SET version=999";
            command.ExecuteNonQuery();
        }
        Assert.Throws<InvalidDataException>(() => Store());
    }

    [Fact]
    public void TamperedSessionSnapshotIsRejectedBeforeItCanResume()
    {
        var store = Store();
        store.SaveSession(SessionLogic.Start(Sample(), DateTimeOffset.UtcNow, "s1"));
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(_folder, "test.db"), Pooling = false }.ToString()))
        {
            db.Open(); using var command = db.CreateCommand();
            command.CommandText = "UPDATE sessions SET json=json_set(json, '$.policy.name', 'Changed outside the host')";
            command.ExecuteNonQuery();
        }
        Assert.Throws<InvalidDataException>(() => store.GetCurrentSession());
    }

    [Fact]
    public async Task TruncatedFramesAreRejectedAndAdjacentFramesAreReadSeparately()
    {
        using var truncated = new MemoryStream([10, 0, 0, 0, (byte)'{']);
        await Assert.ThrowsAsync<EndOfStreamException>(() => PipeTransport.ReadAsync<Request>(truncated));
        using var stream = new MemoryStream();
        await PipeTransport.WriteAsync(stream, Request("GetSessionStatus", new { }));
        await PipeTransport.WriteAsync(stream, Request("ListProfiles", new { }));
        stream.Position = 0;
        Assert.Equal("GetSessionStatus", (await PipeTransport.ReadAsync<Request>(stream)).Type);
        Assert.Equal("ListProfiles", (await PipeTransport.ReadAsync<Request>(stream)).Type);
    }

    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
}
