# Drive the WPF UI through UI Automation instead of pixel clicks.
# ASCII-only on purpose.
param(
    [Parameter(Mandatory=$true)][int]$ProcessId,
    [string]$Name = "",
    [string]$Action = "invoke",
    [int]$Index = 0,
    [switch]$Dump
)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$win = $null
$bestArea = 0.0
foreach ($w in $wins) {
    $r = $w.Current.BoundingRectangle
    $area = $r.Width * $r.Height
    if ($area -gt $bestArea) { $bestArea = $area; $win = $w }
}
if ($win -eq $null) { Write-Error "window not found for pid $ProcessId"; exit 2 }

if ($Action -eq "restore") {
    $wp = $win.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    $wp.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
    Write-Host "restored window"
    exit 0
}

if ($Dump) {
    $all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($el in $all) {
        $n = $el.Current.Name
        if ($n) {
            $ct = $el.Current.ControlType.ProgrammaticName
            Write-Host ("  " + $ct + " | " + $n)
        }
    }
    exit 0
}

$nameCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
$matches = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
$btn = $null
if ($matches.Count -gt $Index) { $btn = $matches[$Index] }
if ($btn -eq $null) { Write-Error "element '$Name' not found"; exit 3 }

$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$probe = $btn
while ($probe -ne $null -and $probe.Current.ControlType.ProgrammaticName -ne "ControlType.Button") {
    $probe = $walker.GetParent($probe)
}
if ($probe -ne $null) { $btn = $probe }

if ($Action -eq "invoke") {
    $pattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
    Write-Host ("invoked '" + $Name + "'")
} elseif ($Action -eq "select") {
    $pattern = $btn.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $pattern.Select()
    Write-Host ("selected '" + $Name + "'")
} elseif ($Action -eq "focus") {
    $btn.SetFocus()
    Write-Host ("focused '" + $Name + "'")
} else {
    Write-Error "unknown action $Action"
    exit 4
}
