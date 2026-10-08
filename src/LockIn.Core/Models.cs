using System.Text.Json;

namespace LockIn.Core;

public enum SessionState { Activating, Active, Releasing, Completed, EndedEarly, Recovered, CleanupPending }
public enum CommitmentMode { Guided, Committed }

public sealed record AppRule(string Id, string DisplayName, string Path);
public sealed record SiteRule(string Id, string Host, bool IncludeSubdomains);
public sealed record YouTubeRule(string Id, string Source, string Value);
public sealed record YouTubeSettings(
    IReadOnlyList<YouTubeRule> Rules,
    IReadOnlyList<string> ApprovedVideoIds,
    string TabAction = "replaceCurrent",
    string DestinationKind = "youtubeHome",
    string? CustomDestination = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? MatchingMode = null);

public sealed record Profile(
    string Id,
    int Revision,
    string Name,
    int DurationSeconds,
    CommitmentMode CommitmentMode,
    IReadOnlyList<AppRule> Apps,
    IReadOnlyList<SiteRule> Websites,
    YouTubeSettings YouTube,
    IReadOnlyList<string> StudyResources);

public sealed record Session(
    string Id,
    string ProfileId,
    int ProfileRevision,
    string PolicyHash,
    Profile Policy,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset LastObservedUtc,
    SessionState State,
    DateTimeOffset? EarlyEndRequestedAtUtc = null,
    string? CompletionReason = null,
    double EffectiveElapsedSeconds = 0,
    double? EarlyEndRequestedElapsedSeconds = null,
    DateTimeOffset? FinishedAtUtc = null,
    string? BrowserInstallationId = null,
    bool BrowserReleased = false,
    string? CleanupEvidence = null);

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    long? MonotonicMilliseconds => null;
}
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public long? MonotonicMilliseconds => Environment.TickCount64;
}

public interface ISimulationProvider
{
    Task ApplyAsync(Session session, CancellationToken token);
    Task ReleaseAsync(Session session, CancellationToken token);
}

public sealed class NoOpSimulationProvider : ISimulationProvider
{
    public Task ApplyAsync(Session session, CancellationToken token) => Task.CompletedTask;
    public Task ReleaseAsync(Session session, CancellationToken token) => Task.CompletedTask;
}
