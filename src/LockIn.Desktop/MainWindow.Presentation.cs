using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
using LockIn.Core;

namespace LockIn.Desktop;

// Presentation only: animations never invoke session or release commands.
public partial class MainWindow
{
    private bool _reviewing;
    private TaskCompletionSource<bool>? _reviewChoice;
    private int _countdownRemaining;
    private string? _presentationKey, _resourceKey;
    private string? _feedback;
    private bool MotionEnabled => SystemParameters.ClientAreaAnimation && ReduceMotionBox.IsChecked != true;
    private static Brush Color(string value) => (Brush)new BrushConverter().ConvertFromString(value)!;
    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void InitializePresentation()
    {
        SizeChanged += (_, _) => UpdateFocusPresentation();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control && SaveButton.IsEnabled)
            {
                e.Handled = true;
                SaveClick(this, new RoutedEventArgs());
            }
        };
        SystemParameters.StaticPropertyChanged += SystemMotionChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= SystemMotionChanged;
        UpdateFocusPresentation();
    }

    private void UpdateFocusPresentation()
    {
        if (FocusView is null || ReduceMotionBox is null) return;
        var running = _active is not null;
        var counting = _countdown is not null;
        var compact = ActualWidth < 900;
        var shortWindow = ActualHeight < 700;
        FocusHeading.FontSize = 28;
        FocusHeading.Margin = new Thickness(0, 0, 0, compact ? 8 : 12);
        FocusSubtitle.Visibility = Show(!compact || running);
        DurationColumn.Width = new GridLength(compact ? 1 : 0, GridUnitType.Star);
        Grid.SetColumn(DurationField, compact ? 1 : 0);
        Grid.SetRow(DurationField, compact ? 0 : 1);
        ProfileField.Margin = new Thickness(0, 0, compact ? 16 : 0, 0);
        ProfilesBox.Margin = new Thickness(0, 8, 0, compact ? 4 : 16);
        DurationHint.Visibility = Show(!compact);
        SetupPanel.Padding = new Thickness(compact ? 16 : 24);
        SessionTicket.Padding = new Thickness(compact ? 16 : 24);
        ReviewPanel.Padding = new Thickness(compact ? 16 : 24);
        RemainingText.FontSize = compact ? 48 : 68;
        RemainingText.Margin = new Thickness(0, compact ? 4 : 14, 0, compact ? 4 : 12);
        TicketLabel.Visibility = Show(!compact || running || counting);
        DeadlineText.Visibility = Show(!compact || running || counting);
        StatusText.Visibility = Show(running || counting || _completionText is not null);
        CoverageText.Visibility = Show(running && _active?.BrowserInstallationId is not null);
        GuidedHint.Visibility = Show(!compact || running || counting);
        StartButton.Margin = new Thickness(0, compact ? 12 : 20, 0, 0);
        ReadinessPanel.Margin = new Thickness(0, compact ? 0 : 12, 0, 0);
        EditorHeading.FontSize = shortWindow ? 26 : 28;
        EditorHeading.Margin = new Thickness(0, 0, 0, shortWindow ? 6 : 12);
        EditorScopeText.Visibility = Show(!shortWindow);
        DraftStatusText.Text = _reviewing ? "Review in progress · Choose Back in Focus to edit"
            : _busy ? "Please wait · Profile operation in progress"
            : IsDirty() ? "Unsaved edits · Ctrl+S to save for the next session" : "Saved · active rules stay frozen · Ctrl+S to save";
        Tabs.Tag = ActualWidth < 760 ? "compact" : ActualHeight < 700 ? "short" : "wide";
        SetupPanel.Visibility = Show(!running && !counting);
        SetupPanel.IsEnabled = !_busy;
        SetupPanel.Margin = new Thickness(0, 0, compact ? 0 : 16, 0);
        SetupColumn.Width = new GridLength(running || counting || compact ? 0 : 1, GridUnitType.Star);
        Grid.SetColumnSpan(SetupPanel, compact ? 2 : 1);
        Grid.SetRow(SessionTicket, compact && !running && !counting ? 1 : 0);
        SessionTicket.Margin = new Thickness(0, compact && !running && !counting ? 12 : 0, 0, 0);
        SessionLayout.Visibility = Show(!_reviewing);
        ReviewPanel.Visibility = Show(_reviewing);
        BoundariesExpander.Visibility = Show(!_reviewing);
        ResourcesExpander.Visibility = Show(!_reviewing);
        StartButton.Visibility = Show(!running && !counting);
        StartButton.Content = _completionText is null ? "_Start focus →" : "Start another session →";
        EndButton.Visibility = Show(_active is { State: SessionState.Active, Policy.CommitmentMode: CommitmentMode.Guided, EarlyEndRequestedAtUtc: null });
        CancelEndButton.Visibility = Show(_active is { State: SessionState.Active, EarlyEndRequestedAtUtc: not null });
        CleanupButton.Visibility = Show(_active is { State: SessionState.CleanupPending });
        OpenResourcesButton.Visibility = Show(_active is { State: SessionState.Active });
        SessionProgress.Visibility = Show(running);
        SessionTicket.Background = Color(running ? "#203A2D" : "#E5EDCE");
        TicketText.SetValue(TextElement.ForegroundProperty, Color(running ? "#F0F1E8" : "#20392C"));
        FocusHeading.Text = _reviewing ? "Review session" : counting ? "Starting soon" : running ? "Focus in progress" : "Focus";
        FocusSubtitle.Text = _reviewing ? "Confirm what will apply to this session." : running ? "Rules are fixed. Edits apply to your next session." : "Choose your boundaries. Make time for one thing.";
        TicketLabel.Text = counting ? "NO RESTRICTIONS APPLIED YET" : running ? "TIME REMAINING" : "YOUR FOCUS WINDOW";
        GuidedHint.Text = _active is { State: SessionState.Active, EarlyEndRequestedAtUtc: not null } ? "Keep focusing cancels your early-end request." :
            _active is { State: SessionState.Active, Policy.CommitmentMode: CommitmentMode.Guided } ? "End early starts a 60-second wait." :
            _active is { Policy.CommitmentMode: CommitmentMode.Committed } ? "Committed session. Help & recovery remains available." : "You'll review the exact settings before the countdown.";
        if (counting)
        {
            RemainingText.Text = _countdownRemaining.ToString();
            StatusText.Text = "Cancel at any time before activation.";
        }
        else if (!running)
        {
            var duration = int.TryParse(DurationBox.Text, out var minutes) && minutes is >= 1 and <= 480 ? minutes * 60 : _editing?.DurationSeconds ?? 1500;
            RemainingText.Text = ClockFace(duration);
            DeadlineText.Text = $"Estimated finish {DateTimeOffset.Now.AddSeconds(duration + 10):t} · includes the 10-second countdown";
        }
        SaveButton.Content = running ? "_Save for next session" : "_Save profile";
        EditorScopeText.Text = running ? "Edits apply to your next session." : "Set your boundaries once. Reuse them whenever you study.";
        var profile = _active?.Policy ?? _editing;
        BoundaryHeading.Text = running ? $"Session boundaries · revision {_active!.ProfileRevision}" : "Profile rules · view details";
        if (profile is not null)
            ProfileSummary.Text = $"Apps: {profile.Apps.Count} selected (simulation only)\nWebsites: {profile.Websites.Count} selected hosts\nYouTube: {profile.YouTube.Rules.Count} exact { (profile.YouTube.MatchingMode == "allowMatchingTags" ? "allowed" : "blocked") } hashtags · {profile.YouTube.ApprovedVideoIds.Count} approved videos\n{ProfileRules.DescribeRedirect(profile.YouTube)}";
        RenderStudyResources(profile);
        UpdateReadinessPresentation();
        UpdateEditorPresentation();
        MotionStatusText.Text = !SystemParameters.ClientAreaAnimation ? "Windows has reduced motion enabled." : ReduceMotionBox.IsChecked == true ? "Motion reduced for this window." : "Short navigation transitions are enabled.";
        if (_feedback != MessageText.Text)
        {
            _feedback = MessageText.Text;
            if (IsLoaded && MotionEnabled && _feedback.StartsWith("Profile saved", StringComparison.Ordinal))
                MessageText.BeginAnimation(OpacityProperty, new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
        }
        var key = $"{Tabs.SelectedIndex}:{EditorSections.SelectedIndex}:{_reviewing}:{counting}:{_active?.State}:{_completionText is not null}";
        if (key != _presentationKey)
        {
            _presentationKey = key;
            if (IsLoaded) AnimateEntrance(Tabs.Template.FindName("PART_SelectedContentHost", Tabs) as FrameworkElement);
        }
    }

    private void RenderStudyResources(Profile? profile)
    {
        var links = profile?.StudyResources ?? [];
        var key = string.Join("\n", links);
        if (_resourceKey == key) return;
        _resourceKey = key;
        ResourceLinks.Children.Clear();
        if (links.Count == 0) ResourceLinks.Children.Add(new TextBlock { Text = "No study links yet. Add them in Profiles → Study links.", TextWrapping = TextWrapping.Wrap });
        foreach (var link in links)
        {
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0) continue;
            var button = new Button { Content = new TextBlock { Text = uri.Host + uri.PathAndQuery + " ↗", TextWrapping = TextWrapping.Wrap }, ToolTip = link, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
            button.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(link) { UseShellExecute = true }); } catch (Exception error) { MessageText.Text = Explain(error); } };
            ResourceLinks.Children.Add(button);
        }
    }

    private void AnimateEntrance(FrameworkElement? element)
    {
        if (element is null) return;
        element.BeginAnimation(OpacityProperty, null);
        element.Opacity = 1;
        if (!MotionEnabled) return;
        var duration = TimeSpan.FromMilliseconds(180);
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0.4, 1, duration) { FillBehavior = FillBehavior.Stop });
    }

    private void SetProgress(double value)
    {
        var previous = SessionProgress.Value;
        SessionProgress.BeginAnimation(ProgressBar.ValueProperty, null);
        SessionProgress.Value = value;
        if (MotionEnabled && Math.Abs(previous - value) < 10)
            SessionProgress.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(previous, value, TimeSpan.FromMilliseconds(250)) { FillBehavior = FillBehavior.Stop });
    }
    private void SystemMotionChanged(object? sender, PropertyChangedEventArgs e) => Dispatcher.InvokeAsync(() => MotionPreferenceChanged(this, new RoutedEventArgs()));
    private void MotionPreferenceChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var presenter = Tabs.Template.FindName("PART_SelectedContentHost", Tabs) as FrameworkElement;
        if (!MotionEnabled && presenter is not null) { presenter.BeginAnimation(OpacityProperty, null); presenter.Opacity = 1; presenter.RenderTransform = new TranslateTransform(); SessionProgress.BeginAnimation(ProgressBar.ValueProperty, null); MessageText.BeginAnimation(OpacityProperty, null); }
        UpdateFocusPresentation();
    }
    private void NavigationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.Source != Tabs) return;
        UpdateFocusPresentation();
        if (Tabs.SelectedIndex == 2) HistoryClick(sender, e);
    }
    private void EditorSectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded && e.Source == EditorSections) UpdateFocusPresentation(); }
    private void HelpClick(object sender, RoutedEventArgs e) => Tabs.SelectedIndex = 4;
    private void OpenResourcesClick(object sender, RoutedEventArgs e) { ResourcesExpander.IsExpanded = true; ResourcesExpander.BringIntoView(); ResourcesExpander.Focus(); }
    private async Task<bool> ReviewSessionAsync(Profile draft, bool useBrowser, BrowserConnection? browser)
    {
        _reviewChoice = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PresentReview(draft, useBrowser, browser);
        _reviewing = true; Tabs.SelectedIndex = 0; UpdateFocusPresentation(); ReviewBeginButton.Focus();
        try { return await _reviewChoice.Task; }
        finally { _reviewing = false; _reviewChoice = null; UpdateFocusPresentation(); }
    }
    private void BeginReviewClick(object sender, RoutedEventArgs e) => _reviewChoice?.TrySetResult(true);
    private void BackReviewClick(object sender, RoutedEventArgs e) => _reviewChoice?.TrySetResult(false);

    private void FocusValidation(string field)
    {
        if (field is "ApprovedVideos" or "Destination") VideoOptionsExpander.IsExpanded = true;
        var (section, control) = field switch
        {
            "Apps" => (1, (Control)AppsBox), "Websites" => (2, SitesBox), "StudyResources" => (4, ResourcesBox),
            "YouTube" => (3, TermsBox), "ApprovedVideos" => (3, ApprovedBox),
            "Destination" => (3, SelectedTag(DestinationBox) == "customHttps" ? (Control)DestinationUrlBox : DestinationBox),
            "Duration" => (0, DurationBox), _ => (0, NameBox)
        };
        EditorSections.SelectedIndex = section;
        // The selected tab/disclosure must finish layout before its field can take focus.
        Dispatcher.InvokeAsync(() =>
        {
            control.UpdateLayout();
            control.BringIntoView();
            Keyboard.Focus(control);
        }, DispatcherPriority.Input);
    }
}
