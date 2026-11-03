function Wait-VerificationOwnedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Process,
        [Parameter(Mandatory)][string]$ExecutablePath,
        [Parameter(Mandatory)][string]$ReportPath,
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 120,
        [System.Exception]$OriginalFailure
    )

    # The caller passes the exact Process returned by its own launch. No PID
    # lookup, process-tree discovery, forced termination or alternate owner.
    $failure = $OriginalFailure
    $cleanupErrors = [System.Collections.Generic.List[System.Exception]]::new()
    $stream = $null
    $state = [ordered]@{
        StartedUtc = [DateTime]::UtcNow.ToString('o')
        DeclaredLaunchedExecutable = [IO.Path]::GetFullPath($ExecutablePath)
        ProcessId = $null
        ProcessStartedUtc = $null
        TimeoutSeconds = $TimeoutSeconds
        StopGraceSeconds = 10
        Outcome = 'Unconfirmed'
        FailureType = $null
        Failure = $null
        CloseRequestAttempted = $false
        CloseRequestAccepted = $null
        CloseError = $null
        IdentityMatchedBeforeClose = $false
        Exited = $false
        ExitCode = $null
        HandleDisposed = $false
        DisposeError = $null
        CleanupConfirmed = $false
        ForcedTermination = $false
        OwnedProcessRetainedIfAlive = $true
    }
    try {
        $state.ProcessId = $Process.Id
        $state.ProcessStartedUtc = $Process.StartTime.ToUniversalTime().ToString('o')
        # Reserve a new report; never replace an earlier run's evidence.
        $stream = [IO.File]::Open($ReportPath, [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write, [IO.FileShare]::Read)
        if ($null -eq $failure) {
            $watch = [Diagnostics.Stopwatch]::StartNew()
            while (-not $Process.HasExited) {
                $remaining = $TimeoutSeconds * 1000.0 - $watch.Elapsed.TotalMilliseconds
                if ($remaining -le 0) {
                    throw [TimeoutException]::new("Owned verification process exceeded $TimeoutSeconds seconds.")
                }
                $slice = [int][Math]::Max(1, [Math]::Min(100, [Math]::Ceiling($remaining)))
                [void]$Process.WaitForExit($slice)
                $Process.Refresh()
            }
        }
    }
    catch {
        if ($null -eq $failure) { $failure = $_.Exception }
        else { $cleanupErrors.Add($_.Exception) }
    }
    finally {
        if ($null -ne $failure) {
            $state.FailureType = $failure.GetType().FullName
            $state.Failure = $failure.ToString()
            $state.Outcome = if ($failure -is [TimeoutException]) { 'TimedOut' } else { 'Failed' }
            try {
                $Process.Refresh()
                if (-not $Process.HasExited) {
                    $sameId = $Process.Id -eq $state.ProcessId
                    $sameStart = $Process.StartTime.ToUniversalTime().ToString('o') -ceq $state.ProcessStartedUtc
                    if (-not $sameId -or -not $sameStart) {
                        throw [InvalidOperationException]::new('Owned process identity changed; no close request issued.')
                    }
                    $state.IdentityMatchedBeforeClose = $true
                    $state.CloseRequestAttempted = $true
                    try { $state.CloseRequestAccepted = $Process.CloseMainWindow() }
                    catch { $state.CloseError = $_.Exception.ToString(); $cleanupErrors.Add($_.Exception) }
                    # WM_CLOSE is only a request. Explicit-shutdown consumers,
                    # canceled closing or a blocked UI may remain alive.
                    [void]$Process.WaitForExit(10000)
                }
            }
            catch { $cleanupErrors.Add($_.Exception) }
        }
        try {
            $Process.Refresh()
            $state.Exited = $Process.HasExited
            if ($state.Exited) { $state.ExitCode = $Process.ExitCode }
            elseif ($null -eq $failure) {
                $failure = [InvalidOperationException]::new('Owned process exit was not confirmed.')
                $state.Outcome = 'Unconfirmed'
                $state.FailureType = $failure.GetType().FullName
                $state.Failure = $failure.ToString()
            }
        }
        catch { $cleanupErrors.Add($_.Exception) }
        try { $Process.Dispose(); $state.HandleDisposed = $true }
        catch { $state.DisposeError = $_.Exception.ToString(); $cleanupErrors.Add($_.Exception) }
        if ($null -eq $failure -and $state.Exited) { $state.Outcome = 'Exited' }
        $state.CleanupConfirmed = $state.Exited -and $state.HandleDisposed -and $cleanupErrors.Count -eq 0
        $state['CleanupErrors'] = @($cleanupErrors | ForEach-Object { $_.ToString() })
        $state['ReturnedUtc'] = [DateTime]::UtcNow.ToString('o')
        if ($null -ne $stream) {
            try {
                $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($state | ConvertTo-Json -Depth 10))
                $stream.Write($bytes, 0, $bytes.Length)
                $stream.Flush($true)
            }
            catch { $cleanupErrors.Add($_.Exception) }
            finally {
                try { $stream.Dispose() }
                catch { $cleanupErrors.Add($_.Exception) }
            }
        }
    }
    # A late exit, even exit code zero after WM_CLOSE, never converts timeout
    # or an earlier failed assertion into a successful verification.
    if ($cleanupErrors.Count -gt 0) {
        $all = [System.Collections.Generic.List[System.Exception]]::new()
        if ($null -ne $failure) { $all.Add($failure) }
        $all.AddRange($cleanupErrors)
        throw [AggregateException]::new('Owned process failure or cleanup could not be qualified.', $all)
    }
    if ($null -ne $failure) { throw $failure }
    return [pscustomobject]$state
}
