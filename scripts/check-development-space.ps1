param(
    [string[]]$Paths = @((Split-Path -Parent $PSScriptRoot), [IO.Path]::GetTempPath()),
    [long]$MinimumFreeBytes = 1GB
)
$ErrorActionPreference = 'Stop'
if ($MinimumFreeBytes -le 0) { throw 'MinimumFreeBytes must be positive.' }
$checkedRoots = @{}
foreach ($candidate in $Paths) {
    $absolute = [IO.Path]::GetFullPath($candidate)
    $root = [IO.Path]::GetPathRoot($absolute)
    if ($checkedRoots.ContainsKey($root)) { continue }
    $checkedRoots[$root] = $true
    $drive = New-Object System.IO.DriveInfo($root)
    if (-not $drive.IsReady) { throw "Development volume is unavailable: $root" }
    $available = $drive.AvailableFreeSpace
    if ($available -lt $MinimumFreeBytes) {
        throw ("Not enough free space on {0}: {1:N0} MB available; {2:N0} MB required. Free at least 5 GB for sustained testing or relocate the workspace and TEMP/TMP. No files were deleted." -f $root, ($available / 1MB), ($MinimumFreeBytes / 1MB))
    }
    Write-Output ("Space check passed: {0} has {1:N0} MB available." -f $root, ($available / 1MB))
}
