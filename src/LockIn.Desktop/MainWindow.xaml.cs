using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LockIn.Core;
using Microsoft.Win32;

namespace LockIn.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly CancellationTokenSource _lifetime = new();
    private Profile? _editing;
    private string _draftId = Guid.NewGuid().ToString("N");
    private string? _candidateId;
    private CancellationTokenSource? _countdown;
    private bool _refreshing, _busy, _selecting, _closing, _connected;
    private Session? _active;
    private string? _lastActiveId;
    private string? _completionText;
    private int _browserRefreshTick;
    private IReadOnlyList<BrowserConnection> _browserConnections = [];
    private readonly string _pipeName = Environment.GetEnvironmentVariable("LOCKIN_SIMULATION_PIPE") ?? PipeTransport.PipeName;

    public MainWindow()
    {
        InitializeComponent();
        InitializePresentation();
        NewProfile();
        _refreshTimer.Tick += async (_, _) => await RefreshStatusAsync();
        Loaded += async (_, _) =>
        {
            await RefreshProfilesAsync(true);
            await RefreshStatusAsync();
            _refreshTimer.Start();
        };
        Closing += OnClosing;
    }

    private static string[] Lines(string value) => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string SelectedTag(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
    private static void SelectTag(ComboBox box, string tag)
    {
        box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Tag?.ToString() == tag);
    }
    private static string TimeText(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss");

    private async Task<T?> CallAsync<T>(string type, object payload, CancellationToken? token = null)
    {
        var response = await ServiceClient.SendAsync(type, payload, token ?? _lifetime.Token, _pipeName);
        if (!response.Ok) throw new InvalidOperationException(response.Error ?? "Unknown host error.");
        return response.Result is JsonElement element && element.ValueKind != JsonValueKind.Null
            ? element.Deserialize<T>(JsonOptions.Default) : default;
    }

    private void UpdateActions()
    {
        EditorPanel.IsEnabled = !_busy;
        BrowserBox.IsEnabled = !_busy;
        StageBrowserButton.IsEnabled = _connected && !_busy;
        RevokeBrowserButton.IsEnabled = _connected && !_busy;
        StartButton.IsEnabled = _connected && !_busy && _active is null;
        SaveButton.IsEnabled = _connected && !_busy;
        EndButton.IsEnabled = _connected && !_busy && _active is { State: SessionState.Active, Policy.CommitmentMode: CommitmentMode.Guided, EarlyEndRequestedAtUtc: null };
        CancelEndButton.IsEnabled = _connected && !_busy && _active is { State: SessionState.Active, EarlyEndRequestedAtUtc: not null };
        RecoveryButton.IsEnabled = _connected && !_busy && _active is not null;
        RemovalRecoveryButton.IsEnabled = _connected && !_busy &&
            _active is { State: SessionState.CleanupPending, BrowserInstallationId: not null, BrowserReleased: false } &&
            DateTimeOffset.UtcNow >= _active.EndsAtUtc;
        var browser = _active?.BrowserInstallationId is not null || _active is null && BrowserAlphaBox.IsChecked == true;
        ModeBanner.Text = browser ? "BROWSER ALPHA · Selected browser only · Desktop app enforcement unavailable" : "SIMULATION · Apps, websites and videos remain unrestricted";
        var connection = _browserConnections.FirstOrDefault(b => b.InstallationId == _active?.BrowserInstallationId);
        CoverageText.Text = _active?.BrowserInstallationId is null ? "" : !_connected ? "Coverage unknown while the local host is unavailable." :
            connection is null ? "Browser enrollment is unavailable. Open Browser for recovery options." : CoverageLabel(connection);
        UpdateFocusPresentation();
    }

    private async Task RefreshProfilesAsync(bool initial = false)
    {
        try
        {
            var profiles = await CallAsync<List<Profile>>("ListProfiles", new { }) ?? [];
            _selecting = true;
            ProfilesBox.ItemsSource = profiles;
            ProfilesBox.SelectedItem = profiles.FirstOrDefault(p => p.Id == _editing?.Id);
            if (initial && profiles.Count > 0) { ProfilesBox.SelectedIndex = 0; LoadEditor(profiles[0]); }
            _connected = true;
        }
        catch (Exception error) { MessageText.Text = Explain(error); }
        finally { _selecting = false; UpdateSummary(); UpdateActions(); }
    }

    private async Task RefreshStatusAsync()
    {
        if (_refreshing || _closing) return;
        _refreshing = true;
        try
        {
            _active = await CallAsync<Session>("GetSessionStatus", new { });
            _connected = true;
            if (++_browserRefreshTick % 5 == 0) await RefreshBrowsersAsync();
            if (_active is null)
            {
                StatusText.Text = _completionText ?? (BrowserAlphaBox.IsChecked == true ? "Ready to prepare a browser session." : "Ready for a simulated session.");
                DeadlineText.Text = "";
                RemainingText.Text = ClockFace(_editing?.DurationSeconds ?? 1500);
                SetProgress(0);
                if (_lastActiveId is not null)
                {
                    var history = await CallAsync<List<SessionHistoryItem>>("ListSessions", new { }) ?? [];
                    var finished = history.FirstOrDefault(s => s.Id == _lastActiveId);
                    if (finished is not null) StatusText.Text = _completionText = finished.CleanupEvidence == "ownerReportedRemoval"
                        ? $"{finished.ProfileName}: recovered by your removal confirmation. Cleanup was not verified by the browser."
                        : $"{finished.ProfileName}: {finished.State}. No restrictions remain.";
                    ShowHistory(history);
                    _lastActiveId = null;
                }
            }
            else
            {
                _completionText = null;
                _lastActiveId = _active.Id;
                StatusText.Text = _active.State == SessionState.Active
                    ? $"{_active.Policy.Name} · {TimeText(_active.Policy.DurationSeconds - _active.EffectiveElapsedSeconds)} remaining"
                    : $"{_active.Policy.Name} · {_active.State}";
                RemainingText.Text = ClockFace(_active.Policy.DurationSeconds - _active.EffectiveElapsedSeconds);
                SetProgress(100 * _active.EffectiveElapsedSeconds / _active.Policy.DurationSeconds);
                DeadlineText.Text = $"{(_active.BrowserInstallationId is null ? "Simulated" : "Browser alpha")} · Ends at {_active.EndsAtUtc.ToLocalTime():g} · Revision {_active.ProfileRevision}";
                if (_active.EarlyEndRequestedElapsedSeconds is { } requested)
                    DeadlineText.Text += $"\nGuided end in {TimeText(60 - (_active.EffectiveElapsedSeconds - requested))}. You may cancel this request.";
                if (_active.State == SessionState.CleanupPending) DeadlineText.Text += "\nWaiting for cleanup confirmation. Reopen the paired browser if it is closed.";
            }
        }
        catch (Exception error)
        {
            _connected = false;
            StatusText.Text = "Local session host unavailable.";
            DeadlineText.Text = Explain(error);
        }
        finally { _refreshing = false; UpdateActions(); }
    }

    private static string Explain(Exception error) => error is TimeoutException or OperationCanceledException or System.IO.IOException
        ? "The local session host did not respond. Start it using the README command, then reconnect."
        : error.Message;

    private void ClearErrors()
    {
        foreach (var block in new[] { NameError, DurationError, AppsError, SitesError, ResourcesError, TermsError, ApprovedError, DestinationError }) block.Text = "";
    }

    private void LoadEditor(Profile profile)
    {
        _editing = profile;
        _draftId = profile.Id;
        NameBox.Text = profile.Name;
        DurationBox.Text = (profile.DurationSeconds / 60).ToString();
        SelectTag(ModeBox, profile.CommitmentMode.ToString());
        AppsBox.Text = string.Join(Environment.NewLine, profile.Apps.Select(a => a.Path));
        SitesBox.Text = string.Join(Environment.NewLine, profile.Websites.Select(s => (s.IncludeSubdomains ? "" : "=") + s.Host));
        TermsBox.Text = string.Join(Environment.NewLine, profile.YouTube.Rules.Select(r => r.Value));
        SelectTag(TagModeBox, profile.YouTube.MatchingMode ?? "filterDistractions");
        ResourcesBox.Text = string.Join(Environment.NewLine, profile.StudyResources);
        ApprovedBox.Text = string.Join(Environment.NewLine, profile.YouTube.ApprovedVideoIds);
        SelectTag(ActionBox, profile.YouTube.TabAction);
        SelectTag(DestinationBox, profile.YouTube.DestinationKind);
        DestinationUrlBox.Text = profile.YouTube.CustomDestination ?? "";
        ClearErrors();
        UpdateSummary();
    }

    private void NewProfile()
    {
        var profile = new Profile(Guid.NewGuid().ToString("N"), 1, "Coding Study", 1500, CommitmentMode.Guided, [], [],
            new YouTubeSettings([new("yt-1", "visibleHashtags", "education"), new("yt-2", "visibleHashtags", "programming")], [], MatchingMode: "allowMatchingTags"), []);
        LoadEditor(profile);
        _editing = null;
        _selecting = true; ProfilesBox.SelectedItem = null; _selecting = false;
        UpdateSummary();
    }

    private Profile Draft()
    {
        int.TryParse(DurationBox.Text, out var minutes);
        var seconds = minutes is >= 1 and <= 480 ? minutes * 60 : 0;
        var apps = Lines(AppsBox.Text).Select((path, i) => new AppRule(
            _editing?.Apps.FirstOrDefault(a => a.Path == path)?.Id ?? Guid.NewGuid().ToString("N"), System.IO.Path.GetFileNameWithoutExtension(path), path)).ToArray();
        var sites = Lines(SitesBox.Text).Select((value, i) =>
        {
            var host = value.TrimStart('=').ToLowerInvariant();
            return new SiteRule(_editing?.Websites.FirstOrDefault(s => s.Host == host)?.Id ?? Guid.NewGuid().ToString("N"), host, !value.StartsWith('='));
        }).ToArray();
        var terms = Lines(TermsBox.Text).Select((value, i) => new YouTubeRule(
            _editing?.YouTube.Rules.FirstOrDefault(r => r.Value == ProfileRules.NormalizeToken(value))?.Id ?? Guid.NewGuid().ToString("N"), "visibleHashtags", value)).ToArray();
        // Retain typed settings through a with-expression; saving never mutates the running snapshot.
        var youtube = (_editing?.YouTube ?? new YouTubeSettings([], [])) with
        {
            Rules = terms, ApprovedVideoIds = Lines(ApprovedBox.Text).Select(v => RulePreview.ParseVideoId(v) ?? v).ToArray(),
            TabAction = SelectedTag(ActionBox), DestinationKind = SelectedTag(DestinationBox), CustomDestination = string.IsNullOrWhiteSpace(DestinationUrlBox.Text) ? null : DestinationUrlBox.Text.Trim(),
            MatchingMode = SelectedTag(TagModeBox) == "allowMatchingTags" ? "allowMatchingTags" : (_editing?.YouTube.MatchingMode == "filterDistractions" ? "filterDistractions" : null)
        };
        return new Profile(_draftId, _editing?.Revision ?? 1, NameBox.Text.Trim(), seconds,
            SelectedTag(ModeBox) == "Committed" ? CommitmentMode.Committed : CommitmentMode.Guided, apps, sites, youtube, Lines(ResourcesBox.Text));
    }

    private Profile ValidatedDraft()
    {
        ClearErrors();
        var draft = Draft();
        var issues = ProfileRules.ValidateFields(draft).ToList();
        foreach (var app in draft.Apps)
            if (!System.IO.File.Exists(app.Path)) issues.Add(new("Apps", $"Application file does not exist: {app.Path}"));
        if (issues.Count > 0)
        {
            foreach (var group in issues.GroupBy(e => e.Field))
            {
                var block = group.Key switch
                {
                    "Name" => NameError, "Duration" => DurationError, "Apps" => AppsError, "Websites" => SitesError,
                    "StudyResources" => ResourcesError, "YouTube" => TermsError, "ApprovedVideos" => ApprovedError,
                    _ => DestinationError
                };
                block.Text += string.Join(Environment.NewLine, group.Select(e => e.Message).Distinct()) + Environment.NewLine;
            }
            Tabs.SelectedIndex = 1;
            FocusValidation(issues[0].Field);
            throw new ArgumentException($"Review your profile: {issues[0].Message}");
        }
        return ProfileRules.Freeze(draft);
    }

    private bool IsDirty()
    {
        if (_editing is null) return true;
        try { return ProfileRules.Hash(Draft()) != ProfileRules.Hash(_editing); } catch { return true; }
    }
    private bool MayDiscard() => !IsDirty() || MessageBox.Show("Discard the current unsaved profile edits?", "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void ProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selecting || ProfilesBox.SelectedItem is not Profile selected) return;
        if (_busy)
        {
            _selecting = true;
            ProfilesBox.SelectedItem = ProfilesBox.Items.Cast<Profile>().FirstOrDefault(p => p.Id == _editing?.Id);
            _selecting = false;
            return;
        }
        if (!MayDiscard())
        {
            _selecting = true;
            ProfilesBox.SelectedItem = ProfilesBox.Items.Cast<Profile>().FirstOrDefault(p => p.Id == _editing?.Id);
            _selecting = false;
            return;
        }
        LoadEditor(selected);
    }

    private void UpdateSummary()
    {
        if (_active is null) RemainingText.Text = ClockFace(_editing?.DurationSeconds ?? 1500);
        ProfileSummary.Text = _editing is null ? "Create and save a profile in the Profile tab."
            : $"{_editing.Name} · {_editing.DurationSeconds / 60} min · {_editing.CommitmentMode}\n{_editing.Apps.Count} apps · {_editing.Websites.Count} websites · {_editing.YouTube.Rules.Count} {(_editing.YouTube.MatchingMode == "allowMatchingTags" ? "allowed" : "blocked")} hashtags\n{ProfileRules.DescribeRedirect(_editing.YouTube)}";
    }
    private void NewClick(object sender, RoutedEventArgs e) { if (!_busy && MayDiscard()) { NewProfile(); Tabs.SelectedIndex = 1; } }
    private void EditClick(object sender, RoutedEventArgs e) { Tabs.SelectedIndex = 1; EditorSections.SelectedIndex = 0; }
    private void DuplicateClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        try
        {
            var draft = ValidatedDraft();
            LoadEditor(draft with { Id = Guid.NewGuid().ToString("N"), Revision = 1, Name = (draft.Name.Length > 73 ? draft.Name[..73] : draft.Name) + " copy" });
            _editing = null;
            _selecting = true; ProfilesBox.SelectedItem = null; _selecting = false;
            MessageText.Text = "Duplicate created as an unsaved profile.";
        }
        catch (Exception error) { MessageText.Text = Explain(error); }
    }

    private async Task<Profile> SaveDraftAsync(Profile draft, Profile? baseline)
    {
        // Save using the context captured by the operation, never a later editor selection.
        if (baseline is not null && ProfileRules.Hash(draft) == ProfileRules.Hash(baseline)) return baseline;
        var saved = await CallAsync<Profile>("SaveProfileDraft", new SaveProfilePayload(draft, baseline?.Revision ?? 0)) ?? throw new InvalidOperationException("The host did not return the saved profile.");
        LoadEditor(saved);
        await RefreshProfilesAsync();
        return saved;
    }
    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true; UpdateActions();
        try { await SaveDraftAsync(ValidatedDraft(), _editing); MessageText.Text = "Profile saved for the next session."; }
        catch (Exception error) { MessageText.Text = Explain(error); }
        finally { _busy = false; UpdateActions(); }
    }

    private void BrowseAppClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dialog = new OpenFileDialog { Filter = "Windows applications (*.exe)|*.exe", CheckFileExists = true, Multiselect = true };
        if (dialog.ShowDialog(this) == true) AppsBox.Text = string.Join(Environment.NewLine, Lines(AppsBox.Text).Concat(dialog.FileNames).Distinct(StringComparer.OrdinalIgnoreCase));
    }
    private void PreviewClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        try
        {
            var profile = ValidatedDraft();
            var id = string.IsNullOrWhiteSpace(PreviewVideoBox.Text) ? null : RulePreview.ParseVideoId(PreviewVideoBox.Text);
            if (!string.IsNullOrWhiteSpace(PreviewVideoBox.Text) && id is null) throw new ArgumentException("Enter an exact video ID or supported YouTube URL for the preview.");
            var decision = RulePreview.Evaluate(profile, id, MetadataUnavailableBox.IsChecked == true ? null : PreviewHashtagsBox.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            PreviewText.Text = $"{decision.Outcome}: {decision.Explanation}\nPreview only; no browser action occurred.";
        }
        catch (Exception error) { PreviewText.Text = Explain(error); }
    }

    private async void StartClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true; UpdateActions();
        try
        {
            var draft = ValidatedDraft();
            var baseline = _editing;
            var useBrowser = BrowserAlphaBox.IsChecked == true;
            if (useBrowser)
            {
                draft = await SaveDraftAsync(draft, baseline);
                baseline = draft;
                await CallAsync<object>("StageBrowserProfile", new PreparePayload(draft.Id, draft.Revision));
                await RefreshBrowsersAsync();
            }
            var selectedBrowser = BrowserBox.SelectedItem as BrowserConnection;
            if (useBrowser && selectedBrowser is not { Connected: true })
            {
                Tabs.SelectedIndex = 3;
                throw new InvalidOperationException("Connect your browser here first. Your profile is already prepared.");
            }
            if (useBrowser && selectedBrowser is not { Ready: true })
            {
                // Give the automatic browser heartbeat time to report the staged
                // profile; never begin a countdown before readiness is confirmed.
                for (var attempt = 0; attempt < 7 && selectedBrowser is not { Ready: true }; attempt++)
                {
                    MessageText.Text = "Checking browser access… If prompted, allow site access in the LockIn extension.";
                    await Task.Delay(1000, _lifetime.Token);
                    await RefreshBrowsersAsync();
                    selectedBrowser = BrowserBox.SelectedItem as BrowserConnection;
                }
                if (selectedBrowser is not { Connected: true, Ready: true })
                    throw new InvalidOperationException("Open the LockIn extension and allow the requested sites, then start again. Your profile is already prepared.");
            }
            if (!await ReviewSessionAsync(draft, useBrowser, selectedBrowser)) return;
            var saved = await SaveDraftAsync(draft, baseline);
            var prepared = await CallAsync<Preparation>("PrepareSession", new PreparePayload(saved.Id, saved.Revision, useBrowser ? selectedBrowser!.InstallationId : null)) ?? throw new InvalidOperationException("Missing preparation.");
            _candidateId = prepared.CandidateId;
            _countdown = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            CancelCountdownButton.Visibility = Visibility.Visible;
            Tabs.SelectedIndex = 0;
            for (var seconds = 10; seconds > 0; seconds--)
            {
                _countdownRemaining = seconds;
                UpdateFocusPresentation();
                MessageText.Text = $"{(useBrowser ? "Browser session" : "Simulation")} starts in {seconds}s. Cancel to keep the session idle.";
                await Task.Delay(1000, _countdown.Token);
            }
            _countdown.Token.ThrowIfCancellationRequested();
            await CallAsync<Session>("CommitSession", new CommitPayload(_candidateId), _countdown.Token);
            _candidateId = null;
            MessageText.Text = useBrowser ? "Waiting for the browser to confirm its rules. Session status will show Active only after acknowledgment." : "Simulated session active. You can close and reopen this window.";
        }
        catch (OperationCanceledException) { MessageText.Text = _countdown?.IsCancellationRequested == true
            ? "Countdown cancelled. Checking the current session."
            : "Request timed out. Checking the current session."; }
        catch (Exception error) { MessageText.Text = Explain(error); }
        finally
        {
            await CancelCandidateAsync();
            _countdown?.Dispose(); _countdown = null;
            CancelCountdownButton.Visibility = Visibility.Collapsed;
            _busy = false;
            await RefreshStatusAsync();
            if (_active is null && Tabs.SelectedIndex == 0) StartButton.Focus();
        }
    }

    private async Task CancelCandidateAsync()
    {
        var id = _candidateId;
        _candidateId = null;
        if (id is null) return;
        try { await CallAsync<object>("CancelPreparation", new CommitPayload(id), CancellationToken.None); }
        catch { /* Candidate expires in the host; a committed session remains visible in status. */ }
    }
    private void CancelCountdownClick(object sender, RoutedEventArgs e) => _countdown?.Cancel();

    private async Task SessionCommandAsync(string command)
    {
        if (_busy) return;
        _busy = true; UpdateActions();
        try { await CallAsync<Session>(command, new { }); await RefreshStatusAsync(); }
        catch (Exception error) { MessageText.Text = Explain(error); }
        finally { _busy = false; UpdateActions(); }
    }
    private async void EndClick(object sender, RoutedEventArgs e) => await SessionCommandAsync("RequestGuidedEnd");
    private async void CancelEndClick(object sender, RoutedEventArgs e) => await SessionCommandAsync("CancelGuidedEnd");
    private async void RecoverClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Release this session through recovery? This will be recorded in history. Keep the paired browser open until cleanup is confirmed.", "Recover session", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            await SessionCommandAsync("RecoverSession");
    }
    private async void RefreshClick(object sender, RoutedEventArgs e) { if (_busy) return; await RefreshProfilesAsync(); await RefreshStatusAsync(); }
    private async void HistoryClick(object sender, RoutedEventArgs e)
    {
        if (!HistoryRefreshButton.IsEnabled) return;
        HistoryRefreshButton.IsEnabled = false;
        HistoryRefreshButton.Content = "Loading…";
        try { ShowHistory(await CallAsync<List<SessionHistoryItem>>("ListSessions", new { }) ?? []); }
        catch (Exception error) { MessageText.Text = Explain(error); }
        finally { HistoryRefreshButton.Content = "Refresh"; HistoryRefreshButton.IsEnabled = true; }
    }
    private void ShowHistory(IEnumerable<SessionHistoryItem> rows)
    {
        _history = rows.OrderByDescending(s => s.StartedAtUtc).ToArray();
        _historyLoaded = true;
        ApplyHistoryFilters();
    }

    private static string ClockFace(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.ToString(span.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss");
    }
    private void ScopeChanged(object sender, RoutedEventArgs e) { if (IsLoaded) UpdateActions(); }
    private async Task RefreshBrowsersAsync()
    {
        try
        {
            var id = (BrowserBox.SelectedItem as BrowserConnection)?.InstallationId;
            var browsers = await CallAsync<List<BrowserConnection>>("GetBrowserConnections", new { }) ?? [];
            _browserConnections = browsers;
            _browserStatusKnown = true;
            BrowserBox.ItemsSource = browsers;
            BrowserBox.SelectedItem = browsers.FirstOrDefault(b => b.InstallationId == id) ?? browsers.FirstOrDefault();
            BrowserStatusText.Text = browsers.Count == 0 ? "No browser paired yet." : string.Join(Environment.NewLine, browsers.Select(b =>
                $"{b.Label}: {(b.Coverage == "idle" ? b.Ready ? "Ready for the staged profile" : b.Connected ? "Connected — review site permissions" : "Not connected" : CoverageLabel(b))}"));
        }
        catch (Exception error) { _browserStatusKnown = false; BrowserStatusText.Text = Explain(error); }
        finally { UpdateFocusPresentation(); }
    }
    private async void PairBrowserClick(object sender, RoutedEventArgs e)
    {
        if (_busy || _active is not null) return;
        _busy = true; UpdateActions();
        try
        {
            var saved = await SaveDraftAsync(ValidatedDraft(), _editing);
            await CallAsync<object>("StageBrowserProfile", new PreparePayload(saved.Id, saved.Revision));
            var result = await CallAsync<JsonElement>("BeginBrowserPairing", new { });
            var code = result.GetProperty("code").GetString()!;
            PairingText.Text = $"{code} · Valid for two minutes";
            try { Clipboard.SetText(code); MessageText.Text = "Code copied. Open LockIn in Brave and paste once to connect. Your profile is already prepared."; }
            catch { MessageText.Text = "Open LockIn in Brave and enter the code below. Your profile is already prepared."; }
            BrowserAlphaBox.IsChecked = true;
        }
        catch (Exception error) { PairingText.Text = Explain(error); }
        finally { _busy = false; UpdateActions(); }
    }
    private async void StageBrowserClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true; UpdateActions();
        try
        {
            var saved = await SaveDraftAsync(ValidatedDraft(), _editing);
            await CallAsync<object>("StageBrowserProfile", new PreparePayload(saved.Id, saved.Revision));
            BrowserStatusText.Text = "Profile sent. Open the extension popup to review and grant site access.";
        }
        catch (Exception error) { MessageText.Text = Explain(error); }
        finally { _busy = false; UpdateActions(); }
    }
    private async void RefreshBrowsersClick(object sender, RoutedEventArgs e) => await RefreshBrowsersAsync();
    private async void RevokeBrowserClick(object sender, RoutedEventArgs e)
    {
        if (_busy || BrowserBox.SelectedItem is not BrowserConnection selected) return;
        _busy = true; UpdateActions();
        try { await CallAsync<object>("RevokeBrowser", new BrowserIdPayload(selected.InstallationId)); await RefreshBrowsersAsync(); }
        catch (Exception error) { BrowserStatusText.Text = Explain(error); }
        finally { _busy = false; UpdateActions(); }
    }

    private static string CoverageLabel(BrowserConnection connection) => connection.Coverage switch
    {
        "reportedActive" => "Browser reports this policy installed. Live page compatibility may vary.",
        "offline" => "Browser heartbeat lost. Coverage cannot be confirmed; reopen the paired browser.",
        "awaitingPolicy" => "Browser connected; waiting for the current policy installation report.",
        "releasing" => "Waiting for browser cleanup acknowledgment.",
        "degraded" => connection.ErrorCode switch
        {
            "permissionMissing" => "Site access was removed or denied. Browser coverage is incomplete.",
            "locallyReleased" => "Browser released locally. Use Release / recover to reconcile this session.",
            "clockDiscrepancy" => "Clock discrepancy triggered browser recovery.",
            "cleanupFailed" => "Browser cleanup failed. Reopen it and use Release / recover.",
            _ => "Browser policy installation is incomplete. Use Release / recover."
        },
        _ => connection.Connected ? "Browser connected." : "Browser not connected."
    };

    private async void ConfirmRemovalClick(object sender, RoutedEventArgs e)
    {
        if (_busy || _active?.BrowserInstallationId is null) return;
        if (RemovalConfirmedBox.IsChecked != true)
        {
            MessageText.Text = "Remove LockIn from the affected browser profile, then check the removal confirmation.";
            return;
        }
        var sessionId = _active.Id;
        if (MessageBox.Show(this,
            "Record that you removed LockIn from this session's browser profile? This revokes its pairing and clears the expired pending session. History will say Owner reported removal; browser cleanup has not been verified. Pair the extension again before another browser session.",
            "Record your removal confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _busy = true; UpdateActions();
        try
        {
            await CallAsync<Session>("ConfirmBrowserRemoval", new ConfirmBrowserRemovalPayload(sessionId, "extensionRemoved"));
            RemovalConfirmedBox.IsChecked = false;
            MessageText.Text = "Removal recorded as owner-reported recovery. Previous browser pairing revoked.";
            await RefreshBrowsersAsync();
            await RefreshStatusAsync();
        }
        catch (Exception error) { MessageText.Text = Explain(error); }
        finally { _busy = false; UpdateActions(); }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closing) return;
        e.Cancel = true;
        _closing = true;
        _reviewChoice?.TrySetResult(false);
        _refreshTimer.Stop();
        _countdown?.Cancel();
        await CancelCandidateAsync();
        _lifetime.Cancel();
        Close();
    }
}
