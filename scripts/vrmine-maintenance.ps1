[CmdletBinding()]
param(
  [string]$StateRoot,
  [int]$MinimumAgeHours = 168,
  [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$scriptRoot = [IO.Path]::GetFullPath((Split-Path -Parent $MyInvocation.MyCommand.Path)).TrimEnd('\')

function Invoke-GitText {
  param([string]$Repository, [string[]]$Arguments)
  try {
    $output = & git -C $Repository @Arguments 2>&1
    $code = [int]$LASTEXITCODE
    return [ordered]@{ code = $code; output = (($output | ForEach-Object { $_.ToString() }) -join "`n").Trim() }
  } catch {
    return [ordered]@{ code = 127; output = $_.Exception.Message }
  }
}

function Test-LiveProcessOwnsPath {
  param([string]$Path)
  $needle = ([IO.Path]::GetFullPath($Path).TrimEnd('\')).ToLowerInvariant()
  try {
    $processes = Get-CimInstance Win32_Process -ErrorAction Stop
    foreach ($process in $processes) {
      $commandLine = [string]$process.CommandLine
      if ($commandLine -and $commandLine.ToLowerInvariant().Contains($needle)) { return $true }
    }
    return $false
  } catch {
    # An inability to prove that no process owns a run is a safe rejection.
    return $true
  }
}

function Add-Reason {
  param([System.Collections.Generic.List[string]]$Reasons, [string]$Reason)
  if ($Reason -and -not $Reasons.Contains($Reason)) { [void]$Reasons.Add($Reason) }
}

$repoProbe = Invoke-GitText $scriptRoot @('rev-parse', '--show-toplevel')
if ($repoProbe.code -ne 0) { throw "Not a Git checkout: $scriptRoot" }
$repoRoot = [IO.Path]::GetFullPath($repoProbe.output).TrimEnd('\')
$repoParent = Split-Path $repoRoot -Parent
$expectedStateParent = Split-Path $repoParent -Parent
$expectedStateRoot = [IO.Path]::GetFullPath((Join-Path $expectedStateParent 'unity.vrmine-verify')).TrimEnd('\')
if (-not $StateRoot) { $StateRoot = $expectedStateRoot }
$StateRoot = [IO.Path]::GetFullPath($StateRoot).TrimEnd('\')
if ($StateRoot -ne $expectedStateRoot) { throw "Refusing a state root outside the configured VRMine verification root: $StateRoot" }
if ($MinimumAgeHours -lt 1) { throw 'MinimumAgeHours must be at least 1' }

$runsRoot = Join-Path $StateRoot 'runs'
$cutoffUtc = [DateTime]::UtcNow.AddHours(-$MinimumAgeHours)
$records = New-Object 'System.Collections.Generic.List[object]'
$removedCount = 0
$failureCount = 0

if (Test-Path -LiteralPath $runsRoot -PathType Container) {
  foreach ($run in (Get-ChildItem -LiteralPath $runsRoot -Directory -Force | Sort-Object Name)) {
    $reasons = New-Object 'System.Collections.Generic.List[string]'
    $manifestPath = Join-Path $run.FullName '.vrmine-verify\manifest.json'
    $ownerPath = Join-Path $run.FullName '.vrmine-verify\ownership.json'
    $manifest = $null
    $owner = $null

    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
      Add-Reason $reasons 'terminal-manifest-missing'
    } else {
      try { $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { Add-Reason $reasons 'terminal-manifest-unreadable' }
    }
    if (-not (Test-Path -LiteralPath $ownerPath -PathType Leaf)) {
      Add-Reason $reasons 'ownership-manifest-missing'
    } else {
      try { $owner = Get-Content -LiteralPath $ownerPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { Add-Reason $reasons 'ownership-manifest-unreadable' }
    }

    if ($owner) {
      if ([string]$owner.verifier -ne 'vrmine') { Add-Reason $reasons 'owner-mismatch' }
      if ([string]$owner.verificationRoot -ne $StateRoot) { Add-Reason $reasons 'verification-root-mismatch' }
      if ([string]$owner.verificationCheckout -ne ([IO.Path]::GetFullPath($run.FullName).TrimEnd('\'))) { Add-Reason $reasons 'checkout-path-mismatch' }
    }
    if ($manifest) {
      if ([string]$manifest.status -notin @('PASS', 'FAIL', 'UNVERIFIED')) { Add-Reason $reasons 'run-not-terminal' }
      $finishedAt = $null
      try { $finishedAt = [DateTime]::Parse([string]$manifest.finishedAt).ToUniversalTime() } catch {}
      if ($null -eq $finishedAt) { $finishedAt = $run.LastWriteTimeUtc }
      if ($finishedAt -gt $cutoffUtc) { Add-Reason $reasons 'minimum-age-not-reached' }
    }
    if (Test-LiveProcessOwnsPath $run.FullName) { Add-Reason $reasons 'live-process-owns-path-or-process-state-unavailable' }

    $statusProbe = Invoke-GitText $run.FullName @('status', '--porcelain=v1', '--untracked-files=all')
    if ($statusProbe.code -ne 0) { Add-Reason $reasons 'git-status-unavailable' }
    elseif ($statusProbe.output) { Add-Reason $reasons 'git-status-not-clean' }

    $eligible = ($reasons.Count -eq 0)
    $record = [ordered]@{
      path = [IO.Path]::GetFullPath($run.FullName).TrimEnd('\')
      status = if ($manifest) { [string]$manifest.status } else { $null }
      finishedAt = if ($manifest) { [string]$manifest.finishedAt } else { $null }
      eligible = $eligible
      reasons = @($reasons.ToArray())
      action = if ($eligible -and $Apply) { 'remove-requested' } elseif ($eligible) { 'dry-run-eligible' } else { 'deferred' }
    }
    if ($eligible -and $Apply) {
      $remove = Invoke-GitText $repoRoot @('worktree', 'remove', '--', $run.FullName)
      if ($remove.code -eq 0 -and -not (Test-Path -LiteralPath $run.FullName)) { $removedCount++ } else { $failureCount++; $record.action = 'remove-failed'; $record.removeOutput = $remove.output }
    }
    [void]$records.Add($record)
  }
}

$result = [ordered]@{
  schema_version = 1
  verifier = 'vrmine-maintenance'
  state_root = $StateRoot
  minimum_age_hours = $MinimumAgeHours
  apply = [bool]$Apply
  eligible_count = @($records | Where-Object { $_.eligible }).Count
  removed_count = $removedCount
  failure_count = $failureCount
  records = @($records.ToArray())
  generated_at_utc = [DateTime]::UtcNow.ToString('o')
}
$result | ConvertTo-Json -Depth 20
if ($failureCount -gt 0) { exit 1 }
