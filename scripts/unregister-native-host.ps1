param([ValidateSet('Chrome','Edge','Brave')][string]$Browser = 'Chrome')
$ErrorActionPreference = 'Stop'
$vendor = switch ($Browser) { 'Edge' { 'Microsoft\Edge' } default { 'Google\Chrome' } }
$key = "HKCU:\Software\$vendor\NativeMessagingHosts\com.lockin.bridge"
$target = Join-Path (Join-Path $env:LOCALAPPDATA 'LockInBrowserAlpha') $Browser
$expected = Join-Path $target 'com.lockin.bridge.json'
$keys = @($key)
if ($Browser -eq 'Brave') { $keys += 'HKCU:\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.lockin.bridge' }
# Check every registration before removing any, including the earlier Brave-only key.
foreach ($ownedKey in $keys) {
    if ((Test-Path $ownedKey) -and (Get-Item -LiteralPath $ownedKey).GetValue('') -ne $expected) { throw 'Registration points elsewhere; refusing to change it.' }
}
foreach ($ownedKey in $keys) {
    if (Test-Path $ownedKey) { Remove-Item -LiteralPath $ownedKey }
}
# Only this helper's exact configuration files are removed. Published executables are retained.
foreach ($name in @('native-host.config.json','com.lockin.bridge.json')) {
    $file = Join-Path $target $name
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
}
Write-Output 'Bridge registration removed. Disable/remove the extension separately after its session has released.'
