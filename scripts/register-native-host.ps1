param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-p]{32}$')][string]$ExtensionId,
    [ValidateSet('Chrome','Edge','Brave')][string]$Browser = 'Chrome',
    [string]$DotnetRoot = "$env:LOCALAPPDATA\LockInDev\dotnet"
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$target = Join-Path (Join-Path $env:LOCALAPPDATA 'LockInBrowserAlpha') $Browser
& (Join-Path $PSScriptRoot 'check-development-space.ps1') -Paths @($workspace, $target, [IO.Path]::GetTempPath())
$vendor = switch ($Browser) { 'Edge' { 'Microsoft\Edge' } 'Brave' { 'Google\Chrome' } default { 'Google\Chrome' } }
$key = "HKCU:\Software\$vendor\NativeMessagingHosts\com.lockin.bridge"
if (Test-Path $key) { throw 'A bridge registration already exists. Remove it using the matching unregister script before replacing it.' }
if (Test-Path (Join-Path $target 'native-host.config.json')) { throw 'Bridge files already exist. Keep the existing registration or use a separate development account; this helper will not overwrite another enrollment.' }
& (Join-Path $DotnetRoot 'dotnet.exe') publish (Join-Path $workspace 'src\LockIn.NativeHost\LockIn.NativeHost.csproj') -c Release -r win-x64 --self-contained true -o $target
if ($LASTEXITCODE -ne 0) { throw 'Native bridge publish failed; no registration was created.' }
$origin = "chrome-extension://$ExtensionId/"
$encoding = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $target 'native-host.config.json'), (@{ allowedOrigins = @($origin) } | ConvertTo-Json), $encoding)
$manifest = Join-Path $target 'com.lockin.bridge.json'
[IO.File]::WriteAllText($manifest, (@{ name = 'com.lockin.bridge'; description = 'LockIn browser alpha bridge'; path = (Join-Path $target 'LockIn.NativeHost.exe'); type = 'stdio'; allowed_origins = @($origin) } | ConvertTo-Json), $encoding)
New-Item -Path $key -Force | Out-Null
Set-Item -LiteralPath $key -Value $manifest
Write-Output "Registered the current-user $Browser bridge for extension $ExtensionId. No service or browser policy was installed."
