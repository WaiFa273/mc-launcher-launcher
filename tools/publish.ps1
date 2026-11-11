# Builds the release exe and drops it into .\publish.
# ASCII-only on purpose.
param(
    [switch]$Portable,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $NoBuild) {
    Write-Host "building..." -ForegroundColor Cyan
    dotnet build -c Release -v quiet --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
}

$exe = Join-Path $root "bin\Release\net48\LauncherLauncher.exe"
if (-not (Test-Path $exe)) { throw "exe not found: $exe" }

$outDir = Join-Path $root "publish"
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path $outDir | Out-Null

Copy-Item $exe (Join-Path $outDir "LauncherLauncher.exe")

# The app is a joke about launchers, so it ships as one file and nothing else.
if ($Portable) {
    New-Item -ItemType File -Path (Join-Path $outDir "portable.txt") | Out-Null
}

$file = Get-Item (Join-Path $outDir "LauncherLauncher.exe")
$kb = [math]::Round($file.Length / 1KB, 1)
Write-Host ""
Write-Host ("publish\LauncherLauncher.exe  " + $kb + " KB") -ForegroundColor Green
Write-Host ("that is " + [math]::Round($file.Length / 1MB, 2) + " MB, budget was 10 MB") -ForegroundColor DarkGray
