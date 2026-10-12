using System.Windows;
using System.Windows.Controls;
using LockIn.Core;

namespace LockIn.Desktop;

public partial class MainWindow
{
    private SessionHistoryItem[] _history = [];
    private bool _historyLoaded;

    private void HistoryFilterChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) ApplyHistoryFilters();
    }

    private void ClearHistoryFiltersClick(object sender, RoutedEventArgs e)
    {
        HistorySearchBox.Clear();
        HistoryScopeBox.SelectedIndex = 0;
        HistoryPeriodBox.SelectedIndex = 0;
    }

    private void ApplyHistoryFilters()
    {
        if (HistorySearchBox is null || HistoryPeriodBox is null) return;
        var query = HistorySearchBox.Text.Trim();
        var scope = SelectedTag(HistoryScopeBox);
        var period = SelectedTag(HistoryPeriodBox);
        var today = DateTimeOffset.Now.Date;
        var rows = _history.Where(s => s.ProfileName.Contains(query, StringComparison.OrdinalIgnoreCase)
            && (scope == "all" || s.Scope == scope)
            && (period == "all" || s.StartedAtUtc.LocalDateTime.Date >= today.AddDays(period == "today" ? 0 : -6))).ToArray();
        HistoryList.ItemsSource = rows.Select(s => new
        {
            s.ProfileName,
            Started = $"{s.StartedAtUtc.ToLocalTime():MMM d, yyyy} · {s.StartedAtUtc.ToLocalTime():t}",
            Elapsed = $"{TimeText(s.ElapsedSeconds)} elapsed",
            Outcome = s.Damaged ? $"Record could not be verified · {s.Scope}" : $"{s.State} · {s.Scope}",
            Details = $"Original deadline: {s.EndsAtUtc.ToLocalTime():g}" +
                (s.FinishedAtUtc is { } finished ? $"\nFinished: {finished.ToLocalTime():g}" : "\nNot finalized") +
                (string.IsNullOrWhiteSpace(s.CompletionReason) ? "" : $"\nReason: {s.CompletionReason}"),
            Cleanup = s.CleanupEvidence switch
            {
                "ownerReportedRemoval" => "Owner reported removal · browser cleanup was not verified",
                "browserAcknowledged" => "Browser acknowledged cleanup",
                _ => s.Scope == "Simulation" ? "Simulation · no restrictions were applied" : "Browser cleanup not recorded"
            }
        }).ToArray();
        var total = TimeSpan.FromSeconds(rows.Sum(s => Math.Max(0, s.ElapsedSeconds)));
        HistorySummaryText.Text = $"{rows.Length} session{(rows.Length == 1 ? "" : "s")} · {(int)total.TotalHours}h {total.Minutes}m elapsed in this view";
        HistoryFiltersExpander.Header = scope == "all" && period == "all" ? "Filter sessions" : "Filter sessions · filters applied";
        HistoryEmptyText.Text = !_historyLoaded ? "Refresh to load session history." : _history.Length == 0
            ? "Your sessions will appear here. Start a session from Focus when you're ready."
            : "No sessions match these filters. Try another profile name or clear the filters.";
        HistoryEmptyText.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
