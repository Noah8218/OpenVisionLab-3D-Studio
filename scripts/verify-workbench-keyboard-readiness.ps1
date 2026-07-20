[CmdletBinding()]
param(
    [string]$ReportPath = "artifacts\ui\20260720-owner-acceptance-readiness\keyboard-ui-automation-report.txt",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Windows.Forms

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $workspaceRoot "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj"
$targetFramework = [regex]::Match(
    (Get-Content $projectPath -Raw),
    "<TargetFramework>(?<value>[^<]+)</TargetFramework>").Groups["value"].Value
$shellExe = Join-Path $workspaceRoot "src\OpenVisionLab.ThreeD.Shell\bin\$Configuration\$targetFramework\OpenVisionLab.ThreeD.Shell.exe"
$recipePath = Join-Path $workspaceRoot "recipes\c3d-xyz-affine-teaching-template.ov3d-teach.json"

if (-not (Test-Path -LiteralPath $shellExe))
{
    throw "Build the Shell first: $shellExe"
}

if (-not (Test-Path -LiteralPath $recipePath))
{
    throw "Teaching recipe is missing: $recipePath"
}

$checks = [System.Collections.Generic.List[string]]::new()
$passed = 0
$total = 0

function Add-Check([string]$Name, [bool]$Condition, [string]$Detail)
{
    $script:total++
    if ($Condition) { $script:passed++ }
    $script:checks.Add("$(if ($Condition) { 'PASS' } else { 'FAIL' }) | $Name | $Detail")
}

function Get-KeyboardAutomationElement(
    [System.Windows.Automation.AutomationElement]$Window,
    [string]$Name)
{
    $condition = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Button)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $Name)))
    return $Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Get-RecipePipelineCount([System.Windows.Automation.AutomationElement]$Window)
{
    $elements = $Window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    $summary = @($elements | Where-Object {
            $_.Current.Name -match '^Recipe pipeline \((\d+) steps\)'
        } | Select-Object -First 1)
    if (-not $summary)
    {
        throw "Recipe pipeline summary was not exposed to UI Automation."
    }

    return [int]([regex]::Match(
        $summary.Current.Name,
        '^Recipe pipeline \((\d+) steps\)').Groups[1].Value)
}

$process = $null
try
{
    $process = Start-Process -FilePath $shellExe -WorkingDirectory $workspaceRoot -ArgumentList @(
        "--ui-language", "en",
        "--tool-teaching-recipe", $recipePath,
        "--tool-teaching-step", "step.edge.ul.horizontal.01",
        "--workbench-bottom-pane", "problems") -WindowStyle Hidden -PassThru

    $processCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $process.Id)
    $window = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do
    {
        Start-Sleep -Milliseconds 250
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            $processCondition)
    }
    until ($window -or [DateTime]::UtcNow -ge $deadline)

    if (-not $window)
    {
        throw "Current Shell main window was not exposed to UI Automation."
    }

    Start-Sleep -Seconds 3
    Add-Check "current Shell window is under test" ($window.Current.Name -eq "OpenVisionLab 3D Studio") "window=$($window.Current.Name)"

    $buttons = $window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Button)))
    $candidate = @($buttons | Where-Object { $_.Current.Name -like "Filter. Ready.*" } | Select-Object -First 1)
    $add = Get-KeyboardAutomationElement $window "Add compatible taught step: Filter"
    Add-Check "compatible Filter candidate has an accessible keyboard target" ($null -ne $candidate -and $candidate.Current.IsEnabled) "found=$($null -ne $candidate);enabled=$(if ($candidate) { $candidate.Current.IsEnabled } else { $false })"
    Add-Check "compatible Filter Add has an accessible keyboard target" ($null -ne $add -and $add.Current.IsEnabled) "found=$($null -ne $add);enabled=$(if ($add) { $add.Current.IsEnabled } else { $false })"

    if ($null -eq $candidate -or $null -eq $add)
    {
        throw "Compatible Filter UI targets were not found."
    }

    $before = Get-RecipePipelineCount $window
    $candidate.SetFocus()
    Start-Sleep -Milliseconds 150
    $candidateFocused = $candidate.Current.HasKeyboardFocus
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
    Start-Sleep -Milliseconds 300
    $afterCandidate = Get-RecipePipelineCount $window
    Add-Check "Enter on compatible candidate selects without adding a step" ($candidateFocused -and $afterCandidate -eq $before) "focused=$candidateFocused;before=$before;after=$afterCandidate"

    $add = Get-KeyboardAutomationElement $window "Add compatible taught step: Filter"
    $add.SetFocus()
    Start-Sleep -Milliseconds 150
    $addFocused = $add.Current.HasKeyboardFocus
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
    Start-Sleep -Milliseconds 500
    $afterAdd = Get-RecipePipelineCount $window
    Add-Check "Enter on explicit compatible Add creates exactly one taught step" ($addFocused -and $afterAdd -eq $before + 1) "focused=$addFocused;before=$before;after=$afterAdd"
}
catch
{
    Add-Check "keyboard automation completed without exception" $false $_.Exception.Message
}
finally
{
    if ($process -and -not $process.HasExited)
    {
        Stop-Process -Id $process.Id -Force
    }
}

$success = $total -gt 0 -and $passed -eq $total
$summary = "WorkbenchKeyboardReadiness|pass=$success|checks=$passed/$total|report=$(Join-Path $workspaceRoot $ReportPath)"
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("OpenVisionLab 3D Workbench keyboard readiness verification")
$lines.Add($summary)
$checks | ForEach-Object { $lines.Add($_) }
$fullReportPath = Join-Path $workspaceRoot $ReportPath
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullReportPath) | Out-Null
[System.IO.File]::WriteAllLines($fullReportPath, $lines)
$summary
if (-not $success) { exit 1 }
