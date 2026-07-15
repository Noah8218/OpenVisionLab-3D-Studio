[CmdletBinding()]
param(
    [string]$ActualPath = "artifacts\research_samples\nist_overhang_x4\OverhangPartX4 Part1 Surface_cleaned.stl",
    [string]$QueryPath = "artifacts\research_samples\nist_overhang_x4\cloudcompare_deviation_20260714\measured_vertices_full.ply",
    [string]$NominalPath = "artifacts\research_samples\nist_overhang_x4\OverhangPart_9x5x5mm.STL",
    [string]$Configuration = "Debug",
    [string]$ArtifactDir = "artifacts\nominal_actual_render_density",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$ArtifactPath = Join-Path $RepoRoot $ArtifactDir
$SummaryPath = Join-Path $ArtifactPath "nominal_actual_render_density_summary.txt"
$Results = New-Object System.Collections.Generic.List[string]
$ModeEvidence = New-Object System.Collections.Generic.List[object]
$Failed = $false

$ExpectedPointCount = 4223524L
$ExpectedActualSha256 = "2108E1B17B2CCE59138C74E5DF4951D407F52A3649C257C3FE942DE874FACA00"
$ExpectedNominalSha256 = "D9FC086CA8C0BC3722709E5C03A39C5C1CF60553845FF62F5699780E1D3C1734"
$ExpectedQuerySha256 = "447CDC6E7703DFDE98431F0A1BA154802FEA02E476F2FC7D06AA09F022874B50"
$DisplayBudgets = [ordered]@{
    Fast = 25000L
    Balanced = 60000L
    Detailed = 150000L
}

function Add-Result {
    param(
        [string]$Status,
        [string]$Name,
        [string]$Detail
    )

    $Results.Add("$Status|$Name|$Detail")
}

function Resolve-RequiredPath {
    param(
        [string]$Name,
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Name is missing: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Invoke-DotNet {
    param(
        [string]$Name,
        [string[]]$Arguments
    )

    Write-Host "==> $Name"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }

    Add-Result "PASS" $Name "exit=0"
}

function Get-RequiredContractLine {
    param(
        [string[]]$Lines,
        [string]$Prefix
    )

    $Matches = @($Lines | Where-Object {
        $_.StartsWith("$Prefix|", [StringComparison]::Ordinal)
    })
    if ($Matches.Count -ne 1) {
        throw "Expected one $Prefix contract line, found $($Matches.Count)."
    }

    return $Matches[0]
}

function ConvertFrom-ContractLine {
    param(
        [string]$Line
    )

    $Fields = @{}
    foreach ($Segment in ($Line -split '\|' | Select-Object -Skip 1)) {
        $Pair = $Segment.Split('=', 2)
        if ($Pair.Count -eq 2) {
            $Fields[$Pair[0]] = $Pair[1]
        }
    }

    return $Fields
}

function Remove-VolatileTimingFields {
    param(
        [string]$Line
    )

    return (($Line -split '\|') | Where-Object {
        $_ -notmatch '^(indexMs|calculationMs|totalMs)='
    }) -join '|'
}

try {
    New-Item -ItemType Directory -Force -Path $ArtifactPath | Out-Null
    $ActualFullPath = Resolve-RequiredPath "Actual source" $ActualPath
    $QueryFullPath = Resolve-RequiredPath "Validation query" $QueryPath
    $NominalFullPath = Resolve-RequiredPath "Nominal source" $NominalPath

    if (-not $SkipBuild) {
        Invoke-DotNet "build solution" @(
            "build", "OpenVisionLab.ThreeDStudio.slnx",
            "-c", $Configuration
        )
    }

    foreach ($Mode in $DisplayBudgets.Keys) {
        $ModeKey = $Mode.ToLowerInvariant()
        $ScreenshotPath = Join-Path $ArtifactPath "viewer_${ModeKey}.png"
        $QualityPath = Join-Path $ArtifactPath "viewer_${ModeKey}_quality.txt"
        $ContractPath = Join-Path $ArtifactPath "viewer_${ModeKey}_contract.txt"
        $NormalizedPath = Join-Path $ArtifactPath "viewer_${ModeKey}_measurement_contract.txt"

        Invoke-DotNet "viewer nominal/actual $Mode density" @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-screenshot", $ScreenshotPath,
            "--smoke-screenshot-quality-report", $QualityPath,
            "--smoke-contracts", $ContractPath,
            "--smoke-density", $Mode,
            "--smoke-nominal-actual", $ActualFullPath, $QueryFullPath, $NominalFullPath,
            "--smoke-publish-result"
        )

        $Quality = Get-Content -LiteralPath $QualityPath -Raw
        if (-not $Quality.Contains("ViewerScreenshotResult|accepted=True")) {
            throw "$Mode screenshot quality was not accepted."
        }

        $Lines = @(Get-Content -LiteralPath $ContractPath)
        $DensityLine = Get-RequiredContractLine $Lines "RenderDensity"
        $ViewModelLine = Get-RequiredContractLine $Lines "NominalActualViewModel"
        $SummaryLine = Get-RequiredContractLine $Lines "NominalActualSummary"
        $InputLine = Get-RequiredContractLine $Lines "NominalActualInput"
        $ResultLine = Get-RequiredContractLine $Lines "NominalActualResult"
        $SignedLine = Get-RequiredContractLine $Lines "NominalActualSignedStatistics"
        $UnsignedLine = Get-RequiredContractLine $Lines "NominalActualUnsignedStatistics"
        $DisplayLine = Get-RequiredContractLine $Lines "NominalActualDisplaySampling"
        $DensityStateLine = Get-RequiredContractLine $Lines "NominalActualDisplayDensityState"

        $Density = ConvertFrom-ContractLine $DensityLine
        $Input = ConvertFrom-ContractLine $InputLine
        $Result = ConvertFrom-ContractLine $ResultLine
        $Display = ConvertFrom-ContractLine $DisplayLine
        $DensityState = ConvertFrom-ContractLine $DensityStateLine
        $DisplayBudget = $DisplayBudgets[$Mode]
        $DisplaySampleCount = [long]::Parse($Display["samples"], [Globalization.CultureInfo]::InvariantCulture)
        $DisplayStride = [long]::Parse($Display["stride"], [Globalization.CultureInfo]::InvariantCulture)
        $MeasuredPointCount = [long]::Parse($Display["measuredPoints"], [Globalization.CultureInfo]::InvariantCulture)

        if (($Density["mode"] -ne $Mode) -or
            ([long]::Parse($Density["maxNominalActualDisplaySamples"], [Globalization.CultureInfo]::InvariantCulture) -ne $DisplayBudget)) {
            throw "$Mode render-density contract does not carry the expected nominal/actual display budget."
        }

        if (($DensityState["current"] -ne $Mode) -or
            ($DensityState["next"] -ne $Mode) -or
            ([long]::Parse($DensityState["currentBudget"], [Globalization.CultureInfo]::InvariantCulture) -ne $DisplayBudget) -or
            ([long]::Parse($DensityState["nextBudget"], [Globalization.CultureInfo]::InvariantCulture) -ne $DisplayBudget) -or
            ($DensityState["changePending"] -ne "False") -or
            ($DensityState["explicitPreviewRequired"] -ne "False")) {
            throw "$Mode display-density state does not identify the completed Preview as current."
        }

        if (($Input["actualSha256"] -ne $ExpectedActualSha256) -or
            ($Input["nominalSha256"] -ne $ExpectedNominalSha256) -or
            ($Input["querySha256"] -ne $ExpectedQuerySha256)) {
            throw "$Mode source identity does not match the fixed NIST baseline."
        }

        if (($Result["status"] -ne "Fail") -or
            ([long]::Parse($Result["points"], [Globalization.CultureInfo]::InvariantCulture) -ne $ExpectedPointCount) -or
            ([long]::Parse($Result["below"], [Globalization.CultureInfo]::InvariantCulture) -ne 548207L) -or
            ([long]::Parse($Result["within"], [Globalization.CultureInfo]::InvariantCulture) -ne 2990143L) -or
            ([long]::Parse($Result["above"], [Globalization.CultureInfo]::InvariantCulture) -ne 685174L) -or
            ($Result["fullQuery"] -ne "True")) {
            throw "$Mode result does not match the fixed full-query NIST baseline."
        }

        if (($MeasuredPointCount -ne $ExpectedPointCount) -or
            ($DisplaySampleCount -le 0) -or
            ($DisplaySampleCount -gt $DisplayBudget) -or
            ($Display["metricsIndependent"] -ne "True")) {
            throw "$Mode display sampling does not preserve the full-query measurement contract."
        }

        $PublishedLines = @($Lines | Where-Object {
            $_.StartsWith("result.nominal-actual-surface-deviation|", [StringComparison]::Ordinal)
        })
        if ($PublishedLines.Count -ne 15) {
            throw "$Mode expected 15 published result/metric/overlay lines, found $($PublishedLines.Count)."
        }

        $MeasurementLines = @(
            $ViewModelLine
            $SummaryLine
            $InputLine
            (Remove-VolatileTimingFields $ResultLine)
            $SignedLine
            $UnsignedLine
        ) + $PublishedLines
        Set-Content -LiteralPath $NormalizedPath -Value $MeasurementLines -Encoding utf8
        $MeasurementHash = (Get-FileHash -LiteralPath $NormalizedPath -Algorithm SHA256).Hash

        $ModeEvidence.Add([pscustomobject]@{
            Mode = $Mode
            DisplayBudget = $DisplayBudget
            DisplaySampleCount = $DisplaySampleCount
            DisplayStride = $DisplayStride
            MeasurementHash = $MeasurementHash
            ScreenshotPath = $ScreenshotPath
            ContractPath = $ContractPath
        })
        Add-Result "PASS" "contract $Mode" "points=$ExpectedPointCount|display=$DisplaySampleCount/$DisplayBudget|stride=$DisplayStride|measurementSha256=$MeasurementHash"
    }

    $MeasurementHashes = @($ModeEvidence | Select-Object -ExpandProperty MeasurementHash -Unique)
    $DisplaySampleCounts = @($ModeEvidence | Select-Object -ExpandProperty DisplaySampleCount -Unique)
    if ($MeasurementHashes.Count -ne 1) {
        throw "Measurement contracts differ across render-density modes."
    }

    if ($DisplaySampleCounts.Count -ne $DisplayBudgets.Count) {
        throw "Display sample counts are not distinct across render-density modes."
    }

    for ($Index = 1; $Index -lt $ModeEvidence.Count; $Index++) {
        if ($ModeEvidence[$Index - 1].DisplaySampleCount -ge $ModeEvidence[$Index].DisplaySampleCount) {
            throw "Display sample counts do not increase from Fast to Balanced to Detailed."
        }
    }

    Add-Result "PASS" "cross-density measurement parity" "measurementSha256=$($MeasurementHashes[0])|displayCounts=$($DisplaySampleCounts -join ',')"

    $PendingScreenshotPath = Join-Path $ArtifactPath "viewer_balanced_to_detailed_pending.png"
    $PendingQualityPath = Join-Path $ArtifactPath "viewer_balanced_to_detailed_pending_quality.txt"
    $PendingContractPath = Join-Path $ArtifactPath "viewer_balanced_to_detailed_pending_contract.txt"
    $PendingNormalizedPath = Join-Path $ArtifactPath "viewer_balanced_to_detailed_pending_measurement_contract.txt"
    Invoke-DotNet "viewer nominal/actual Balanced to Detailed pending density" @(
        "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
        "-c", $Configuration, "--no-build", "--",
        "--smoke-screenshot", $PendingScreenshotPath,
        "--smoke-screenshot-quality-report", $PendingQualityPath,
        "--smoke-contracts", $PendingContractPath,
        "--smoke-density", "Balanced",
        "--smoke-next-density", "Detailed",
        "--smoke-nominal-actual", $ActualFullPath, $QueryFullPath, $NominalFullPath,
        "--smoke-publish-result"
    )

    $PendingQuality = Get-Content -LiteralPath $PendingQualityPath -Raw
    if (-not $PendingQuality.Contains("ViewerScreenshotResult|accepted=True")) {
        throw "Balanced to Detailed pending screenshot quality was not accepted."
    }

    $PendingLines = @(Get-Content -LiteralPath $PendingContractPath)
    $PendingDensity = ConvertFrom-ContractLine (Get-RequiredContractLine $PendingLines "RenderDensity")
    $PendingState = ConvertFrom-ContractLine (Get-RequiredContractLine $PendingLines "NominalActualDisplayDensityState")
    $PendingDisplay = ConvertFrom-ContractLine (Get-RequiredContractLine $PendingLines "NominalActualDisplaySampling")
    $PendingDisplaySampleCount = [long]::Parse($PendingDisplay["samples"], [Globalization.CultureInfo]::InvariantCulture)
    $PendingDisplayStride = [long]::Parse($PendingDisplay["stride"], [Globalization.CultureInfo]::InvariantCulture)
    $BalancedEvidence = $ModeEvidence | Where-Object Mode -eq "Balanced" | Select-Object -First 1

    if (($PendingDensity["mode"] -ne "Detailed") -or
        ($PendingState["current"] -ne "Balanced") -or
        ($PendingState["next"] -ne "Detailed") -or
        ([long]::Parse($PendingState["currentBudget"], [Globalization.CultureInfo]::InvariantCulture) -ne $DisplayBudgets["Balanced"]) -or
        ([long]::Parse($PendingState["nextBudget"], [Globalization.CultureInfo]::InvariantCulture) -ne $DisplayBudgets["Detailed"]) -or
        ($PendingState["changePending"] -ne "True") -or
        ($PendingState["explicitPreviewRequired"] -ne "True") -or
        ($PendingDisplaySampleCount -ne $BalancedEvidence.DisplaySampleCount) -or
        ($PendingDisplayStride -ne $BalancedEvidence.DisplayStride)) {
        throw "Balanced to Detailed pending density changed the completed display before explicit Preview."
    }

    $PendingPublishedLines = @($PendingLines | Where-Object {
        $_.StartsWith("result.nominal-actual-surface-deviation|", [StringComparison]::Ordinal)
    })
    $PendingMeasurementLines = @(
        (Get-RequiredContractLine $PendingLines "NominalActualViewModel")
        (Get-RequiredContractLine $PendingLines "NominalActualSummary")
        (Get-RequiredContractLine $PendingLines "NominalActualInput")
        (Remove-VolatileTimingFields (Get-RequiredContractLine $PendingLines "NominalActualResult"))
        (Get-RequiredContractLine $PendingLines "NominalActualSignedStatistics")
        (Get-RequiredContractLine $PendingLines "NominalActualUnsignedStatistics")
    ) + $PendingPublishedLines
    Set-Content -LiteralPath $PendingNormalizedPath -Value $PendingMeasurementLines -Encoding utf8
    $PendingMeasurementHash = (Get-FileHash -LiteralPath $PendingNormalizedPath -Algorithm SHA256).Hash
    if ($PendingMeasurementHash -ne $BalancedEvidence.MeasurementHash) {
        throw "Balanced to Detailed pending density changed measurement or published evidence."
    }

    Add-Result "PASS" "pending density requires Preview" "current=Balanced|next=Detailed|display=$PendingDisplaySampleCount|stride=$PendingDisplayStride|measurementSha256=$PendingMeasurementHash"
}
catch {
    $Failed = $true
    Add-Result "FAIL" "verification runtime" ($_.Exception.Message -replace '\|', '/')
}

$Status = if ($Failed) { "Fail" } else { "Pass" }
$Header = "NistNominalActualRenderDensityVerification|$Status|modes=$($ModeEvidence.Count)|expectedModes=$($DisplayBudgets.Count)"
Set-Content -LiteralPath $SummaryPath -Value (@($Header) + $Results) -Encoding utf8
Write-Host "NIST nominal/actual render-density verification: $Status ($SummaryPath)"

if ($Failed) {
    exit 1
}
