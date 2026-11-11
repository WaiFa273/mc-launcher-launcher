# Seed %APPDATA%\LauncherLauncher\data.json with demo entries for screenshots.
# ASCII-only on purpose; all display strings come from tools/demo-launchers.json.
param(
    [string]$DemoDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "demo"),
    [string]$DataFile = (Join-Path $PSScriptRoot "demo-launchers.json"),
    [string]$Target = "$env:APPDATA\LauncherLauncher\data.json"
)

$json = [System.IO.File]::ReadAllText($DataFile, [System.Text.Encoding]::UTF8)
$items = $json | ConvertFrom-Json
$str = ([System.IO.File]::ReadAllText((Join-Path (Split-Path -Parent $DataFile) "demo-strings.json"), [System.Text.Encoding]::UTF8) | ConvertFrom-Json)

$now = [DateTime]::UtcNow
function Ticks([double]$daysAgo) { return $now.AddDays(-$daysAgo).Ticks }

$entries = @()
$i = 0
foreach ($it in $items) {
    $path = Join-Path $DemoDir $it.file
    $entries += [ordered]@{
        Id                  = [guid]::NewGuid().ToString("N")
        Name                = $it.title
        Path                = $path
        Arguments           = ""
        WorkingDirectory    = ""
        Notes               = ""
        TagsRaw             = "MC, " + $it.title
        RunAsAdmin          = $false
        LaunchCount         = 0
        LastLaunchUtcTicks  = [long]0
        AddedUtcTicks       = (Ticks (30 - $i * 3))
    }
    $i++
}

# give the first few entries some history
$entries[0].LaunchCount = 42; $entries[0].LastLaunchUtcTicks = (Ticks 0.2); $entries[0].TagsRaw = $str.tagsPrimary; $entries[0].Notes = ""
$entries[1].LaunchCount = 17; $entries[1].LastLaunchUtcTicks = (Ticks 1.4)
$entries[2].LaunchCount = 5;  $entries[2].LastLaunchUtcTicks = (Ticks 9)
$entries[3].LaunchCount = 2;  $entries[3].LastLaunchUtcTicks = (Ticks 41)
$entries[4].LaunchCount = 1;  $entries[4].LastLaunchUtcTicks = (Ticks 96)
$entries[5].LaunchCount = 0

# one entry that has run away, to show the missing state
$ghost = [ordered]@{
    Id = [guid]::NewGuid().ToString("N")
    Name = $str.ghostName
    Path = (Join-Path $DemoDir "HMCL-old.exe")
    Arguments = ""
    WorkingDirectory = ""
    Notes = ""
    TagsRaw = $str.ghostTags
    RunAsAdmin = $false
    LaunchCount = 13
    LastLaunchUtcTicks = (Ticks 210)
    AddedUtcTicks = (Ticks 240)
}
$entries += $ghost

$chain = [ordered]@{
    Id = [guid]::NewGuid().ToString("N")
    Name = $str.chainMain
    Mode = 0
    DelayMs = 1200
    Steps = @(
        [ordered]@{ LauncherId = $entries[0].Id; Note = "" },
        [ordered]@{ LauncherId = $entries[1].Id; Note = "" },
        [ordered]@{ LauncherId = $entries[3].Id; Note = "" }
    )
}
$chain2 = [ordered]@{
    Id = [guid]::NewGuid().ToString("N")
    Name = $str.chainPreheat
    Mode = 1
    DelayMs = 800
    Steps = @(
        [ordered]@{ LauncherId = $entries[0].Id; Note = "" },
        [ordered]@{ LauncherId = $entries[2].Id; Note = "" }
    )
}

$db = [ordered]@{
    Version = 1
    Launchers = $entries
    Chains = @($chain, $chain2)
    RecentIds = @($entries[0].Id, $entries[1].Id)
    Settings = [ordered]@{
        AutoScanOnStartup = $false
        DoubleClickToLaunch = $true
        ShowPathInList = $true
        MinimizeToTray = $false
        MinimizeAfterLaunch = $false
        LaunchAllSimultaneous = $false
        LaunchDelayMs = 900
        AutoStartWithWindows = $false
        JavaPath = ""
        SortMode = 0
        SkipConfirmations = $true
    }
}

$out = $db | ConvertTo-Json -Depth 8
$dir = Split-Path -Parent $Target
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
if (Test-Path $Target) { Copy-Item $Target ($Target + ".seeded.bak") -Force }
[System.IO.File]::WriteAllText($Target, $out, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("seeded " + $entries.Count + " entries -> " + $Target)
