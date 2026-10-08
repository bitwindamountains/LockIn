namespace LockIn.Core;

public sealed record BrowserEnrollment(string InstallationId, string Label, string CredentialHash, DateTimeOffset CreatedAtUtc);
public sealed record BrowserAuth(string InstallationId, string Credential);
public sealed record BrowserIdPayload(string InstallationId);
public sealed record PairBrowserPayload(string Code, string InstallationId, string Label);
public sealed record PairBrowserResult(string InstallationId, string Credential);
public sealed record BrowserPollPayload(BrowserAuth Auth, string? ReadyProfileHash,
    string? AppliedSessionId = null, string? AppliedPolicyHash = null, string? ErrorCode = null);
public sealed record BrowserAckPayload(BrowserAuth Auth, string SessionId, string PolicyHash, bool Released = false);
public sealed record BrowserPolicy(string SessionId, string PolicyHash, DateTimeOffset EndsAtUtc, int Revision,
    IReadOnlyList<SiteRule> Websites, YouTubeSettings YouTube, IReadOnlyList<string> StudyResources, double RemainingSeconds);
public sealed record BrowserPollResult(BrowserPolicy? Policy, string? ReleaseSessionId, DateTimeOffset ServerUtc,
    string? StagedProfileHash, IReadOnlyList<string> RequiredOrigins, string? ReleasePolicyHash);
public sealed record BrowserConnection(string InstallationId, string Label, bool Connected, bool Ready,
    string Coverage = "idle", DateTimeOffset? LastSeenUtc = null, string? ErrorCode = null);
public sealed record ConfirmBrowserRemovalPayload(string SessionId, string Confirmation);

public static class BrowserRequirements
{
    public static IReadOnlyList<string> Origins(Profile profile)
    {
        var origins = profile.Websites.Select(s => "*://" + (s.IncludeSubdomains ? "*." : "") + ProfileRules.NormalizeHost(s.Host) + "/*").ToList();
        if (profile.YouTube.Rules.Count > 0 || profile.YouTube.ApprovedVideoIds.Count > 0 || profile.YouTube.MatchingMode == "allowMatchingTags")
            origins.Add("https://www.youtube.com/*");
        return origins.Distinct(StringComparer.Ordinal).ToArray();
    }
}
