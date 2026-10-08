param([string]$DotnetRoot = "$env:LOCALAPPDATA\LockInDev\dotnet", [switch]$SkipBuild, [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'check-development-space.ps1')
$sdk = Join-Path $DotnetRoot 'dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) {
    $installed = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $installed) { throw 'Install the .NET 10 SDK or pass -DotnetRoot with its directory.' }
    $sdk = $installed.Source
    $DotnetRoot = Split-Path -Parent $sdk
}
if (-not $SkipBuild) {
    & $sdk build (Join-Path $workspace 'LockIn.slnx') -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. The simulation was not started.' }
}
$oldRoot = $env:DOTNET_ROOT
$env:DOTNET_ROOT = $DotnetRoot
try {
    $service = Join-Path $workspace "src\LockIn.Service\bin\$Configuration\net10.0\LockIn.Service.exe"
    $desktop = Join-Path $workspace "src\LockIn.Desktop\bin\$Configuration\net10.0-windows\LockIn.Desktop.exe"
    if (-not (Test-Path -LiteralPath $service) -or -not (Test-Path -LiteralPath $desktop)) { throw 'Build LockIn once before using the desktop shortcut.' }
    $logFolder = Join-Path $env:LOCALAPPDATA 'LockIn\logs'
    [IO.Directory]::CreateDirectory($logFolder) | Out-Null
    $runId = [DateTime]::Now.ToString('yyyyMMdd-HHmmss')
    $hostProcess = Start-Process -FilePath $service -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logFolder "$runId-host.log") -RedirectStandardError (Join-Path $logFolder "$runId-errors.log")
    # This is the visible application requested by running this launcher.
    Start-Process -FilePath $desktop | Out-Null
    Write-Output "Simulation opened. The background host remains after the window closes. Logs: $logFolder"
    Write-Output "Host process launched: $($hostProcess.Id). If a host already owns the endpoint, this duplicate exits automatically."
} finally { $env:DOTNET_ROOT = $oldRoot }
