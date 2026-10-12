param([string]$DotnetRoot = "$env:LOCALAPPDATA\LockInDev\dotnet", [switch]$EditorOnly, [switch]$BrowserRecovery, [switch]$DesignOnly, [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'check-development-space.ps1')
$uiTestMutex = New-Object System.Threading.Mutex($false, 'Local\LockIn.DesktopWalkthrough')
try { $ownsUiTest = $uiTestMutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $ownsUiTest = $true }
if (-not $ownsUiTest) { $uiTestMutex.Dispose(); throw 'Another LockIn desktop walkthrough is running. Run UI checks sequentially.' }
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace LockInSmoke {
    public static class Native {
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    }
}
'@
$workspace = Split-Path -Parent $PSScriptRoot
$serviceExe = Join-Path $workspace "src\LockIn.Service\bin\$Configuration\net10.0\LockIn.Service.exe"
$desktopExe = Join-Path $workspace "src\LockIn.Desktop\bin\$Configuration\net10.0-windows\LockIn.Desktop.exe"
$testFolder = Join-Path ([IO.Path]::GetTempPath()) ('lockin-ui-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testFolder) | Out-Null
$pipeName = 'LockIn.Test.' + [guid]::NewGuid().ToString('N')
$oldRoot = $env:DOTNET_ROOT
$oldPipe = $env:LOCKIN_SIMULATION_PIPE
$env:DOTNET_ROOT = $DotnetRoot
$env:LOCKIN_SIMULATION_PIPE = $pipeName
$serviceProcess = $null
$desktopProcess = $null

function Wait-For($Action, [int]$Seconds = 15) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        $result = & $Action
        if ($result) { return $result }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($desktopProcess -and -not $desktopProcess.HasExited) {
        $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$desktopProcess.Id)
        $elements = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        foreach ($element in $elements) {
            if ($element.Current.Name -or $element.Current.AutomationId) {
                Write-Host ("UI: {0} | {1} | {2} | invoke={3}" -f $element.Current.ControlType.ProgrammaticName, $element.Current.Name, $element.Current.AutomationId, $element.GetCurrentPropertyValue([System.Windows.Automation.AutomationElement]::IsInvokePatternAvailableProperty))
            }
        }
    }
    throw 'Timed out waiting for the expected UI or host state.'
}
function Read-Exact($Stream, [int]$Count) {
    $buffer = New-Object byte[] $Count
    $offset = 0
    while ($offset -lt $Count) {
        $read = $Stream.ReadAsync($buffer, $offset, $Count - $offset)
        if (-not $read.Wait(8000)) { throw 'Timed out reading a pipe response.' }
        if ($read.Result -eq 0) { throw 'Truncated pipe response.' }
        $offset += $read.Result
    }
    return ,$buffer
}
function Send-Request([string]$Type, $Payload = @{}) {
    $pipe = New-Object IO.Pipes.NamedPipeClientStream('.', $pipeName, [IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(2000)
        $requestId = [guid]::NewGuid().ToString('N')
        $json = @{ protocolVersion = 1; requestId = $requestId; type = $Type; payload = $Payload } | ConvertTo-Json -Compress -Depth 15
        $bytes = [Text.Encoding]::UTF8.GetBytes($json)
        $header = [BitConverter]::GetBytes([int]$bytes.Length)
        $pipe.Write($header, 0, 4)
        $pipe.Write($bytes, 0, $bytes.Length)
        $pipe.Flush()
        $responseHeader = Read-Exact $pipe 4
        $length = [BitConverter]::ToInt32($responseHeader, 0)
        if ($length -lt 1 -or $length -gt 1048576) { throw 'Invalid response length.' }
        $body = Read-Exact $pipe $length
        $response = [Text.Encoding]::UTF8.GetString($body) | ConvertFrom-Json
        if (-not $response.ok -or $response.requestId -ne $requestId) { throw "Host rejected request: $($response.error)" }
        return $response.result
    } finally { $pipe.Dispose() }
}
function Window-Root {
    $desktopProcess.Refresh()
    if ($desktopProcess.HasExited) { throw 'Desktop process exited unexpectedly.' }
    if ($desktopProcess.MainWindowHandle -eq [IntPtr]::Zero) { return $null }
    return [System.Windows.Automation.AutomationElement]::FromHandle($desktopProcess.MainWindowHandle)
}
function Restore-TestWindow {
    # Window-Root is scoped to the process launched by this harness.
    $window = Window-Root
    $pattern = $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    if ($pattern.Current.WindowVisualState -ne [System.Windows.Automation.WindowVisualState]::Normal) {
        $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
    }
    return Wait-For {
        $current = Window-Root
        $state = $current.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Current.WindowVisualState
        if ($state -eq [System.Windows.Automation.WindowVisualState]::Normal -and $current.Current.BoundingRectangle.Height -ge 400) { $current }
    }
}
function Set-TestWindowSize([double]$Width, [double]$Height) {
    $window = Restore-TestWindow
    $transform = $window.GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
    if (-not $transform.Current.CanMove -or -not $transform.Current.CanResize) { throw 'Test window is not ready for geometry checks.' }
    $transform.Move(0, 0)
    $transform.Resize($Width, $Height)
}
function Find-Control([string]$Id) {
    $root = Window-Root
    if ($null -eq $root) { return $null }
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Invoke-Control([string]$Id) {
    $element = Wait-For { Find-Control $Id }
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}
function Select-Tab([string]$Id) {
    $element = Wait-For { Find-Control $Id }
    $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}
function Set-Value([string]$Id, [string]$Value) {
    $element = Wait-For { Find-Control $Id }
    $element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)
}
function Select-Option([string]$Id, [string]$Name) {
    $element = Wait-For { Find-Control $Id }
    $element.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $option = Wait-For { $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition) }
    $option.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $element.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
}
function Confirm-Preflight {
    $yes = Wait-For {
        $review = Find-Control 'ReviewBeginButton'
        if ($review -and -not $review.Current.IsOffscreen) { return $review }
        $name = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Yes')
        $process = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$desktopProcess.Id)
        $id = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, '6')
        $namedButton = New-Object System.Windows.Automation.AndCondition($name, $id)
        $condition = New-Object System.Windows.Automation.AndCondition($namedButton, $process)
        [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    }
    if ($yes.Current.AutomationId -eq 'ReviewBeginButton') {
        $yes.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return
    }
    $nativeHandle = [IntPtr]$yes.Current.NativeWindowHandle
    [uint32]$ownerPid = 0
    [LockInSmoke.Native]::GetWindowThreadProcessId($nativeHandle, [ref]$ownerPid) | Out-Null
    if ($ownerPid -ne $desktopProcess.Id -or $nativeHandle -eq [IntPtr]::Zero) { throw 'Confirmation button does not belong to this test process.' }
    if (-not [LockInSmoke.Native]::PostMessage($nativeHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)) { throw 'Could not confirm the test preflight.' }
}

function Save-WindowImage([string]$Name) {
    $window = Restore-TestWindow
    # Capture the settled page, after its finite 180 ms navigation fade.
    Start-Sleep -Milliseconds 250
    $window = Window-Root
    $bounds = $window.Current.BoundingRectangle
    if ($bounds.Width -lt 500 -or $bounds.Height -lt 400) { throw 'Refusing an invalid/minimized window capture.' }
    $bitmap = New-Object System.Drawing.Bitmap([int]$bounds.Width, [int]$bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $dc = $graphics.GetHdc()
    try { $captured = [LockInSmoke.Native]::PrintWindow([IntPtr]$window.Current.NativeWindowHandle, $dc, 2) }
    finally { $graphics.ReleaseHdc($dc); $graphics.Dispose() }
    try {
        if (-not $captured -or $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Minimized) { throw 'Window capture failed or the test window was minimized during capture.' }
        $bitmap.Save((Join-Path $testFolder $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $bitmap.Dispose() }
}

try {
    $arguments = '--data-path "' + (Join-Path $testFolder 'simulation.db') + '" --pipe-name ' + $pipeName
    $serviceProcess = Start-Process -FilePath $serviceExe -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $testFolder 'host-error.log') -RedirectStandardOutput (Join-Path $testFolder 'host.log')
    Wait-For { try { Send-Request 'GetCapabilities' } catch { $null } } | Out-Null
    $desktopProcess = Start-Process -FilePath $desktopExe -WindowStyle Hidden -PassThru
    Wait-For { Find-Control 'ProfileTab' } | Out-Null
    Set-TestWindowSize 1140 740
    if ($DesignOnly) {
        Wait-For { $label = Find-Control 'DraftProfileLabel'; if ($label -and $label.Current.Name -like '*Coding Study*' -and -not $label.Current.IsOffscreen) { $true } } | Out-Null
        (Window-Root).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Minimized)
        Wait-For { if ((Window-Root).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Minimized) { $true } } | Out-Null
        Save-WindowImage 'first-use.png'
        Write-Output 'PASS: capture restored only the deliberately minimized test window.'
    }
    Select-Tab 'ProfileTab'
    Set-Value 'NameBox' 'UI walkthrough'
    Set-Value 'DurationBox' '1'
    if ($EditorOnly) {
        Select-Tab 'EditorSection4'
        Set-Value 'ResourcesBox' 'https://study.example.org/lesson'
        Select-Tab 'EditorSection3'
        (Find-Control 'VideoOptionsExpander').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Set-Value 'ApprovedBox' 'https://youtu.be/AbCdEf12345'
        Select-Option 'ActionBox' 'Open one replacement tab'
        Select-Option 'DestinationBox' 'Custom HTTPS destination'
        Set-Value 'DestinationUrlBox' 'https://study.example.org/focus'
        Select-Option 'DestinationBox' 'YouTube Home'
        Wait-For { $field = Find-Control 'DestinationUrlBox'; if ($null -eq $field -or $field.Current.IsOffscreen) { $true } } | Out-Null
        Select-Option 'DestinationBox' 'Custom HTTPS destination'
        if ((Find-Control 'DestinationUrlBox').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne 'https://study.example.org/focus') { throw 'Switching destination discarded the custom URL.' }
    }
    Invoke-Control 'SaveButton'
    Wait-For { $profiles = @(Send-Request 'ListProfiles'); if ($profiles.Count -eq 1 -and $profiles[0].durationSeconds -eq 60) { $true } } | Out-Null
    Write-Output 'PASS: profile saved through WPF UI with a one-minute duration.'
    Save-WindowImage 'profile.png'
    if ($DesignOnly) {
        Select-Tab 'FocusTab'
        Set-Value 'FocusDurationBox' '37'
        Wait-For { if ((Find-Control 'RemainingText').Current.Name -eq '37:00') { $true } } | Out-Null
        Invoke-Control 'StartButton'
        Wait-For { if (-not (Find-Control 'ReviewBeginButton').Current.IsOffscreen) { $true } } | Out-Null
        if ((Find-Control 'ReviewText').Current.Name -notlike '*37 minute(s)*') { throw 'Inline duration did not reach review.' }
        if ((Find-Control 'ReviewScopeHeading').Current.Name -ne 'SIMULATION ONLY') { throw 'Review lost the simulation scope.' }
        Select-Tab 'ProfileTab'
        foreach ($id in @('NewProfileButton', 'DuplicateProfileButton', 'NameBox', 'SaveButton')) {
            if ((Find-Control $id).Current.IsEnabled) { throw "Conflicting editor action is enabled during review: $id" }
        }
        Select-Tab 'FocusTab'
        Save-WindowImage 'structured-review.png'
        Invoke-Control 'ReviewBeginButton'
        Wait-For { if (@(Send-Request 'ListProfiles')[0].durationSeconds -eq 2220 -and -not (Find-Control 'CancelCountdownButton').Current.IsOffscreen) { $true } } | Out-Null
        Invoke-Control 'CancelCountdownButton'
        Wait-For { if ((Find-Control 'StartButton').Current.IsEnabled) { $true } } | Out-Null
        Select-Tab 'ProfileTab'
        if (-not (Find-Control 'DuplicateProfileButton').Current.IsEnabled) { throw 'Cancel did not restore profile actions.' }
        Select-Tab 'FocusTab'
        Write-Output 'PASS: review blocks conflicting profile edits; Begin saves the reviewed draft and cancellation restores editing.'
        (Find-Control 'BrowserAlphaBox').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
        Wait-For { if ((Find-Control 'ReadinessTitle').Current.Name -eq 'Connect your browser once') { $true } } | Out-Null
        Invoke-Control 'ReadinessAction'
        if (-not (Find-Control 'BrowserTab').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected) { throw 'Missing-browser guidance did not navigate to setup.' }
        Save-WindowImage 'connection-setup.png'
        Select-Tab 'FocusTab'
        # Readiness reports from an isolated test actor; this installs no browser restriction.
        $pairing = Send-Request 'BeginBrowserPairing'
        $auth = Send-Request 'PairBrowser' @{ code = $pairing.code; installationId = [guid]::NewGuid().ToString('N'); label = 'Simulated browser for design verification' }
        Wait-For { if ((Find-Control 'ReadinessTitle').Current.Name -eq 'Reopen your paired browser') { $true } } | Out-Null
        $profile = @(Send-Request 'ListProfiles')[0]
        Send-Request 'StageBrowserProfile' @{ profileId = $profile.id } | Out-Null
        $poll = Send-Request 'PollBrowser' @{ auth = $auth; readyProfileHash = $null }
        Wait-For { if ((Find-Control 'ReadinessTitle').Current.Name -eq 'Prepare browser access') { $true } } | Out-Null
        Send-Request 'PollBrowser' @{ auth = $auth; readyProfileHash = $poll.stagedProfileHash } | Out-Null
        Wait-For { if ((Find-Control 'ReadinessTitle').Current.Name -eq 'Browser connected') { $true } } | Out-Null
        Select-Tab 'BrowserTab'
        if ((Find-Control 'BrowserSetupExpander').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState -ne [System.Windows.Automation.ExpandCollapseState]::Collapsed) { throw 'Setup did not collapse after enrollment.' }
        Save-WindowImage 'connection-paired.png'
        Select-Tab 'FocusTab'
        Invoke-Control 'StartButton'
        Wait-For { $review = Find-Control 'ReviewBeginButton'; if ($review -and -not $review.Current.IsOffscreen) { $true } } | Out-Null
        Select-Tab 'BrowserTab'
        $manageName = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Manage connection')
        $expandable = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsExpandCollapsePatternAvailableProperty, $true)
        $manageCondition = New-Object System.Windows.Automation.AndCondition($manageName, $expandable)
        $manage = Wait-For { (Window-Root).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $manageCondition) }
        $manage.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        $stage = Wait-For { Find-Control 'StageBrowserButton' }
        if ($stage.Current.IsEnabled -or (Find-Control 'BrowserBox').Current.IsEnabled) { throw 'Review permits competing browser selection or profile staging.' }
        Select-Tab 'FocusTab'
        Invoke-Control 'ReviewBackButton'
        Wait-For { if ((Find-Control 'StartButton').Current.IsEnabled) { $true } } | Out-Null
        if ($null -ne (Send-Request 'GetSessionStatus')) { throw 'Backing out of browser review started a session.' }
        Write-Output 'PASS: browser review blocks competing staging/selection and Back applies no restrictions.'
        Send-Request 'RevokeBrowser' @{ installationId = $auth.installationId } | Out-Null
        Select-Tab 'FocusTab'
        Wait-For { if ((Find-Control 'ReadinessTitle').Current.Name -eq 'Connect your browser once') { $true } } | Out-Null
        (Find-Control 'BrowserAlphaBox').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
        Wait-For { if ((Find-Control 'ReadinessTitle').Current.Name -eq 'Simulation selected') { $true } } | Out-Null
        Set-Value 'FocusDurationBox' '1'
        Write-Output 'PASS: inline custom duration reviewed and saved; unpaired guidance routes to setup; simulation stays explicit.'
        Write-Output 'PASS: offline, access preparation, ready and revoked states follow host reports; connected setup is collapsed.'
        Select-Tab 'HelpTab'
        $motionControl = Wait-For { Find-Control 'ReduceMotionBox' }
        Save-WindowImage 'help-redesign.png'
        $motionControl.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
        Wait-For { if ((Find-Control 'MotionStatusText').Current.Name -match 'reduced') { $true } } | Out-Null
        Select-Tab 'ProfileTab'
        Select-Tab 'EditorSection3'
        Save-WindowImage 'youtube-editor.png'
        Select-Tab 'HistoryTab'
        Wait-For { if ((Find-Control 'HistoryEmptyText').Current.Name -like 'Your sessions will appear*') { $true } } | Out-Null
        Set-TestWindowSize 1140 740
        Select-Tab 'FocusTab'
        Start-Sleep -Milliseconds 300
        Save-WindowImage 'redesign-focus.png'
        foreach ($width in @(620, 768, 1024, 1280, 1440)) {
            Set-TestWindowSize $width 740
            Save-WindowImage ("focus-width-$width.png")
        }
        Set-TestWindowSize 640 600
        Start-Sleep -Milliseconds 300
        Save-WindowImage 'compact-focus.png'
        $startBounds = (Find-Control 'StartButton').Current.BoundingRectangle
        if ((Find-Control 'StartButton').Current.IsOffscreen -or $startBounds.Bottom -gt (Window-Root).Current.BoundingRectangle.Bottom - 18) { throw 'Start is clipped in the compact Focus view.' }
        Select-Tab 'ProfileTab'
        Select-Tab 'EditorSection3'
        Set-Value 'TermsBox' ''
        Select-Tab 'EditorSection0'
        Invoke-Control 'SaveButton'
        Wait-For { if ((Find-Control 'EditorSection3').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected -and (Find-Control 'TermsError').Current.Name) { $true } } | Out-Null
        if ((Find-Control 'SaveButton').Current.IsOffscreen) { throw 'Save is not reachable at narrow window size.' }
        Wait-For { if ((Find-Control 'DraftStatusText').Current.Name -like 'Unsaved*') { $true } } | Out-Null
        Save-WindowImage 'compact-validation.png'
        Set-Value 'TermsBox' 'education'
        Invoke-Control 'SaveButton'
        Wait-For { if ((Find-Control 'DraftStatusText').Current.Name -like 'Saved*') { $true } } | Out-Null
        Select-Tab 'FocusTab'
        Invoke-Control 'StartButton'
        Wait-For { if (-not (Find-Control 'ReviewBeginButton').Current.IsOffscreen) { $true } } | Out-Null
        Save-WindowImage 'compact-review.png'
        Invoke-Control 'ReviewBackButton'
        Wait-For { if ((Find-Control 'StartButton').Current.IsEnabled) { $true } } | Out-Null
        if ($null -ne (Send-Request 'GetSessionStatus')) { throw 'Backing out of review started a session.' }
        Select-Tab 'ProfileTab'
        Select-Tab 'EditorSection3'
        Set-Value 'TermsBox' ''
        (Find-Control 'VideoOptionsExpander').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Set-Value 'ApprovedBox' 'AbCdEf12345'
        Select-Tab 'FocusTab'
        Invoke-Control 'StartButton'
        Wait-For { $rules = Find-Control 'ReviewRulesText'; if ($rules -and $rules.Current.Name -like '*only the approved videos are allowed*') { $true } } | Out-Null
        if ((Find-Control 'ReviewRulesText').Current.Name -like '*no restriction rules*') { throw 'Approved-only allow mode was described as unrestricted.' }
        (Find-Control 'ReviewRulesExpander').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        if ((Find-Control 'ReviewDetailsText').Current.Name -notlike '*AbCdEf12345*') { throw 'Exact approved ID is absent from review.' }
        Select-Tab 'ProfileTab'
        if ((Find-Control 'ApprovedBox').Current.IsEnabled -or (Find-Control 'DuplicateProfileButton').Current.IsEnabled) { throw 'Pending review permits profile changes.' }
        Select-Tab 'FocusTab'
        if ((Find-Control 'ReviewDetailsText').Current.Name -notlike '*AbCdEf12345*' -or (Find-Control 'ReviewDetailsText').Current.Name -like '*12345678901*') { throw 'Review changed with the live editor.' }
        Invoke-Control 'ReviewBackButton'
        Wait-For { if ((Find-Control 'StartButton').Current.IsEnabled) { $true } } | Out-Null
        if ($null -ne (Send-Request 'GetSessionStatus')) { throw 'Backing out of approved-only review started a session.' }
        Select-Tab 'ProfileTab'
        Set-Value 'ApprovedBox' '12345678901'
        if ((Find-Control 'ApprovedBox').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne '12345678901') { throw 'Back did not restore editing.' }
        Write-Output 'PASS: named first-use draft, structured scope, exact approved-only rules and immutable review snapshot.'
        Select-Tab 'ProfileTab'
        Select-Tab 'EditorSection3'
        (Find-Control 'VideoOptionsExpander').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
        Select-Tab 'EditorSection2'
        Set-Value 'SitesBox' 'www.youtube.com'
        (Find-Control 'SaveButton').SetFocus()
        Invoke-Control 'SaveButton'
        Wait-For { $destination = Find-Control 'DestinationBox'; if ($destination -and -not $destination.Current.IsOffscreen -and $destination.Current.HasKeyboardFocus) { $true } } | Out-Null
        if (-not (Find-Control 'DestinationError').Current.Name) { throw 'Built-in destination conflict was not explained.' }
        Write-Output 'PASS: built-in destination conflict reveals settings and focuses the visible destination selector.'
        Write-Output 'PASS: compact navigation, reachable save, validation routing, reduced-motion preference and review cancellation.'
        Write-Output "Evidence directory: $testFolder"
        return
    }
    if ($BrowserRecovery) {
        # Browser messages are simulated through the host pipe; no browser restriction is installed.
        $profile = @(Send-Request 'ListProfiles')[0]
        $pairing = Send-Request 'BeginBrowserPairing'
        $auth = Send-Request 'PairBrowser' @{ code = $pairing.code; installationId = [guid]::NewGuid().ToString('N'); label = 'Simulated browser for UI verification' }
        Send-Request 'StageBrowserProfile' @{ profileId = $profile.id } | Out-Null
        $poll = Send-Request 'PollBrowser' @{ auth = $auth; readyProfileHash = $null }
        Send-Request 'PollBrowser' @{ auth = $auth; readyProfileHash = $poll.stagedProfileHash } | Out-Null
        Select-Tab 'BrowserTab'
        Wait-For { if ((Find-Control 'BrowserStatusText').Current.Name -like '*Ready for the staged profile*') { $true } } | Out-Null
        Select-Tab 'FocusTab'
        (Find-Control 'BrowserAlphaBox').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
        Invoke-Control 'StartButton'
        Wait-For { $scope = Find-Control 'ReviewScopeHeading'; if ($scope -and $scope.Current.Name -like '*REAL RESTRICTIONS*') { $true } } | Out-Null
        if ((Find-Control 'ReviewScopeText').Current.Name -notlike '*Simulated browser for UI verification*') { throw 'Review omitted the chosen browser.' }
        Confirm-Preflight
        $session = Wait-For { Send-Request 'GetSessionStatus' } 20
        Send-Request 'AcknowledgeBrowserPolicy' @{ auth = $auth; sessionId = $session.id; policyHash = $session.policyHash } | Out-Null
        Send-Request 'PollBrowser' @{ auth = $auth; readyProfileHash = $null; appliedSessionId = $session.id; appliedPolicyHash = $session.policyHash } | Out-Null
        Wait-For { if ((Find-Control 'CoverageText').Current.Name -like '*reports this policy installed*') { $true } } | Out-Null
        Send-Request 'PollBrowser' @{ auth = $auth; readyProfileHash = $null; errorCode = 'permissionMissing' } | Out-Null
        Wait-For { if ((Find-Control 'CoverageText').Current.Name -like '*Site access was removed*') { $true } } | Out-Null
        Write-Output 'PASS: desktop reflects reported coverage and permission loss from a simulated browser actor.'
        $recovering = Send-Request 'RecoverSession'
        if ($recovering.state -ne 'active' -or -not $recovering.earlyEndRequestedAtUtc) { throw 'Live browser recovery skipped the Guided delay.' }
        Write-Output 'PASS: recovery of a live browser session waits for the Guided delay.'
        Select-Tab 'HelpTab'
        Wait-For { if ((Send-Request 'GetSessionStatus').state -eq 'cleanupPending') { $true } } 75 | Out-Null
        if ([DateTimeOffset]::UtcNow -lt [DateTimeOffset]$session.endsAtUtc -and (Find-Control 'RemovalRecoveryButton').Current.IsEnabled) { throw 'Owner recovery enabled before original deadline.' }
        Wait-For { if ((Find-Control 'RemovalRecoveryButton').Current.IsEnabled) { $true } } 75 | Out-Null
        (Find-Control 'RemovalConfirmedBox').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
        Invoke-Control 'RemovalRecoveryButton'
        Confirm-Preflight
        Wait-For { if ($null -eq (Send-Request 'GetSessionStatus')) { $true } } | Out-Null
        $history = @(Send-Request 'ListSessions')
        if ($history.Count -ne 1 -or $history[0].cleanupEvidence -ne 'ownerReportedRemoval' -or $history[0].state -ne 'recovered') { throw 'Owner removal evidence was not preserved.' }
        if (@(Send-Request 'GetBrowserConnections').Count -ne 0) { throw 'Owner recovery did not revoke enrollment.' }
        Select-Tab 'FocusTab'
        Wait-For { if ((Find-Control 'StatusText').Current.Name -like '*Cleanup was not verified*') { $true } } | Out-Null
        Save-WindowImage 'owner-recovery.png'
        Write-Output 'PASS: expired pending browser session recovered through WPF, with distinct owner-reported evidence and revoked enrollment.'
        Write-Output "Evidence directory: $testFolder"
        return
    }
    if ($EditorOnly) {
        $profile = @(Send-Request 'ListProfiles')[0]
        if ($profile.studyResources[0] -ne 'https://study.example.org/lesson' -or $profile.youTube.approvedVideoIds[0] -ne 'AbCdEf12345' -or $profile.youTube.tabAction -ne 'openReplacement' -or $profile.youTube.destinationKind -ne 'customHttps' -or $profile.youTube.customDestination -ne 'https://study.example.org/focus') { throw 'Editor fields did not persist correctly.' }
        (Window-Root).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
        Wait-For { $desktopProcess.Refresh(); $desktopProcess.HasExited } | Out-Null
        $desktopProcess = Start-Process -FilePath $desktopExe -WindowStyle Hidden -PassThru
        Wait-For { Find-Control 'ProfileTab' } | Out-Null
        Select-Tab 'ProfileTab'
        Wait-For { $name = Find-Control 'NameBox'; if ($name.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -eq 'UI walkthrough') { $true } } | Out-Null
        Set-Value 'NameBox' 'Updated walkthrough'
        Invoke-Control 'SaveButton'
        Wait-For { $saved = @(Send-Request 'ListProfiles')[0]; if ($saved.name -eq 'Updated walkthrough') { $true } } | Out-Null
        $profile = @(Send-Request 'ListProfiles')[0]
        if ($profile.studyResources[0] -ne 'https://study.example.org/lesson' -or $profile.youTube.approvedVideoIds[0] -ne 'AbCdEf12345' -or $profile.youTube.tabAction -ne 'openReplacement' -or $profile.youTube.customDestination -ne 'https://study.example.org/focus') { throw 'Saving after reopening dropped profile fields.' }
        Write-Output 'PASS: study links, video approvals, tab action and destination survived save, reopen and edit.'
        Write-Output "Evidence directory: $testFolder"
        return
    }
    Select-Tab 'FocusTab'
    Invoke-Control 'StartButton'
    Confirm-Preflight
    Wait-For { $button = Find-Control 'CancelCountdownButton'; if ($button -and $button.Current.IsEnabled) { $button } } | Out-Null
    Invoke-Control 'CancelCountdownButton'
    Wait-For { $button = Find-Control 'StartButton'; if ($button -and $button.Current.IsEnabled) { $true } } | Out-Null
    if ($null -ne (Send-Request 'GetSessionStatus')) { throw 'Cancelling countdown started a session.' }
    if (@(Send-Request 'ListSessions').Count -ne 0) { throw 'Cancelled countdown created a history entry.' }
    Write-Output 'PASS: countdown cancellation applied nothing.'
    Invoke-Control 'StartButton'
    Confirm-Preflight
    $session = Wait-For { Send-Request 'GetSessionStatus' } 20
    if ($session.state -ne 'active') { throw 'Session did not activate.' }
    $originalId = $session.id
    $originalDeadline = $session.endsAtUtc
    (Window-Root).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Wait-For { $desktopProcess.Refresh(); $desktopProcess.HasExited } | Out-Null
    $desktopProcess = Start-Process -FilePath $desktopExe -WindowStyle Hidden -PassThru
    Wait-For { Find-Control 'StatusText' } | Out-Null
    $resumed = Send-Request 'GetSessionStatus'
    if ($resumed.id -ne $originalId -or $resumed.endsAtUtc -ne $originalDeadline) { throw 'Reopening UI changed the session.' }
    Write-Output 'PASS: closing and reopening the UI retained the same session and deadline.'
    Save-WindowImage 'focus.png'
    while ($null -ne (Send-Request 'GetSessionStatus')) {
        if ([DateTimeOffset]::UtcNow -gt ([DateTimeOffset]$originalDeadline).AddSeconds(10)) { throw 'Automatic expiry did not finish on time.' }
        Start-Sleep -Seconds 5
    }
    $history = @(Send-Request 'ListSessions')
    if ($history.Count -ne 1 -or $history[0].state -ne 'completed' -or $history[0].elapsedSeconds -ne 60) { throw 'Completion history is incorrect.' }
    Select-Tab 'FocusTab'
    Wait-For { $status = Find-Control 'StatusText'; if ($status.Current.Name -like '*Completed*') { $true } } | Out-Null
    Write-Output 'PASS: automatic one-minute expiry and completed history were observed through the UI and host.'
    Select-Tab 'HistoryTab'
    Wait-For { if ((Find-Control 'HistorySummaryText').Current.Name -like '1 session *') { $true } } | Out-Null
    Set-Value 'HistorySearchBox' 'no matching profile'
    Wait-For { if ((Find-Control 'HistorySummaryText').Current.Name -like '0 sessions *') { $true } } | Out-Null
    (Find-Control 'HistoryFiltersExpander').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Invoke-Control 'ClearHistoryFiltersButton'
    Select-Option 'HistoryScopeBox' 'Browser alpha'
    Wait-For { if ((Find-Control 'HistorySummaryText').Current.Name -like '0 sessions *') { $true } } | Out-Null
    Select-Option 'HistoryScopeBox' 'Simulation'
    Select-Option 'HistoryPeriodBox' 'Today'
    Set-Value 'HistorySearchBox' 'WALKTHROUGH'
    Wait-For { if ((Find-Control 'HistorySummaryText').Current.Name -like '1 session *0h 1m*') { $true } } | Out-Null
    Set-TestWindowSize 1000 600
    Start-Sleep -Milliseconds 300
    Save-WindowImage 'history.png'
    Write-Output 'PASS: history search, scope/date filters, reset and elapsed summary agree with the completed host session.'
    Write-Output "Evidence directory: $testFolder"
} finally {
    foreach ($ownedProcess in @($desktopProcess, $serviceProcess)) {
        if ($null -ne $ownedProcess) {
            $ownedProcess.Refresh()
            if (-not $ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id -Force }
        }
    }
    $env:DOTNET_ROOT = $oldRoot
    $env:LOCKIN_SIMULATION_PIPE = $oldPipe
    $uiTestMutex.ReleaseMutex()
    $uiTestMutex.Dispose()
}
