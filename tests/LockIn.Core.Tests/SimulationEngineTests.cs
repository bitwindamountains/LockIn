using System.Text.Json;
using LockIn.Core;
using LockIn.Infrastructure;

namespace LockIn.Core.Tests;

public class SimulationEngineTests
{
    private sealed class FakeClock(DateTimeOffset start) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = start;
    }

    private sealed class CountingProvider : ISimulationProvider
    {
        public int Applied { get; private set; }
        public int Released { get; private set; }
        public Task ApplyAsync(Session session, CancellationToken token) { Applied++; return Task.CompletedTask; }
        public Task ReleaseAsync(Session session, CancellationToken token) { Released++; return Task.CompletedTask; }
    }

    private static Request Request(string type, object payload) => new(1, Guid.NewGuid().ToString("N"), type, JsonSerializer.SerializeToElement(payload, JsonOptions.Default));

    [Fact]
    public async Task DuplicateCommitAppliesOnceAndReopenReleasesAtOriginalDeadline()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lockin-engine-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "state.db");
        try
        {
            var clock = new FakeClock(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
            var provider = new CountingProvider();
            var store = new LocalStore(path);
            store.SaveProfile(new Profile("study", 1, "Study", 60, CommitmentMode.Guided, [], [], new YouTubeSettings([], []), []), 0);
            var engine = new SimulationEngine(store, clock, provider);
            var prepared = await engine.HandleAsync(Request("PrepareSession", new PreparePayload("study")));
            var id = Assert.IsType<Preparation>(prepared.Result).CandidateId;
            clock.UtcNow = clock.UtcNow.AddSeconds(10);
            var first = await engine.HandleAsync(Request("CommitSession", new CommitPayload(id)));
            var duplicate = await engine.HandleAsync(Request("CommitSession", new CommitPayload(id)));
            Assert.True(first.Ok);
            Assert.True(duplicate.Ok);
            Assert.Equal(1, provider.Applied);
            var deadline = store.GetCurrentSession()!.EndsAtUtc;
            clock.UtcNow = deadline.AddSeconds(1);
            var reopened = new SimulationEngine(new LocalStore(path), clock, provider);
            await reopened.ReconcileAsync();
            Assert.Null(store.GetCurrentSession());
            Assert.Equal(1, provider.Released);
            Assert.Equal(SessionState.Completed, store.ListHistory()[0].State);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task InterruptedActivationReleasesWithoutApplyingAgain()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lockin-engine-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "state.db");
        try
        {
            var clock = new FakeClock(DateTimeOffset.UtcNow);
            var provider = new CountingProvider();
            var store = new LocalStore(path);
            var profile = new Profile("study", 1, "Study", 60, CommitmentMode.Guided, [], [], new YouTubeSettings([], []), []);
            store.SaveSession(SessionLogic.Start(profile, clock.UtcNow, "s1") with { State = SessionState.Activating });
            await new SimulationEngine(store, clock, provider).ReconcileAsync();
            Assert.Equal(0, provider.Applied);
            Assert.Equal(1, provider.Released);
            Assert.Equal(SessionState.Recovered, store.ListHistory()[0].State);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
