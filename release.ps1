# Builds self-contained release zips into dist\:
#   Switcher-win10-x64.zip  — .NET 8, Windows 10/11 x64
#   Switcher-win7-x64.zip   — .NET 6, Windows 7 SP1 / 8.1 x64
#   Switcher-win7-x86.zip   — .NET 6, Windows 7 SP1 / 8.1 x86
# Usage: powershell -ExecutionPolicy Bypass -File release.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csproj = Join-Path $root 'Switcher.csproj'
$dist = Join-Path $root 'dist'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { $dotnet = "$env:ProgramFiles\dotnet\dotnet.exe" } else { $dotnet = $dotnet.Source }
if (-not (Test-Path $dotnet)) {
    $local = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path $local) { $dotnet = $local }
}
if (-not (Test-Path $dotnet)) { throw "NET SDK not found. Install .NET 8 SDK (it can also target net6)." }

function Publish-Zip([string]$tfm, [string]$rid, [string]$zipName) {
    $out = Join-Path $root ("publish\" + [IO.Path]::GetFileNameWithoutExtension($zipName))
    Write-Host "Publishing $zipName  ($tfm / $rid)..." -ForegroundColor Cyan
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    $args = @(
        'publish', $csproj, '-c', 'Release', '-f', $tfm, '-r', $rid,
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:DebugType=none',
        '-o', $out
    )
    & $dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "publish failed: $zipName" }
    if (-not (Test-Path (Join-Path $out 'Switcher.exe'))) { throw "Switcher.exe missing in $out" }
    if (-not (Test-Path (Join-Path $out 'dict\ru_RU.dic'))) { throw "dictionaries missing in $out" }
    $zip = Join-Path $dist $zipName
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -Force
    $item = Get-Item $zip
    Write-Host ("  {0}  {1:N1} MB" -f $item.Name, ($item.Length / 1MB)) -ForegroundColor Green
}

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force $dist | Out-Null
Remove-Item (Join-Path $root 'publish') -Recurse -Force -ErrorAction SilentlyContinue

Publish-Zip 'net8.0-windows' 'win-x64' 'Switcher-win10-x64.zip'
Publish-Zip 'net6.0-windows' 'win-x64' 'Switcher-win7-x64.zip'
Publish-Zip 'net6.0-windows' 'win-x86' 'Switcher-win7-x86.zip'

# alias expected by older docs / upstream
Copy-Item (Join-Path $dist 'Switcher-win10-x64.zip') (Join-Path $dist 'Switcher-win-x64.zip') -Force

Write-Host ""
Write-Host "Done. Zips:" -ForegroundColor Green
Get-ChildItem $dist -Filter '*.zip' | ForEach-Object {
    Write-Host ("  {0,-28} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB))
}
