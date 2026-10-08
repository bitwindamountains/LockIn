using System.Windows;
using System.Windows.Controls;
using LockIn.Core;

namespace LockIn.Desktop;

// Derived presentation only. Start still stages, validates and prepares through the host.
public partial class MainWindow
{
    private bool _browserStatusKnown;
    private bool? _presentedEnrollment;
    private enum ReadinessDestination { None, Connections, Help, Profile }
    private sealed record Readiness(string Title, string Detail, string? Action = null,
        ReadinessDestination Destination = ReadinessDestination.None);

    private Readiness DescribeReadiness()
    {
        if (!_connected)
            return new("Local host unavailable", "Open LockIn from your desktop shortcut, or check the connection in Help.", "Open help", ReadinessDestination.Help);
        if (BrowserAlphaBox.IsChecked != true)
            return new("Simulation selected", "Apps, websites and videos stay unrestricted. Select your paired browser for browser restrictions.");
        if (SelectedTag(ModeBox) != "Guided" || Lines(AppsBox.Text).Length > 0)
            return new("Adjust this profile for browser alpha", "Browser alpha needs Guided mode and no application rules.", "Edit profile", ReadinessDestination.Profile);
        if (!_browserStatusKnown)
            return new("Checking browser connection", "Waiting for a current report from the local host. No restrictions have started.");
        if (BrowserBox.SelectedItem is not BrowserConnection browser)
            return new("Connect your browser once", "Get a setup code, then paste it into the LockIn extension.", "Set up browser", ReadinessDestination.Connections);
        if (!browser.Connected)
            return new("Reopen your paired browser", $"Open {browser.Label} and its LockIn extension. Connection checks run automatically.", "Connection details", ReadinessDestination.Connections);
        return browser.Ready
            ? new("Browser connected", $"{browser.Label} is ready for the staged profile. Start checks your latest edits before review.")
            : new("Prepare browser access", "Start prepares your latest profile. If access is requested, approve it in the LockIn extension, then start again.", "Connection details", ReadinessDestination.Connections);
    }

    private void UpdateReadinessPresentation()
    {
        var readiness = DescribeReadiness();
        ReadinessPanel.Visibility = Show(_active is null && _countdown is null);
        ReadinessTitle.Text = readiness.Title;
        ReadinessDetail.Text = readiness.Detail;
        ReadinessAction.Content = readiness.Action;
        ReadinessAction.Tag = readiness.Destination;
        ReadinessAction.Visibility = Show(readiness.Action is not null);
        ReadinessAction.IsEnabled = !_busy;
        PairBrowserButton.IsEnabled = _connected && !_busy && _active is null && _countdown is null;

        // Change expansion only when enrollment changes, so periodic refresh never fights the user.
        var enrolled = _browserConnections.Count > 0;
        if (_browserStatusKnown && _presentedEnrollment != enrolled)
        {
            _presentedEnrollment = enrolled;
            BrowserSetupExpander.IsExpanded = !enrolled;
            if (enrolled) PairingText.Text = "";
        }
        BrowserSetupExpander.Header = enrolled ? "Connect another browser" : "Connect a browser";
        ConnectedBrowserCard.Visibility = Show(enrolled);
        if (_active is null && _countdown is null && _connected)
            StatusText.Text = _completionText ?? "Review your settings, then begin.";
    }

    private void ReadinessClick(object sender, RoutedEventArgs e)
    {
        switch (ReadinessAction.Tag)
        {
            case ReadinessDestination.Connections: Tabs.SelectedIndex = 3; break;
            case ReadinessDestination.Help: Tabs.SelectedIndex = 4; break;
            case ReadinessDestination.Profile: EditClick(sender, e); break;
        }
    }
    private void BackToFocusClick(object sender, RoutedEventArgs e) => Tabs.SelectedIndex = 0;
    private void BrowserSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) UpdateFocusPresentation();
    }
}
