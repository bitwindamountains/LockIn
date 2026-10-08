namespace LockIn.Core;

public sealed record PreviewDecision(string Outcome, string Explanation, string? RuleId = null);

/// <summary>Deterministic editor preview for the currently supported exact visible-hashtag rules.</summary>
public static class RulePreview
{
    public static PreviewDecision Evaluate(Profile profile, string? videoId, IEnumerable<string>? visibleHashtags)
    {
        profile = ProfileRules.Freeze(profile);
        if (videoId is not null && profile.YouTube.ApprovedVideoIds.Contains(videoId, StringComparer.Ordinal))
            return new("Allow", "The exact video is approved; its approval overrides hashtag terms.");
        if (visibleHashtags is null)
            return profile.YouTube.MatchingMode == "allowMatchingTags"
                ? new("Redirect", $"Allowed hashtags could not be verified. {ProfileRules.DescribeRedirect(profile.YouTube)}")
                : new("Coverage warning", "Visible hashtags are unavailable. Distraction filtering allows the video with reduced coverage.");
        var tokens = visibleHashtags.Select(ProfileRules.NormalizeToken).ToHashSet(StringComparer.Ordinal);
        var matched = profile.YouTube.Rules.FirstOrDefault(r => tokens.Contains(ProfileRules.NormalizeToken(r.Value)));
        if (profile.YouTube.MatchingMode == "allowMatchingTags")
            return matched is not null
                ? new("Allow", $"The current video contains the allowed exact hashtag #{matched.Value}.", matched.Id)
                : new("Redirect", $"None of the allowed hashtags match this video. {ProfileRules.DescribeRedirect(profile.YouTube)}");
        return matched is null
            ? new("Allow", "No configured exact hashtag matches the supplied current-video hashtags.")
            : new("Redirect", $"Exact hashtag #{matched.Value} matches. {ProfileRules.DescribeRedirect(profile.YouTube)}", matched.Id);
    }

    public static string? ParseVideoId(string value)
    {
        value = value.Trim();
        if (ProfileRules.IsVideoId(value)) return value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0) return null;
        var host = uri.IdnHost.ToLowerInvariant();
        string? id = null;
        if (host == "youtu.be") id = uri.AbsolutePath.Trim('/');
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com")
        {
            if (uri.AbsolutePath == "/watch")
            {
                var values = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).Where(p => p.Length == 2 && p[0] == "v").ToArray();
                if (values.Length == 1) id = Uri.UnescapeDataString(values[0][1]);
            }
            else if (uri.AbsolutePath.StartsWith("/shorts/", StringComparison.Ordinal)) id = uri.AbsolutePath[8..].TrimEnd('/');
        }
        return ProfileRules.IsVideoId(id) ? id : null;
    }
}
