using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LockIn.Core;

public sealed record ValidationIssue(string Field, string Message);

public static class ProfileRules
{
    public static IReadOnlyList<string> Validate(Profile? profile) => ValidateFields(profile).Select(e => e.Message).ToArray();

    public static IReadOnlyList<ValidationIssue> ValidateFields(Profile? profile)
    {
        var issues = new List<ValidationIssue>();
        void Error(string field, string message) => issues.Add(new(field, message));
        if (profile is null) { Error("Profile", "A profile is required."); return issues; }
        if (!Bounded(profile.Id, 80)) Error("Id", "Profile ID is required (max 80 characters).");
        if (profile.Revision < 1) Error("Revision", "Profile revision must be positive.");
        if (!Bounded(profile.Name, 80)) Error("Name", "Enter a profile name of 1–80 characters.");
        if (profile.DurationSeconds is < 60 or > 28800) Error("Duration", "Duration must be 1–480 minutes.");
        if (!Enum.IsDefined(profile.CommitmentMode)) Error("Mode", "Unknown commitment mode.");
        if (profile.Apps is null || profile.Websites is null || profile.StudyResources is null || profile.YouTube?.Rules is null || profile.YouTube.ApprovedVideoIds is null)
        { Error("Profile", "Profile rule collections cannot be null."); return issues; }
        if (profile.Apps.Count > 100) Error("Apps", "Use at most 100 apps.");
        if (profile.Websites.Count > 500) Error("Websites", "Use at most 500 website rules.");
        if (profile.StudyResources.Count > 50) Error("StudyResources", "Use at most 50 study links.");
        if (profile.YouTube.Rules.Count > 100 || profile.YouTube.ApprovedVideoIds.Count > 500) Error("YouTube", "Use at most 100 terms and 500 approved videos.");
        if (profile.YouTube.MatchingMode is not (null or "filterDistractions" or "allowMatchingTags")) Error("YouTube", "Unknown YouTube matching mode.");
        if (profile.YouTube.MatchingMode == "allowMatchingTags" && profile.YouTube.Rules.Count + profile.YouTube.ApprovedVideoIds.Count == 0)
            Error("YouTube", "Enter at least one allowed hashtag or approve an exact video.");
        foreach (var app in profile.Apps)
            if (app is null || !Bounded(app.Id, 80) || !Bounded(app.DisplayName, 160) || !Bounded(app.Path, 1024) || !Path.IsPathFullyQualified(app.Path) || !app.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                Error("Apps", "Each app needs an ID, a display name, and an absolute .exe path.");
        foreach (var site in profile.Websites)
            if (site is null || !Bounded(site.Id, 80) || !IsHost(site.Host)) Error("Websites", "Enter a hostname only, such as example.com, without a path or port.");
        foreach (var rule in profile.YouTube.Rules)
            if (rule is null || !Bounded(rule.Id, 80) || rule.Source != "visibleHashtags" || !Bounded(rule.Value, 100) || NormalizeToken(rule.Value).Length == 0 || rule.Value.Any(char.IsWhiteSpace))
                Error("YouTube", "This build supports exact visible-hashtag terms, without spaces.");
        foreach (var id in profile.YouTube.ApprovedVideoIds)
            if (!IsVideoId(id)) Error("ApprovedVideos", "An approved video ID must contain exactly 11 letters, digits, underscores, or hyphens.");
        if (profile.Apps.Where(a => a is not null).Select(a => a.Id).Distinct().Count() != profile.Apps.Count ||
            profile.Websites.Where(s => s is not null).Select(s => s.Id).Distinct().Count() != profile.Websites.Count ||
            profile.YouTube.Rules.Where(r => r is not null).Select(r => r.Id).Distinct().Count() != profile.YouTube.Rules.Count)
            Error("Profile", "Rule IDs must be unique within each rule type.");
        foreach (var url in profile.StudyResources)
        {
            if (!IsHttps(url)) Error("StudyResources", "Study links must be HTTPS URLs without embedded credentials.");
            else if (IsBlockedHost(new Uri(url).IdnHost, profile.Websites)) Error("StudyResources", $"Study link is blocked: {url}");
        }
        if (profile.YouTube.TabAction is not ("replaceCurrent" or "switchExisting" or "openReplacement")) Error("Action", "Unknown YouTube tab action.");
        if (profile.YouTube.DestinationKind is not ("youtubeHome" or "focusPage" or "customHttps")) Error("Destination", "Unknown YouTube destination.");
        var destination = profile.YouTube.DestinationKind == "youtubeHome" ? "https://www.youtube.com/" : profile.YouTube.CustomDestination;
        if (profile.YouTube.DestinationKind == "customHttps" && !IsHttps(destination)) Error("Destination", "Custom destination must be an HTTPS URL without embedded credentials.");
        if (profile.YouTube.DestinationKind != "focusPage" && IsHttps(destination) && IsBlockedHost(new Uri(destination!).IdnHost, profile.Websites))
            Error("Destination", "The redirect destination is blocked. Choose Focus Page or remove the conflicting site rule.");
        return issues;
    }

    public static bool IsHttps(string? value) => value is { Length: <= 2048 } && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && !string.IsNullOrWhiteSpace(uri.Host);
    public static bool IsVideoId(string? value) => value is not null && Regex.IsMatch(value, "\\A[A-Za-z0-9_-]{11}\\z", RegexOptions.CultureInvariant);
    public static string NormalizeToken(string value) => value.Normalize(NormalizationForm.FormKC).Trim().TrimStart('#').ToLowerInvariant();
    public static string NormalizeHost(string host) => new IdnMapping().GetAscii(host.Trim().TrimEnd('.')).ToLowerInvariant();
    private static bool Bounded(string? value, int length) => !string.IsNullOrWhiteSpace(value) && value.Length <= length;
    private static bool IsHost(string? value) => Bounded(value, 253) && !value!.Any(c => char.IsWhiteSpace(c) || c is '/' or ':' or '*' or '@') && Uri.CheckHostName(value) == UriHostNameType.Dns;
    public static bool IsBlockedHost(string host, IReadOnlyList<SiteRule> sites) => sites.Any(s => s is not null && IsHost(s.Host) &&
        (NormalizeHost(host) == NormalizeHost(s.Host) || (s.IncludeSubdomains && NormalizeHost(host).EndsWith("." + NormalizeHost(s.Host), StringComparison.Ordinal))));

    // Copy each collection into read-only storage: IReadOnlyList alone does not freeze an array/list.
    public static Profile Freeze(Profile profile)
    {
        var errors = Validate(profile);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        return profile with
        {
            Name = profile.Name.Trim(),
            Apps = Array.AsReadOnly(profile.Apps.ToArray()),
            Websites = Array.AsReadOnly(profile.Websites.Select(s => s with { Host = NormalizeHost(s.Host) }).ToArray()),
            StudyResources = Array.AsReadOnly(profile.StudyResources.ToArray()),
            YouTube = profile.YouTube with
            {
                Rules = Array.AsReadOnly(profile.YouTube.Rules.Select(r => r with { Value = NormalizeToken(r.Value) }).ToArray()),
                ApprovedVideoIds = Array.AsReadOnly(profile.YouTube.ApprovedVideoIds.Distinct(StringComparer.Ordinal).ToArray())
            }
        };
    }

    public static string Hash(Profile profile) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Freeze(profile), JsonOptions.Default))).ToLowerInvariant();

    public static string DescribeRedirect(YouTubeSettings settings)
    {
        var destination = settings.DestinationKind switch { "focusPage" => "the Focus Page", "customHttps" => settings.CustomDestination ?? "a custom HTTPS page", _ => "YouTube Home" };
        return settings.TabAction switch
        {
            "switchExisting" => $"Switch to an existing tab for {destination}, or reuse this tab if none exists; neutralize the source video.",
            "openReplacement" => $"Open one replacement tab for {destination}; close the source only after the destination opens.",
            _ => $"Pause the video and go to {destination} in this tab."
        };
    }
}
