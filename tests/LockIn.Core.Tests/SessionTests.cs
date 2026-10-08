using System.Text.Json;
using LockIn.Core;
using LockIn.Infrastructure;

namespace LockIn.Core.Tests;

public class SessionTests
{
    private static Profile Sample(int seconds = 60, CommitmentMode mode = CommitmentMode.Guided) =>
        new("study", 1, "Coding Study", seconds, mode, [], [], new YouTubeSettings([], []), []);

    [Fact]
    public void DeadlineIsFixedAndProfileSnapshotIsImmutable()
    {
        var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        var session = SessionLogic.Start(Sample(), now, "s1");
        var changed = Sample() with { Name = "Edited later", Revision = 2 };
        Assert.Equal(now.AddMinutes(1), session.EndsAtUtc);
        Assert.Equal("Coding Study", session.Policy.Name);
        Assert.NotEqual(ProfileRules.Hash(changed), session.PolicyHash);
        Assert.Equal(SessionState.Releasing, SessionLogic.Reconcile(session, now.AddMinutes(2)).State);
    }

    [Fact]
    public void BackwardClockTriggersRecoveryInsteadOfExtension()
    {
        var now = DateTimeOffset.UtcNow;
        var session = SessionLogic.Start(Sample(), now, "s1");
        var checkedSession = SessionLogic.Reconcile(session, now.AddSeconds(20));
        var anomaly = SessionLogic.Reconcile(checkedSession, now.AddMinutes(-3));
        Assert.Equal("clockAnomaly", anomaly.CompletionReason);
        Assert.Equal(SessionState.Releasing, anomaly.State);
    }

    [Fact]
    public void GuidedEndRequiresFullDelayAndCanBeCancelled()
    {
        var now = DateTimeOffset.UtcNow;
        var session = SessionLogic.Start(Sample(300), now, "s1");
        var requested = SessionLogic.RequestGuidedEnd(session, now);
        Assert.Equal(SessionState.Active, SessionLogic.Reconcile(requested, now.AddSeconds(59)).State);
        Assert.Equal(SessionState.Releasing, SessionLogic.Reconcile(requested, now.AddSeconds(60)).State);
        Assert.Equal(SessionState.Active, SessionLogic.Reconcile(SessionLogic.CancelGuidedEnd(requested), now.AddSeconds(60)).State);
    }

    [Fact]
    public void CommittedOrdinaryEarlyEndIsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var session = SessionLogic.Start(Sample(300, CommitmentMode.Committed), now, "s1");
        Assert.Throws<InvalidOperationException>(() => SessionLogic.RequestGuidedEnd(session, now));
    }

    [Fact]
    public void ProfileRevisionConflictAndSessionSurviveStoreReopen()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lockin-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "state.db");
        try
        {
            var store = new LocalStore(path);
            var profile = store.SaveProfile(Sample(), 0);
            Assert.Throws<InvalidOperationException>(() => store.SaveProfile(profile, 0));
            var session = SessionLogic.Start(profile, DateTimeOffset.UtcNow, "s1");
            store.SaveSession(session);
            var reopened = new LocalStore(path);
            Assert.Equal(session.EndsAtUtc, reopened.GetCurrentSession()!.EndsAtUtc);
            Assert.Equal(session.PolicyHash, reopened.GetCurrentSession()!.PolicyHash);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task FramingRejectsOversizedPayloadBeforeAllocation()
    {
        using var stream = new MemoryStream();
        await stream.WriteAsync(BitConverter.GetBytes(PipeTransport.MaxFrameBytes + 1));
        stream.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => PipeTransport.ReadAsync<Request>(stream));
    }
}
