namespace LockIn.Core;

public static class SessionLogic
{
    public static Session Start(Profile profile, DateTimeOffset now, string sessionId)
    {
        profile = ProfileRules.Freeze(profile);
        return new Session(sessionId, profile.Id, profile.Revision, ProfileRules.Hash(profile), profile,
            now, now.AddSeconds(profile.DurationSeconds), now, SessionState.Active);
    }

    public static Session Reconcile(Session session, DateTimeOffset now, TimeSpan? monotonicElapsed = null)
    {
        if (session.State is not (SessionState.Active or SessionState.Releasing or SessionState.CleanupPending)) return session;
        // Without a trustworthy in-process elapsed counter, a backward checkpoint is recovered.
        if (monotonicElapsed is null && now < session.LastObservedUtc.AddSeconds(-2))
            return session with { State = SessionState.Releasing, CompletionReason = "clockAnomaly" };
        var effectiveNow = now > session.LastObservedUtc ? now : session.LastObservedUtc;
        var elapsed = Math.Max(session.EffectiveElapsedSeconds + Math.Max(0, monotonicElapsed?.TotalSeconds ?? 0), (effectiveNow - session.StartedAtUtc).TotalSeconds);
        var next = session with { LastObservedUtc = effectiveNow, EffectiveElapsedSeconds = Math.Min(elapsed, session.Policy.DurationSeconds) };
        if (session.State == SessionState.Active && (effectiveNow >= session.EndsAtUtc || elapsed >= session.Policy.DurationSeconds))
            return next with { State = SessionState.Releasing, CompletionReason = "deadline" };
        if (session.State == SessionState.Active && session.EarlyEndRequestedAtUtc is { } requested &&
            (effectiveNow >= requested.AddSeconds(60) || (session.EarlyEndRequestedElapsedSeconds is { } requestedElapsed && elapsed >= requestedElapsed + 60)))
            return next with { State = SessionState.Releasing, CompletionReason = "guidedEnd" };
        return next;
    }

    public static Session RequestGuidedEnd(Session session, DateTimeOffset now)
    {
        if (session.State != SessionState.Active) throw new InvalidOperationException("No active session.");
        if (session.Policy.CommitmentMode == CommitmentMode.Committed) throw new InvalidOperationException("Committed sessions cannot end early through the ordinary control.");
        return session with
        {
            EarlyEndRequestedAtUtc = session.EarlyEndRequestedAtUtc ?? (now > session.LastObservedUtc ? now : session.LastObservedUtc),
            EarlyEndRequestedElapsedSeconds = session.EarlyEndRequestedElapsedSeconds ?? session.EffectiveElapsedSeconds
        };
    }

    public static Session CancelGuidedEnd(Session session)
    {
        if (session.State != SessionState.Active) throw new InvalidOperationException("No active session.");
        return session with { EarlyEndRequestedAtUtc = null, EarlyEndRequestedElapsedSeconds = null };
    }

    public static Session FinishRelease(Session session, DateTimeOffset? now = null)
    {
        if (session.State is not (SessionState.Releasing or SessionState.CleanupPending))
            throw new InvalidOperationException("Session is not releasing.");
        return session with
        {
            FinishedAtUtc = now ?? session.LastObservedUtc,
            State = session.CompletionReason switch
            {
                "guidedEnd" => SessionState.EndedEarly,
                "deadline" => SessionState.Completed,
                _ => SessionState.Recovered
            }
        };
    }
}
