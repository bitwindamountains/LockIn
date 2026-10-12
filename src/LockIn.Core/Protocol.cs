using System.Text.Json;

namespace LockIn.Core;

public sealed record Request(int ProtocolVersion, string RequestId, string Type, JsonElement Payload);
public sealed record Response(int ProtocolVersion, string RequestId, bool Ok, object? Result, string? Error = null);
public sealed record PreparePayload(string ProfileId, int? ExpectedRevision = null, string? BrowserInstallationId = null);
public sealed record CommitPayload(string CandidateId);
public sealed record SaveProfilePayload(Profile Profile, int ExpectedRevision);
public sealed record Preparation(string CandidateId, Profile Profile, DateTimeOffset ReadyAtUtc, DateTimeOffset ExpiresAtUtc);
public sealed record SessionHistoryItem(string Id, string ProfileName, DateTimeOffset StartedAtUtc, DateTimeOffset EndsAtUtc,
    SessionState State, double ElapsedSeconds, DateTimeOffset? FinishedAtUtc, string? CompletionReason, string Scope = "Simulation", string? CleanupEvidence = null, bool Damaged = false);
