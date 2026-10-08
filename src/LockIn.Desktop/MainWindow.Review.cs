using System.Windows;
using System.Windows.Controls;
using LockIn.Core;

namespace LockIn.Desktop;

public partial class MainWindow
{
    // Render only the validated snapshot captured by Start, never live editor bindings.
    private void PresentReview(Profile draft, bool useBrowser, BrowserConnection? browser)
    {
        var allow = draft.YouTube.MatchingMode == "allowMatchingTags";
        var noRules = !allow && draft.Apps.Count + draft.Websites.Count + draft.YouTube.Rules.Count == 0;
        ReviewText.Text = $"{draft.Name} · {draft.DurationSeconds / 60} minute(s)";
        ReviewTimingText.Text = $"Estimated finish {DateTimeOffset.Now.AddSeconds(10 + draft.DurationSeconds):g} · includes 10-second countdown";
        ReviewScopeHeading.Text = useBrowser ? "BROWSER ALPHA · REAL RESTRICTIONS" : "SIMULATION ONLY";
        ReviewScopeText.Text = useBrowser
            ? $"Browser: {browser?.Label}. Selected sites and YouTube rules apply here. Already-open blocked sites will be replaced; save your work first. Desktop apps stay unrestricted."
            : "Apps, websites and videos stay unrestricted.";
        var tagPolicy = allow
            ? draft.YouTube.Rules.Count == 0
                ? "YouTube: only the approved videos are allowed; all other videos redirect."
                : "YouTube: any exact allowed hashtag or approved video permits playback. No match or unavailable metadata redirects."
            : "YouTube: matching hashtags redirect (legacy mode).";
        ReviewRulesText.Text = $"{draft.Apps.Count} apps · {draft.Websites.Count} websites · {draft.YouTube.Rules.Count} hashtags · {draft.YouTube.ApprovedVideoIds.Count} approved videos\n{tagPolicy}\n{ProfileRules.DescribeRedirect(draft.YouTube)}"
            + (noRules ? "\nThis profile has no restriction rules." : "");
        ReviewCommitmentText.Text = draft.CommitmentMode == CommitmentMode.Committed
            ? "Committed · ordinary early end is unavailable. Simulation recovery remains available."
            : "Guided · ending early requires a 60-second wait.";
        static string List(IEnumerable<string> values) => string.Join("\n", values.DefaultIfEmpty("None"));
        ReviewDetailsText.Text = $"APPS (SIMULATION ONLY)\n{List(draft.Apps.Select(a => a.Path))}\n\nWEBSITES\n{List(draft.Websites.Select(s => s.Host + (s.IncludeSubdomains ? " (including subdomains)" : " (exact host)")))}\n\n{(allow ? "ALLOWED" : "BLOCKED")} HASHTAGS\n{List(draft.YouTube.Rules.Select(r => "#" + r.Value))}\n\nAPPROVED VIDEO IDS\n{List(draft.YouTube.ApprovedVideoIds)}\n\nSTUDY LINKS\n{List(draft.StudyResources)}";
        ReviewRulesExpander.IsExpanded = false;
    }

    private void UpdateEditorPresentation()
    {
        DraftProfileLabel.Visibility = Show(ProfilesBox.SelectedItem is null);
        DraftProfileLabel.Margin = new Thickness(12, 0, 38, ActualWidth < 900 ? 0 : 8);
        ProfilesBox.IsEnabled = ProfilesBox.Items.Count > 0 && !_busy;
        EditorProfilesBox.IsEnabled = ProfilesBox.Items.Count > 0 && !_busy;
        CustomDestinationFields.Visibility = Show(SelectedTag(DestinationBox) == "customHttps");
    }

    private void DestinationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) UpdateEditorPresentation();
    }
}
