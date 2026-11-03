# Shared only by the provenance verifiers' report boundary. Numerical and
# dependency-admission contracts remain in their existing owners.
if ($null -eq ('OpenVisionLab.Provenance.ReportFileIdentity' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'ProvenanceReportFileIdentity.cs')
}

function Get-ProvenanceCanonicalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Empty provenance path.' }
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.StartsWith('\\?\') -or $full.StartsWith('\\.\')) {
        throw 'Device paths are not provenance report paths.'
    }
    $root = [IO.Path]::GetPathRoot($full)
    $tail = $full.Substring($root.Length)
    foreach ($segment in $tail.Split([char[]]@('\', '/'))) {
        if ($segment.Contains(':') -or $segment.EndsWith('.') -or $segment.EndsWith(' ')) {
            throw 'Ambiguous provenance path or alternate data stream.'
        }
    }
    $remaining = [Collections.Generic.Stack[string]]::new()
    $current = $full
    while ($true) {
        $facts = [OpenVisionLab.Provenance.ReportFileIdentity]::Read($current)
        if ($facts.Exists) {
            if ($remaining.Count -gt 0 -and !$facts.IsDirectory) {
                throw 'Provenance path has a file as an ancestor.'
            }
            $canonical = $facts.Path
            while ($remaining.Count -gt 0) { $canonical = [IO.Path]::Combine($canonical, $remaining.Pop()) }
            return [pscustomobject]@{ Path = $canonical; Identity = if ($current -eq $full) { $facts.Identity } else { $null }; Exists = $current -eq $full; IsDirectory = ($current -eq $full -and $facts.IsDirectory) }
        }
        $parent = [IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) {
            throw 'Cannot establish provenance path identity.'
        }
        $remaining.Push([IO.Path]::GetFileName($current))
        $current = $parent
    }
}

function Assert-ProvenanceReportDestination {
    param([Parameter(Mandatory)][string]$ReportPath,
          [string[]]$InputPaths = @(), [string[]]$SourceDirectories = @())
    $report = Get-ProvenanceCanonicalPath $ReportPath
    if ($report.IsDirectory) { throw 'Provenance report destination is a directory.' }
    foreach ($inputPath in $InputPaths) {
        $inputFile = Get-ProvenanceCanonicalPath $inputPath
        if ($report.Path.Equals($inputFile.Path, [StringComparison]::OrdinalIgnoreCase) -or
            ($report.Identity -and $inputFile.Identity -and $report.Identity -ceq $inputFile.Identity)) {
            throw "Provenance report aliases an input: $inputPath"
        }
    }
    foreach ($sourceDirectory in $SourceDirectories) {
        $source = Get-ProvenanceCanonicalPath $sourceDirectory
        $prefix = $source.Path.TrimEnd([char[]]@('\', '/')) + [IO.Path]::DirectorySeparatorChar
        if ($report.Path.Equals($source.Path, [StringComparison]::OrdinalIgnoreCase) -or
            $report.Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Provenance report is inside an audited source tree: $sourceDirectory"
        }
    }
    return [pscustomobject]@{ ReportPath = [IO.Path]::GetFullPath($ReportPath); InputPaths = $InputPaths; SourceDirectories = $SourceDirectories }
}

function Write-ProvenanceReport {
    param([Parameter(Mandatory)]$Destination, [Parameter(Mandatory)][string]$Json)
    # Recheck before directory creation/publication. Callers also check before
    # running verification, so a caught validation failure cannot overwrite input.
    $null = Assert-ProvenanceReportDestination -ReportPath $Destination.ReportPath -InputPaths $Destination.InputPaths -SourceDirectories $Destination.SourceDirectories
    $directory = [IO.Path]::GetDirectoryName($Destination.ReportPath)
    [void][IO.Directory]::CreateDirectory($directory)
    $temporary = [IO.Path]::Combine($directory, '.provenance-report-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    $stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Json + [Environment]::NewLine)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }
    # Replacing the directory entry preserves bytes at any other hard link.
    # A failed write/replace throws and retains the temporary evidence file.
    $null = Assert-ProvenanceReportDestination -ReportPath $Destination.ReportPath -InputPaths $Destination.InputPaths -SourceDirectories $Destination.SourceDirectories
    if ([IO.File]::Exists($Destination.ReportPath)) {
        [IO.File]::Replace($temporary, $Destination.ReportPath, [NullString]::Value)
    }
    else { [IO.File]::Move($temporary, $Destination.ReportPath) }
}
