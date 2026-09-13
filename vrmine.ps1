[CmdletBinding()]
param(
  [Parameter(Position = 0)]
  [ValidateSet('verify')]
  [string]$Command = 'verify',
  [string]$Revision,
  [switch]$Full,
  [switch]$Clean,
  [switch]$NoUnity,
  [switch]$NoVpm,
  [switch]$NoBlender,
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$RemainingArguments
)

$ErrorActionPreference = 'Stop'
$startedAt = [DateTime]::UtcNow
$script:lastStepAt = $startedAt
$root = [IO.Path]::GetFullPath((Split-Path -Parent $MyInvocation.MyCommand.Path)).TrimEnd('\')
$rawArguments = @($RemainingArguments + $args | ForEach-Object { [string]$_ })
if ($rawArguments -contains '--full') { $Full = $true }
if ($rawArguments -contains '--clean') { $Clean = $true }
if ($rawArguments -contains '--no-unity') { $NoUnity = $true }
if ($rawArguments -contains '--no-vpm') { $NoVpm = $true }
if ($rawArguments -contains '--no-blender') { $NoBlender = $true }

$steps = New-Object 'System.Collections.Generic.List[object]'
$evidenceRecords = New-Object 'System.Collections.Generic.List[object]'
$fatalStatus = 'FAIL'
$rootCause = $null
$config = $null
$source = $root
$worktree = $null
$state = $null
$baseCommit = $null
$sourceHead = $null
$snapshotTree = $null
$inputId = $null
$sourceStatusBefore = $null
$sourceStatusHashBefore = $null
$sourceContentHashBefore = $null
$tools = [ordered]@{}
$surface = [ordered]@{}
$verificationRoot = $null
$runId = $null
$runMode = if ($Clean) { 'clean-append-only' } else { 'append-only' }
$quarantinedResources = New-Object 'System.Collections.Generic.List[object]'
$changedFiles = @()

function Write-JsonFile {
  param([string]$Path, $Value)
  $parent = Split-Path -Parent $Path
  if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
  $json = $Value | ConvertTo-Json -Depth 50
  [IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}

function Read-JsonFile {
  param([string]$Path)
  return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json)
}

function Hash-Text {
  param([AllowNull()][string]$Text)
  $hashAlgorithm = [Security.Cryptography.SHA256]::Create()
  try {
    $value = if ($null -eq $Text) { '' } else { $Text }
    $bytes = [Text.Encoding]::UTF8.GetBytes($value)
    return ([BitConverter]::ToString($hashAlgorithm.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant()
  } finally {
    $hashAlgorithm.Dispose()
  }
}

function Hash-File {
  param([string]$Path)
  $hashAlgorithm = [Security.Cryptography.SHA256]::Create()
  try {
    return ([BitConverter]::ToString($hashAlgorithm.ComputeHash([IO.File]::ReadAllBytes($Path))) -replace '-', '').ToLowerInvariant()
  } finally {
    $hashAlgorithm.Dispose()
  }
}

function Get-SourceContentFingerprint {
  $tracked = @((Run-Git $source @('diff', '--name-only', 'HEAD')).output -split "`n" | Where-Object { $_ })
  $untracked = @((Run-Git $source @('ls-files', '--others', '--exclude-standard')).output -split "`n" | Where-Object { $_ })
  $paths = @($tracked + $untracked | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Sort-Object -Unique)
  $parts = New-Object 'System.Collections.Generic.List[string]'
  foreach ($relative in $paths) {
    $path = Join-Path $source $relative
    if (Test-Path -LiteralPath $path -PathType Leaf) {
      [void]$parts.Add("$relative`n$(Hash-File $path)")
    } else {
      [void]$parts.Add("$relative`n<missing>")
    }
  }
  return Hash-Text ($parts -join "`n")
}

function Add-Step {
  param(
    [string]$Name,
    [ValidateSet('PASS', 'FAIL', 'UNVERIFIED')]
    [string]$Status,
    [string]$Detail,
    [bool]$Required = $true,
    [string]$Tool,
    [Nullable[int]]$ExitCode,
    [string]$Log,
    [string]$Evidence
  )
  $stepStartedAt = $script:lastStepAt
  $stepFinishedAt = [DateTime]::UtcNow
  $durationMs = [Math]::Round(($stepFinishedAt - $stepStartedAt).TotalMilliseconds, 3)
  $item = [ordered]@{
    name = $Name
    status = $Status
    required = $Required
    detail = $Detail
    tool = $Tool
    exitCode = $ExitCode
    log = $Log
    evidence = $Evidence
    startedAt = $stepStartedAt.ToString('o')
    finishedAt = $stepFinishedAt.ToString('o')
    durationMs = $durationMs
    timestamp = $stepFinishedAt.ToString('o')
  }
  [void]$steps.Add($item)
  $script:lastStepAt = $stepFinishedAt
}

function Add-EvidenceRecord {
  param(
    [string]$Name,
    [string]$Status,
    [string]$CommandLine,
    [Nullable[int]]$ExitCode,
    [string]$Log,
    [string]$ProducerEvidence,
    [object]$Details = $null
  )
  $record = [ordered]@{
    schemaVersion = 1
    name = $Name
    status = $Status
    evidenceLevel = 'U1'
    baseCommit = $baseCommit
    snapshotTree = $snapshotTree
    inputId = $inputId
    verificationCheckout = $worktree
    command = $CommandLine
    exitCode = $ExitCode
    log = $Log
    producerEvidence = $ProducerEvidence
    details = $Details
    timestamp = [DateTime]::UtcNow.ToString('o')
  }
  $safe = $Name -replace '[^A-Za-z0-9_.-]', '_'
  $path = Join-Path $state "evidence\$safe.json"
  Write-JsonFile $path $record
  $record.path = $path
  [void]$evidenceRecords.Add($record)
  return $path
}

function Stop-Verification {
  param([string]$Message, [ValidateSet('FAIL', 'UNVERIFIED')][string]$Status = 'FAIL')
  $script:fatalStatus = $Status
  throw $Message
}

function Run-Git {
  param([string]$Repository, [string[]]$GitArguments)
  try {
    $output = & git -C $Repository @GitArguments 2>&1
    $code = [int]$LASTEXITCODE
    return [ordered]@{
      code = $code
      output = (($output | ForEach-Object { $_.ToString() }) -join "`n").Trim()
    }
  } catch {
    return [ordered]@{ code = 127; output = $_.Exception.Message }
  }
}

function Invoke-Logged {
  param(
    [string]$Name,
    [string]$Executable,
    [string[]]$Arguments,
    [string]$WorkingDirectory,
    [hashtable]$Environment = @{}
  )
  $safe = $Name -replace '[^A-Za-z0-9_.-]', '_'
  $log = Join-Path $state "logs\$safe.log"
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $log) | Out-Null
  $oldLocation = Get-Location
  $oldEnvironment = @{}
  $previousErrorAction = $ErrorActionPreference
  $code = 127
  $errorText = $null
  try {
    foreach ($key in $Environment.Keys) {
      $oldEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
      Set-Item -Path "Env:$key" -Value ([string]$Environment[$key])
    }
    Set-Location -LiteralPath $WorkingDirectory
    # Native tools may use stderr for progress and diagnostics even when the
    # command succeeds. Capture it in the log without turning it into a
    # terminating PowerShell error.
    $ErrorActionPreference = 'Continue'
    $leafName = [IO.Path]::GetFileName($Executable)
    if ($leafName -ieq 'Unity.exe') {
      # Unity.exe is a Windows GUI executable. PowerShell can return from the
      # call operator before the editor process has finished, which would let
      # the next isolated Unity stage race the compiler. Start-Process -Wait
      # makes the stage boundary real; Unity's own -logFile owns its log.
      $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory -Wait -PassThru
      $code = [int]$process.ExitCode
    } else {
      & $Executable @Arguments *> $log
      $code = [int]$LASTEXITCODE
    }
  } catch {
    $errorText = $_.Exception.Message
    [IO.File]::WriteAllText($log, "Invocation error: $errorText`r`n", [Text.UTF8Encoding]::new($false))
  } finally {
    try { Set-Location -LiteralPath $oldLocation } catch {}
    foreach ($key in $Environment.Keys) {
      if ($null -eq $oldEnvironment[$key]) {
        Remove-Item -Path "Env:$key" -ErrorAction SilentlyContinue
      } else {
        Set-Item -Path "Env:$key" -Value $oldEnvironment[$key]
      }
    }
    $ErrorActionPreference = $previousErrorAction
  }
  $output = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw -ErrorAction SilentlyContinue } else { '' }
  return [ordered]@{ code = $code; log = $log; output = [string]$output; error = $errorText }
}

function Find-CommandPath {
  param([string[]]$Names)
  foreach ($name in $Names) {
    $command = Get-Command $name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) {
      if ($command.Source) { return $command.Source }
      if ($command.Path) { return $command.Path }
    }
  }
  return $null
}

function Find-UnityPath {
  param([string]$Version)
  $candidates = New-Object 'System.Collections.Generic.List[string]'
  if ($env:UNITY_EXE) { [void]$candidates.Add($env:UNITY_EXE) }
  $programRoots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ }
  foreach ($programRoot in $programRoots) {
    [void]$candidates.Add((Join-Path $programRoot "Unity\Hub\Editor\$Version\Editor\Unity.exe"))
  }
  $pathTool = Find-CommandPath @('Unity.exe', 'Unity')
  if ($pathTool) { [void]$candidates.Add($pathTool) }
  foreach ($candidate in $candidates) {
    if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
      return [IO.Path]::GetFullPath($candidate)
    }
  }
  return $null
}

function Find-BlenderPath {
  param([string]$Version)
  $candidates = New-Object 'System.Collections.Generic.List[string]'
  if ($env:BLENDER_EXE) { [void]$candidates.Add($env:BLENDER_EXE) }
  if ($worktree) {
    [void]$candidates.Add((Join-Path $worktree ".tools\blender-$Version\blender.exe"))
    [void]$candidates.Add((Join-Path $worktree ".tools\blender-$Version-windows-x64\blender.exe"))
    [void]$candidates.Add((Join-Path $worktree '.tools\blender\blender.exe'))
  }
  if ($source) {
    [void]$candidates.Add((Join-Path $source ".tools\blender-$Version\blender.exe"))
    [void]$candidates.Add((Join-Path $source ".tools\blender-$Version-windows-x64\blender.exe"))
    [void]$candidates.Add((Join-Path $source '.tools\blender\blender.exe'))
  }
  $programRoots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ }
  foreach ($programRoot in $programRoots) {
    [void]$candidates.Add((Join-Path $programRoot "Blender Foundation\Blender $Version\blender.exe"))
  }
  $pathTool = Find-CommandPath @('blender.exe', 'blender')
  if ($pathTool) { [void]$candidates.Add($pathTool) }
  foreach ($candidate in $candidates) {
    if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
      return [IO.Path]::GetFullPath($candidate)
    }
  }
  return $null
}

function Get-UnityProjectVersion {
  param([string]$Repository)
  $path = Join-Path $Repository 'ProjectSettings\ProjectVersion.txt'
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
  $match = Select-String -LiteralPath $path -Pattern '^m_EditorVersion:\s*(\S+)' | Select-Object -First 1
  if ($match) { return $match.Matches[0].Groups[1].Value.Trim() }
  return $null
}

function Get-ToolVersion {
  param([string]$Executable, [string[]]$Arguments = @('--version'))
  if (-not $Executable) { return $null }
  $previousErrorAction = $ErrorActionPreference
  try {
    # Some native tools (notably Blender on Windows) write harmless startup
    # diagnostics to stderr. Do not let those diagnostics abort the probe.
    $ErrorActionPreference = 'Continue'
    $text = & $Executable @Arguments 2>&1
    $code = [int]$LASTEXITCODE
    return [ordered]@{ exitCode = $code; text = (($text | ForEach-Object { $_.ToString() }) -join "`n").Trim() }
  } catch {
    return [ordered]@{ exitCode = 127; text = $_.Exception.Message }
  } finally {
    $ErrorActionPreference = $previousErrorAction
  }
}

function Get-BlenderVersion {
  param([string]$Executable)
  $probe = Get-ToolVersion $Executable @('-b', '--version')
  $text = [string]$probe['text']
  $match = [regex]::Match($text, '(?im)^\s*Blender\s+(\d+\.\d+\.\d+)')
  $version = if ($match.Success) { [string]$match.Groups[1].Value } else { $null }
  return [ordered]@{ probe = $probe; version = $version }
}

function Get-UnityMcpProbe {
  param([string]$ProjectPath)
  $registry = Join-Path $env:LOCALAPPDATA 'UnityMCP\registry'
  if (-not (Test-Path -LiteralPath $registry -PathType Container)) {
    return [ordered]@{ status = 'UNVERIFIED'; detail = 'UnityMCP registry directory is absent' }
  }
  $projectFullPath = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\').ToLowerInvariant()
  $entries = Get-ChildItem -LiteralPath $registry -Filter '*.json' -File -ErrorAction SilentlyContinue
  foreach ($entry in $entries) {
    try {
      $registration = Read-JsonFile $entry.FullName
      $registeredPath = if ($registration.projectPath) { [IO.Path]::GetFullPath([string]$registration.projectPath).TrimEnd('\').ToLowerInvariant() } else { '' }
      if ($registeredPath -ne $projectFullPath) { continue }
      $port = [int]$registration.port
      $response = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$port/" -TimeoutSec 5
      $health = $response.Content | ConvertFrom-Json
      $safeHealth = [ordered]@{
        status = 'PASS'
        httpStatus = [int]$response.StatusCode
        projectName = [string]$health.projectName
        projectPath = $ProjectPath
        unityVersion = [string]$health.unityVersion
        pluginVersion = [string]$health.pluginVersion
        protocolV = [int]$health.protocolV
        compiling = [bool]$health.compiling
        clients = [int]$health.clients
        jobs = [int]$health.jobs
        port = $port
        registryFile = $entry.FullName
      }
      if ([string]$health.status -ne 'ok') { $safeHealth.status = 'FAIL'; $safeHealth.detail = 'health endpoint did not return status=ok' }
      return $safeHealth
    } catch {
      return [ordered]@{ status = 'UNVERIFIED'; detail = "UnityMCP registration exists but health probe failed: $($_.Exception.Message)"; registryFile = $entry.FullName }
    }
  }
  return [ordered]@{ status = 'UNVERIFIED'; detail = 'No UnityMCP registration matched the development checkout' }
}

function Test-UnityLogForErrors {
  param([string[]]$Paths)
  $hits = New-Object 'System.Collections.Generic.List[string]'
  foreach ($path in $Paths) {
    if (-not $path -or -not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    foreach ($line in (Get-Content -LiteralPath $path -ErrorAction SilentlyContinue)) {
      $text = [string]$line
      if ($text -match '(?i)error\s+CS\d+' -or $text -match '(?i)script compilation failed' -or $text -match '(?i)compilation failed' -or $text -match '(?i)error building player' -or $text -match '(?i)assembly .* will not be loaded') {
        if ($hits.Count -lt 20) { [void]$hits.Add($text.Trim()) }
      }
    }
  }
  return @($hits)
}

function Test-BlenderLogForErrors {
  param([string]$Path)
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return @('Blender log is missing') }
  $hits = New-Object 'System.Collections.Generic.List[string]'
  foreach ($line in (Get-Content -LiteralPath $Path -ErrorAction SilentlyContinue)) {
    $text = [string]$line
    if ($text -match '(?i)Traceback \(most recent call last\)' -or $text -match '(?i)^Error: ' -or $text -match '(?i)^(RuntimeError|AssertionError|Exception):') {
      if ($hits.Count -lt 20) { [void]$hits.Add($text.Trim()) }
    }
  }
  return @($hits)
}

function Match-ChangedPath {
  param([string[]]$Paths, [string[]]$Patterns)
  foreach ($path in $Paths) {
    foreach ($pattern in $Patterns) {
      $regex = [regex]::Escape($pattern).Replace('\*', '.*')
      if ($path -match "(?i)^$regex") { return $true }
    }
  }
  return $false
}

function Record-Quarantine {
  param([string]$Path, [string]$Reason)
  if (-not (Test-Path -LiteralPath $Path)) { return }
  $item = [ordered]@{
    schemaVersion = 1
    verifier = 'vrmine'
    path = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    reason = $Reason
    observedAt = [DateTime]::UtcNow.ToString('o')
  }
  $previousState = Join-Path $Path '.vrmine-verify'
  $previousManifest = Join-Path $previousState 'manifest.json'
  if (Test-Path -LiteralPath $previousManifest -PathType Leaf) {
    try {
      $previous = Read-JsonFile $previousManifest
      $item.previousStatus = [string]$previous.status
      $item.previousBaseCommit = [string]$previous.baseCommit
      $item.previousSnapshotTree = [string]$previous.snapshotTree
    } catch {
      $item.previousManifest = 'unreadable'
    }
  }
  [void]$quarantinedResources.Add($item)
  if ($verificationRoot -and (Test-Path -LiteralPath $verificationRoot -PathType Container)) {
    $safe = ([IO.Path]::GetFileName($Path) -replace '[^A-Za-z0-9_.-]', '_')
    $recordPath = Join-Path $verificationRoot "quarantine\$safe-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')).json"
    try { Write-JsonFile $recordPath $item } catch {}
  }
}

function Add-VerificationWorktree {
  if (Test-Path -LiteralPath $worktree) {
    Stop-Verification "Unique verification run path unexpectedly exists; refusing to delete or reuse it: $worktree" 'UNVERIFIED'
  }
  $parent = Split-Path -Parent $worktree
  New-Item -ItemType Directory -Force -Path $parent | Out-Null
  $added = Run-Git $source @('worktree', 'add', '--detach', $worktree, $baseCommit)
  # Git can finish materializing the worktree while Windows PowerShell
  # reports its progress stream as a native non-zero result. Verify the
  # resulting checkout itself before treating creation as a failure.
  if ($added.code -ne 0 -and -not (Test-Path -LiteralPath (Join-Path $worktree '.git'))) {
    Stop-Verification "Could not create verification worktree: exit=$($added.code); $($added.output)" 'FAIL'
  }
  $check = Run-Git $worktree @('rev-parse', 'HEAD')
  if ($check.code -ne 0 -or $check.output -ne $baseCommit) { Stop-Verification "Verification worktree revision mismatch: expected $baseCommit, found $($check.output)" 'FAIL' }
  New-Item -ItemType Directory -Force -Path (Join-Path $worktree '.vrmine-verify') | Out-Null
  foreach ($directory in @('evidence', 'logs', 'cache', 'lock')) { New-Item -ItemType Directory -Force -Path (Join-Path $state $directory) | Out-Null }
  $owner = [ordered]@{
    verifier = 'vrmine'
    schemaVersion = 1
    sourceCheckout = $source
    verificationRoot = $verificationRoot
    verificationCheckout = $worktree
    runId = $runId
    runMode = $runMode
    cleanupPolicy = 'maintenance-only'
    createdAt = [DateTime]::UtcNow.ToString('o')
  }
  Write-JsonFile (Join-Path $state 'ownership.json') $owner
}

function Materialize-DirtySnapshot {
  param([string]$SourceHead)
  $snapshotPath = Join-Path $state 'snapshot.json'
  $patchPath = Join-Path $state 'cache\source-dirty.patch'
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $patchPath) | Out-Null
  & git '-C' $source 'diff' '--binary' '--no-ext-diff' 'HEAD' "--output=$patchPath" 2>&1 | Out-Null
  $patchCode = [int]$LASTEXITCODE
  if ($patchCode -ne 0) { Stop-Verification "Could not capture the development checkout diff (exit $patchCode)" 'FAIL' }
  if ((Test-Path -LiteralPath $patchPath -PathType Leaf) -and (Get-Item -LiteralPath $patchPath).Length -gt 0) {
    $applied = Run-Git $worktree @('apply', '--binary', '--whitespace=nowarn', $patchPath)
    if ($applied.code -ne 0) { Stop-Verification "Could not apply the protected dirty snapshot to the verification worktree: $($applied.output)" 'FAIL' }
  }
  $untracked = @((Run-Git $source @('ls-files', '--others', '--exclude-standard')).output -split "`n" | Where-Object { $_ })
  foreach ($relative in $untracked) {
    $relative = $relative.Trim()
    if (-not $relative) { continue }
    $sourcePath = Join-Path $source $relative
    $targetPath = Join-Path $worktree $relative
    if (-not (Test-Path -LiteralPath $sourcePath)) { continue }
    if (Test-Path -LiteralPath $sourcePath -PathType Container) {
      New-Item -ItemType Directory -Force -Path $targetPath | Out-Null
      Get-ChildItem -LiteralPath $sourcePath -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $targetPath -Recurse -Force }
    } else {
      New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetPath) | Out-Null
      Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
    }
  }
  $added = Run-Git $worktree @('add', '-A', '--')
  if ($added.code -ne 0) { Stop-Verification "Could not stage the isolated input snapshot: $($added.output)" 'FAIL' }
  $tree = Run-Git $worktree @('write-tree')
  if ($tree.code -ne 0 -or $tree.output -notmatch '^[0-9a-f]{40}$') { Stop-Verification "Could not calculate the isolated snapshot tree: $($tree.output)" 'FAIL' }
  $script:snapshotTree = $tree.output
  $script:inputId = Hash-Text "$baseCommit`n$snapshotTree"
  $changed = @((Run-Git $worktree @('diff', '--cached', '--name-only', $baseCommit)).output -split "`n" | Where-Object { $_ })
  $snapshot = [ordered]@{ schemaVersion = 1; verifier = 'vrmine'; baseCommit = $baseCommit; sourceHead = $SourceHead; sourceStatusHash = $sourceStatusHashBefore; sourceContentHash = $sourceContentHashBefore; sourceStatus = $sourceStatusBefore; snapshotTree = $snapshotTree; inputId = $inputId; changedFiles = $changed; createdAt = [DateTime]::UtcNow.ToString('o') }
  Write-JsonFile $snapshotPath $snapshot
  return $changed
}

function Materialize-Tools {
  $sourceTools = Join-Path $source '.tools'
  $targetTools = Join-Path $worktree '.tools'
  New-Item -ItemType Directory -Force -Path $targetTools | Out-Null
  $sourceVrcGet = Join-Path $sourceTools 'vrc-get.exe'
  $targetVrcGet = Join-Path $targetTools 'vrc-get.exe'
  if (Test-Path -LiteralPath $sourceVrcGet -PathType Leaf) {
    $toolchain = Read-JsonFile (Join-Path $source 'config\vrchat-toolchain.json')
    $expected = [string]$toolchain.vrcGet.assets.'win32-x64'.sha256
    $actual = Hash-File $sourceVrcGet
    if ($expected -and $actual -ne $expected.ToLowerInvariant()) { Add-Step 'pinned-vrc-get' 'FAIL' "Pinned vrc-get checksum mismatch: expected=$expected actual=$actual" $true $sourceVrcGet 1; return }
    if (-not (Test-Path -LiteralPath $targetVrcGet -PathType Leaf) -or (Hash-File $targetVrcGet) -ne $actual) { Copy-Item -LiteralPath $sourceVrcGet -Destination $targetVrcGet -Force }
    $tools['vrc-get'] = [ordered]@{ path = $targetVrcGet; sha256 = $actual }
  } else { $tools['vrc-get'] = $null }
  $sourceVenv = Join-Path $sourceTools 'venv'
  $targetVenv = Join-Path $targetTools 'venv'
  if ((Test-Path -LiteralPath (Join-Path $sourceVenv 'Scripts\python.exe') -PathType Leaf) -and -not (Test-Path -LiteralPath (Join-Path $targetVenv 'Scripts\python.exe') -PathType Leaf)) { Copy-Item -LiteralPath $sourceVenv -Destination $targetTools -Recurse -Force }
  $tools['python'] = if (Test-Path -LiteralPath (Join-Path $targetVenv 'Scripts\python.exe') -PathType Leaf) { Join-Path $targetVenv 'Scripts\python.exe' } else { $null }
}

function Assert-SnapshotStable {
  $stage = Run-Git $worktree @('add', '-A', '--')
  if ($stage.code -ne 0) { return [ordered]@{ stable = $false; detail = $stage.output } }
  $tree = Run-Git $worktree @('write-tree')
  if ($tree.code -ne 0) { return [ordered]@{ stable = $false; detail = $tree.output } }
  return [ordered]@{ stable = ($tree.output -eq $snapshotTree); detail = "expected=$snapshotTree actual=$($tree.output)" }
}

try {
  $configPath = Join-Path $root 'config\vrmine-request.json'
  if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { Stop-Verification "Request manifest is missing: $configPath" 'UNVERIFIED' }
  $config = Read-JsonFile $configPath
  if ([int]$config.schemaVersion -ne 1) { Stop-Verification 'Unsupported vrmine request manifest schema' 'FAIL' }
  $gitRootProbe = Run-Git $source @('rev-parse', '--show-toplevel')
  if ($gitRootProbe.code -ne 0) { Stop-Verification "Development checkout is not a Git repository: $source" 'UNVERIFIED' }
  $gitRoot = [IO.Path]::GetFullPath($gitRootProbe.output).TrimEnd('\')
  if ($gitRoot -ne $source) { Stop-Verification "Verifier must be launched from the repository checkout; resolved root=$gitRoot expected=$source" 'UNVERIFIED' }
  $origin = Run-Git $source @('remote', 'get-url', 'origin')
  if ($origin.code -ne 0 -or $origin.output -ne [string]$config.repository.remote) { Add-Step 'origin' 'FAIL' "expected=$($config.repository.remote) actual=$($origin.output)" $true 'git' $origin.code; Stop-Verification 'origin remote does not match KAFKA2306/vrmine' 'FAIL' }
  Add-Step 'origin' 'PASS' $origin.output $true 'git' 0
  $sourceHeadProbe = Run-Git $source @('rev-parse', 'HEAD')
  if ($sourceHeadProbe.code -ne 0) { Stop-Verification 'Development checkout has no resolvable HEAD' 'UNVERIFIED' }
  $sourceHead = $sourceHeadProbe.output
  $requestedRevision = if ($Revision) { $Revision } else { $sourceHead }
  $revisionProbe = Run-Git $source @('rev-parse', '--verify', "$requestedRevision`^{commit}")
  if ($revisionProbe.code -ne 0) { Stop-Verification "Requested revision cannot be resolved: $requestedRevision" 'UNVERIFIED' }
  $baseCommit = $revisionProbe.output
  $sourceStatusBefore = (Run-Git $source @('status', '--short', '--branch')).output
  $sourcePorcelainBefore = (Run-Git $source @('status', '--porcelain=v1', '--untracked-files=all')).output
  $sourceStatusHashBefore = Hash-Text $sourcePorcelainBefore
  $sourceContentHashBefore = Get-SourceContentFingerprint
  Add-Step 'revision' 'PASS' "baseCommit=$baseCommit; sourceHead=$sourceHead; dirty=$([bool]$sourcePorcelainBefore)" $true 'git' 0
  $sourceParent = Split-Path $source -Parent
  $verificationParent = Split-Path $sourceParent -Parent
  $verificationRoot = [IO.Path]::GetFullPath((Join-Path $verificationParent ([string]$config.verification.verificationCheckoutSibling))).TrimEnd('\')
  if ($verificationRoot -eq $source) { Stop-Verification 'Verification root resolves to the development checkout' 'FAIL' }
  if ($sourcePorcelainBefore -and $sourceHead -ne $baseCommit) { Stop-Verification 'A dirty development checkout cannot be combined with a different requested revision' 'UNVERIFIED' }
  if (Test-Path -LiteralPath $verificationRoot -PathType Leaf) { Stop-Verification "Verification root exists as a file: $verificationRoot" 'UNVERIFIED' }
  New-Item -ItemType Directory -Force -Path $verificationRoot | Out-Null
  $legacyState = Join-Path $verificationRoot '.vrmine-verify'
  if (Test-Path -LiteralPath $legacyState -PathType Container) { Record-Quarantine $verificationRoot 'legacy-root-worktree-or-prior-run' }
  $runsDirectory = [string]$config.verification.runDirectory
  if (-not $runsDirectory) { $runsDirectory = 'runs' }
  $runsRoot = [IO.Path]::GetFullPath((Join-Path $verificationRoot $runsDirectory)).TrimEnd('\')
  New-Item -ItemType Directory -Force -Path $runsRoot | Out-Null
  $shortCommit = $baseCommit.Substring(0, [Math]::Min(8, $baseCommit.Length))
  $runId = "$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))-$shortCommit"
  $runCandidate = Join-Path $runsRoot $runId
  $collision = 0
  while (Test-Path -LiteralPath $runCandidate) {
    $collision++
    $runCandidate = Join-Path $runsRoot "$runId-$collision"
  }
  $worktree = [IO.Path]::GetFullPath($runCandidate).TrimEnd('\')
  $state = Join-Path $worktree '.vrmine-verify'
  Add-VerificationWorktree
  $state = Join-Path $worktree '.vrmine-verify'
  foreach ($directory in @('evidence', 'logs', 'cache', 'lock')) { New-Item -ItemType Directory -Force -Path (Join-Path $state $directory) | Out-Null }
  $changedFiles = @(Materialize-DirtySnapshot $sourceHead)
  Add-Step 'snapshot' 'PASS' "baseCommit=$baseCommit; snapshotTree=$snapshotTree; inputId=$inputId; files=$($changedFiles.Count)" $true 'git' 0
  $surface = [ordered]@{
    static = $true
    vpm = $true
    blender = ($Full -or (Match-ChangedPath $changedFiles @('Taskfile.yml', 'scripts/*blender', 'scripts/*world_item', 'scripts/*world_build', 'scripts/test_world_item_geometry.py', 'config/world-items/', 'config/world-design/', '*.blend', '*.fbx', '*.glb', '*.obj')))
    unity = ($Full -or (Match-ChangedPath $changedFiles @('Assets/', 'Packages/', 'ProjectSettings/', 'scripts/*unity', '*.cs', '*.unity', '*.prefab', '*.asset')))
    bridge = ($Full -or (Match-ChangedPath $changedFiles @('Packages/manifest.json', 'ProjectSettings/', 'Assets/', 'scripts/*unity')))
  }
  Add-Step 'surface-detection' 'PASS' (([ordered]@{ full = [bool]$Full; selected = $surface; changedFiles = $changedFiles } | ConvertTo-Json -Compress)) $true 'vrmine-request.json' 0
  $markers = @('Assets', 'Packages\manifest.json', 'ProjectSettings\ProjectVersion.txt', 'config\vrmine-request.json') | Where-Object { Test-Path -LiteralPath (Join-Path $worktree $_) }
  $ignorePath = Join-Path $worktree '.gitignore'
  $ignoreText = if (Test-Path -LiteralPath $ignorePath) { Get-Content -LiteralPath $ignorePath -Raw } else { '' }
  $requiredIgnorePatterns = @('/.vrmine-verify/', '/.tools/', '/.artifacts/', '/[Ll]ibrary/', '/[Tt]emp/', '/[Ll]ogs/')
  $missingIgnore = @($requiredIgnorePatterns | Where-Object { $ignoreText -notmatch [regex]::Escape($_) })
  $secretHits = New-Object 'System.Collections.Generic.List[string]'
  foreach ($relative in $changedFiles) {
    $extension = [IO.Path]::GetExtension($relative).ToLowerInvariant()
    if ($extension -notin @('.json', '.yml', '.yaml', '.ps1', '.cmd', '.mjs', '.js', '.py', '.md', '.txt')) { continue }
    $path = Join-Path $worktree $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $text = Get-Content -LiteralPath $path -Raw -ErrorAction SilentlyContinue
    if ($text -match '(?i)(ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|-----BEGIN (RSA|OPENSSH|EC) PRIVATE KEY-----)') { [void]$secretHits.Add($relative) }
  }
  $staticOk = ($markers.Count -eq 4 -and $missingIgnore.Count -eq 0 -and $secretHits.Count -eq 0)
  $staticDetail = "markers=$($markers.Count)/4; missingIgnore=$($missingIgnore -join ','); secretFiles=$($secretHits -join ',')"
  Add-Step 'static-checks' $(if ($staticOk) { 'PASS' } else { 'FAIL' }) $staticDetail $true 'git' $(if ($staticOk) { 0 } else { 1 })
  if (-not $staticOk) { Stop-Verification 'Static repository hygiene checks failed' 'FAIL' }
  $projectUnityVersion = Get-UnityProjectVersion $worktree
  $expectedUnityVersion = [string]$config.toolchain.unityVersion
  if ($projectUnityVersion -ne $expectedUnityVersion) { Add-Step 'unity-project-version' 'FAIL' "expected=$expectedUnityVersion actual=$projectUnityVersion" $true 'ProjectSettings/ProjectVersion.txt' 1; Stop-Verification 'Unity project version is not the pinned version' 'FAIL' }
  Add-Step 'unity-project-version' 'PASS' $projectUnityVersion $true 'ProjectSettings/ProjectVersion.txt' 0
  Materialize-Tools
  $nodePath = Find-CommandPath @('node.exe', 'node')
  $taskPath = Find-CommandPath @('task.exe', 'task')
  $uvPath = Find-CommandPath @('uv.exe', 'uv')
  $gitPath = Find-CommandPath @('git.exe', 'git')
  $unityPath = if ($surface.unity -and -not $NoUnity) { Find-UnityPath $expectedUnityVersion } else { $null }
  $blenderPath = if ($surface.blender -and -not $NoBlender) { Find-BlenderPath ([string]$config.toolchain.blenderVersion) } else { $null }
  $vrcGetPath = Join-Path $worktree '.tools\vrc-get.exe'
  $pythonPath = Join-Path $worktree '.tools\venv\Scripts\python.exe'
  $tools['git'] = $gitPath
  $tools['git-lfs'] = if ($gitPath) { ((Run-Git $worktree @('lfs', 'version')).output) } else { $null }
  $tools['node'] = $nodePath
  $tools['task'] = $taskPath
  $tools['uv'] = $uvPath
  $tools['unity'] = $unityPath
  $tools['blender'] = $blenderPath
  $tools['python'] = $pythonPath
  if ($vrcGetPath -and (Test-Path -LiteralPath $vrcGetPath -PathType Leaf)) { $vrcVersionProbe = Get-ToolVersion $vrcGetPath; $tools['vrc-get'] = [ordered]@{ path = $vrcGetPath; version = $vrcVersionProbe.text; exitCode = $vrcVersionProbe.exitCode; sha256 = Hash-File $vrcGetPath } } else { $vrcVersionProbe = $null; $tools['vrc-get'] = $null }
  $toolFailures = New-Object 'System.Collections.Generic.List[string]'
  $toolUnverified = New-Object 'System.Collections.Generic.List[string]'
  if (-not $gitPath) { [void]$toolUnverified.Add('git') }
  if (-not $tools['git-lfs']) { [void]$toolUnverified.Add('git-lfs') }
  if (-not $nodePath) { [void]$toolUnverified.Add('node') }
  if (-not $taskPath) { [void]$toolUnverified.Add('task') }
  if (-not $uvPath) { [void]$toolUnverified.Add('uv') }
  if ($surface.vpm -and (-not $vrcVersionProbe -or $vrcVersionProbe.exitCode -ne 0)) { [void]$toolUnverified.Add('vrc-get') }
  if ($vrcVersionProbe -and $vrcVersionProbe.text -notmatch [regex]::Escape([string]$config.toolchain.vrcGetVersion)) { [void]$toolFailures.Add('vrc-get version') }
  if (-not $pythonPath -or -not (Test-Path -LiteralPath $pythonPath -PathType Leaf)) { [void]$toolUnverified.Add('python') }
  if ($surface.unity -and -not $unityPath) { [void]$toolUnverified.Add('Unity 2022.3.22f1') }
  $blenderVersionProbe = $null
  if ($surface.blender) {
    if (-not $blenderPath) { [void]$toolUnverified.Add("Blender $($config.toolchain.blenderVersion)") }
    else {
      $blenderVersionProbe = Get-BlenderVersion $blenderPath
      $detectedBlenderVersion = [string]$blenderVersionProbe['version']
      $tools['blenderVersion'] = $detectedBlenderVersion
      $probe = $blenderVersionProbe['probe']
      $tools['blenderProbe'] = [ordered]@{ exitCode = $probe['exitCode']; text = [string]$probe['text'] }
      if ($detectedBlenderVersion.Trim() -ne [string]$config.toolchain.blenderVersion) { [void]$toolFailures.Add("Blender version (found $detectedBlenderVersion)") }
    }
  }
  $toolStatus = if ($toolFailures.Count -gt 0) { 'FAIL' } elseif ($toolUnverified.Count -gt 0) { 'UNVERIFIED' } else { 'PASS' }
  $toolDetail = "missing=$($toolUnverified -join ','); mismatch=$($toolFailures -join ',')"
  Add-Step 'toolchain' $toolStatus $toolDetail $true 'pinned toolchain' $(if ($toolStatus -eq 'PASS') { 0 } else { 1 })
  if ($toolStatus -eq 'FAIL') { Stop-Verification 'Pinned toolchain mismatch' 'FAIL' }
  if ($toolStatus -eq 'UNVERIFIED' -and (($surface.vpm -and -not $vrcVersionProbe) -or ($surface.unity -and -not $unityPath) -or ($surface.blender -and -not $blenderPath))) { Stop-Verification "Required producer is unavailable: $toolDetail" 'UNVERIFIED' }
  if ($surface.bridge) {
    if ($config.gates.unityBridgeHealth -and -not $NoUnity) {
      $bridgeProbe = Get-UnityMcpProbe $source
      $bridgeStatus = if ($bridgeProbe.status -eq 'PASS') { 'PASS' } else { 'UNVERIFIED' }
      $bridgeEvidence = Add-EvidenceRecord 'unity-bridge-health' $bridgeStatus 'GET http://127.0.0.1:<registered-port>/' 0 $null $null $bridgeProbe
      Add-Step 'unity-bridge-health' $bridgeStatus ([string]($bridgeProbe | ConvertTo-Json -Compress)) $false 'UnityMCP/TunaSync' 0 $null $bridgeEvidence
    } else {
      $bridgeReason = if ($NoUnity) { 'optional Unity bridge probe was disabled by --no-unity' } else { 'optional Unity bridge probe is disabled by request configuration' }
      Add-Step 'unity-bridge-health' 'UNVERIFIED' $bridgeReason $false 'UnityMCP/TunaSync' 2
    }
  } else { Add-Step 'unity-bridge-health' 'PASS' 'not selected by request manifest surface detection' $false 'UnityMCP/TunaSync' 0 }
  $sourceStatusNow = (Run-Git $source @('status', '--porcelain=v1', '--untracked-files=all')).output
  if ((Hash-Text $sourceStatusNow) -ne $sourceStatusHashBefore -or (Get-SourceContentFingerprint) -ne $sourceContentHashBefore) { Add-Step 'development-checkout-protection' 'UNVERIFIED' 'development checkout changed while preparing verification' $true 'git' 2; Stop-Verification 'Development checkout changed during verification setup' 'UNVERIFIED' }
  Add-Step 'development-checkout-protection' 'PASS' 'development checkout status fingerprint unchanged' $true 'git' 0
  if ($surface.vpm) {
    if ($NoVpm) { Add-Step 'task-vpm-check' 'UNVERIFIED' 'VPM verification was disabled by --no-vpm' $true $taskPath 2; Stop-Verification 'Required VPM verification was disabled' 'UNVERIFIED' }
    $vpmRun = Invoke-Logged 'task-vpm-check' $taskPath @($config.gates.vpmTask) $worktree
    $vpmProducer = Join-Path $worktree '.artifacts\vpm-u1.json'
    $vpmEvidence = $null
    $vpmValid = $false
    $vpmDetail = "exit=$($vpmRun.code); log=$($vpmRun.log)"
    if ($vpmRun.code -eq 0 -and (Test-Path -LiteralPath $vpmProducer -PathType Leaf)) {
      try {
        $vpmEvidence = Read-JsonFile $vpmProducer
        $vpmValid = ([string]$vpmEvidence.unityVersion -eq $expectedUnityVersion -and [string]$vpmEvidence.vrchatSdkTarget -eq [string]$config.toolchain.vrchatSdkVersion -and [int]$vpmEvidence.resolve.status -eq 0 -and [int]$vpmEvidence.outdated.status -eq 0 -and [string]$vpmEvidence.canonicalHashes.before.manifest -eq [string]$vpmEvidence.canonicalHashes.after.manifest -and [string]$vpmEvidence.canonicalHashes.before.vpmManifest -eq [string]$vpmEvidence.canonicalHashes.after.vpmManifest)
        if (-not $vpmValid) { $vpmDetail += '; producer evidence fields are incomplete or inconsistent' }
      } catch { $vpmDetail += "; producer evidence is unreadable: $($_.Exception.Message)" }
    } elseif ($vpmRun.code -eq 0) { $vpmDetail += '; producer evidence is missing' }
    $vpmStatus = if ($vpmValid) { 'PASS' } elseif ($vpmRun.code -eq 127 -or -not $vrcGetPath) { 'UNVERIFIED' } else { 'FAIL' }
    $vpmEvidencePath = Add-EvidenceRecord 'vpm-u1' $vpmStatus "task $($config.gates.vpmTask)" $vpmRun.code $vpmRun.log $vpmProducer ([ordered]@{ producer = $vpmEvidence })
    Add-Step 'task-vpm-check' $vpmStatus $vpmDetail $true $taskPath $vpmRun.code $vpmRun.log $vpmEvidencePath
    if ($vpmStatus -ne 'PASS') { Stop-Verification 'VPM verification did not pass; downstream Unity and Blender gates were not started' $(if ($vpmStatus -eq 'FAIL') { 'FAIL' } else { 'UNVERIFIED' }) }
    $stable = Assert-SnapshotStable
    if (-not $stable.stable) { Add-Step 'snapshot-after-vpm' 'FAIL' $stable.detail $true 'git' 1; Stop-Verification 'VPM gate changed the exact input snapshot' 'FAIL' }
    Add-Step 'snapshot-after-vpm' 'PASS' $stable.detail $true 'git' 0
  }
  if ($config.gates.repositoryTask) {
    $checkRun = Invoke-Logged 'task-check' $taskPath @($config.gates.repositoryTask) $worktree
    $checkStatus = if ($checkRun.code -eq 0) { 'PASS' } elseif ($checkRun.code -eq 127) { 'UNVERIFIED' } else { 'FAIL' }
    $checkEvidencePath = Add-EvidenceRecord 'task-check' $checkStatus "task $($config.gates.repositoryTask)" $checkRun.code $checkRun.log $null ([ordered]@{})
    Add-Step 'task-check' $checkStatus "exit=$($checkRun.code); log=$($checkRun.log)" $true $taskPath $checkRun.code $checkRun.log $checkEvidencePath
    if ($checkStatus -ne 'PASS') { Stop-Verification 'Repository task check failed; downstream runtime gates were not started' $(if ($checkStatus -eq 'FAIL') { 'FAIL' } else { 'UNVERIFIED' }) }
    $stable = Assert-SnapshotStable
    if (-not $stable.stable) { Add-Step 'snapshot-after-check' 'FAIL' $stable.detail $true 'git' 1; Stop-Verification 'Repository task check changed the exact input snapshot' 'FAIL' }
    Add-Step 'snapshot-after-check' 'PASS' $stable.detail $true 'git' 0
  }
  if ($surface.blender) {
    if ($NoBlender) { Stop-Verification 'Required Blender verification was disabled' 'UNVERIFIED' }
    $blenderScript = Join-Path $worktree ([string]$config.gates.blenderGeometrySmoke)
    $blenderRun = Invoke-Logged 'blender-geometry-smoke' $blenderPath @('-b', '--python-exit-code', '1', '--python', $blenderScript) $worktree
    $blenderErrors = @(Test-BlenderLogForErrors $blenderRun.log)
    $blenderStatus = if ($blenderRun.code -eq 0 -and $blenderErrors.Count -eq 0) { 'PASS' } elseif ($blenderRun.code -eq 127) { 'UNVERIFIED' } else { 'FAIL' }
    $blenderEvidencePath = Add-EvidenceRecord 'blender-geometry-smoke' $blenderStatus "blender -b --python-exit-code 1 --python $blenderScript" $blenderRun.code $blenderRun.log $null ([ordered]@{ version = [string]$blenderVersionProbe['version']; errors = $blenderErrors })
    Add-Step 'blender-geometry-smoke' $blenderStatus "version=$([string]$blenderVersionProbe['version']); exit=$($blenderRun.code); errors=$($blenderErrors.Count)" $true $blenderPath $blenderRun.code $blenderRun.log $blenderEvidencePath
    if ($blenderStatus -ne 'PASS') { Stop-Verification 'Blender batchmode verification failed' $(if ($blenderStatus -eq 'FAIL') { 'FAIL' } else { 'UNVERIFIED' }) }
  } else { Add-Step 'blender-geometry-smoke' 'PASS' 'not selected by request manifest surface detection' $false $null 0 }
  if ($surface.unity) {
    if ($NoUnity) { Stop-Verification 'Required Unity verification was disabled' 'UNVERIFIED' }
    $compileLog = Join-Path $state 'logs\unity-compile-editor.log'
    $compileRun = Invoke-Logged 'unity-compile' $unityPath @('-batchmode', '-quit', '-nographics', '-projectPath', $worktree, '-logFile', $compileLog) $worktree @{ UNITY_MCP_AUTOCONSENT = '1' }
    $unityErrors = @(Test-UnityLogForErrors @($compileLog, $compileRun.log))
    $compileStatus = if ($compileRun.code -eq 0 -and $unityErrors.Count -eq 0) { 'PASS' } elseif ($compileRun.code -eq 127) { 'UNVERIFIED' } else { 'FAIL' }
    $compileEvidencePath = Add-EvidenceRecord 'unity-compile' $compileStatus "Unity.exe -batchmode -quit -nographics -projectPath $worktree" $compileRun.code $compileLog $null ([ordered]@{ unityVersion = $expectedUnityVersion; errors = $unityErrors })
    Add-Step 'unity-batchmode-compile' $compileStatus "version=$expectedUnityVersion; exit=$($compileRun.code); errors=$($unityErrors.Count)" $true $unityPath $compileRun.code $compileLog $compileEvidencePath
    if ($compileStatus -ne 'PASS') { Stop-Verification 'Unity batchmode compilation failed' $(if ($compileStatus -eq 'FAIL') { 'FAIL' } else { 'UNVERIFIED' }) }
    $runner = Join-Path $worktree 'scripts\run-unity-tests.mjs'
    if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) { Stop-Verification 'Unity test producer is missing: scripts/run-unity-tests.mjs' 'UNVERIFIED' }
    $runnerRun = Invoke-Logged 'unity-tests' $nodePath @($runner) $worktree @{ UNITY_EXE = $unityPath; VRMINE_EVIDENCE_DIR = (Join-Path $state 'evidence'); UNITY_MCP_AUTOCONSENT = '1' }
    $runnerEvidence = Join-Path $state 'evidence\unity-tests-evidence.json'
    $runnerValid = $false
    if ($runnerRun.code -eq 0 -and (Test-Path -LiteralPath $runnerEvidence -PathType Leaf)) {
      try {
        $testEvidence = Read-JsonFile $runnerEvidence
        $badResults = @($testEvidence.results | Where-Object { $_.exitCode -ne 0 -or -not (Test-Path -LiteralPath ([string]$_.resultPath) -PathType Leaf) })
        $runnerValid = ([string]$testEvidence.status -eq 'PASS' -and @($testEvidence.results).Count -eq 2 -and $badResults.Count -eq 0)
      } catch { $runnerValid = $false }
    }
    $testStatus = if ($runnerValid) { 'PASS' } elseif ($runnerRun.code -eq 127) { 'UNVERIFIED' } else { 'FAIL' }
    $testProducer = if (Test-Path -LiteralPath $runnerEvidence -PathType Leaf) { Read-JsonFile $runnerEvidence } else { $null }
    $testEvidencePath = Add-EvidenceRecord 'unity-tests' $testStatus "node $runner" $runnerRun.code $runnerRun.log $runnerEvidence ([ordered]@{ producer = $testProducer })
    Add-Step 'unity-editmode-playmode-tests' $testStatus "exit=$($runnerRun.code); producer=$runnerEvidence" $true $nodePath $runnerRun.code $runnerRun.log $testEvidencePath
    if ($testStatus -ne 'PASS') { Stop-Verification 'Unity EditMode/PlayMode test evidence did not pass' $(if ($testStatus -eq 'FAIL') { 'FAIL' } else { 'UNVERIFIED' }) }
  } else {
    Add-Step 'unity-batchmode-compile' 'PASS' 'not selected by request manifest surface detection' $false $null 0
    Add-Step 'unity-editmode-playmode-tests' 'PASS' 'not selected by request manifest surface detection' $false $null 0
  }
  $sourceStatusAfter = (Run-Git $source @('status', '--porcelain=v1', '--untracked-files=all')).output
  if ((Hash-Text $sourceStatusAfter) -ne $sourceStatusHashBefore -or (Get-SourceContentFingerprint) -ne $sourceContentHashBefore) { Add-Step 'development-checkout-protection-final' 'UNVERIFIED' 'development checkout changed during verification' $true 'git' 2; Stop-Verification 'Development checkout was not preserved unchanged' 'UNVERIFIED' }
  Add-Step 'development-checkout-protection-final' 'PASS' 'development checkout status fingerprint unchanged' $true 'git' 0
  $rootCause = 'none'
  $fatalStatus = 'PASS'
} catch {
  if (-not $rootCause) { $rootCause = $_.Exception.Message }
  $rootStepExists = @($steps | Where-Object { $_.name -eq 'root-cause' }).Count -gt 0
  if (-not $rootStepExists) { Add-Step 'root-cause' $fatalStatus $rootCause $true $null 1 }
} finally {
  if ($state) {
    try {
      $finishedAt = [DateTime]::UtcNow
      $badRequired = @($steps | Where-Object { $_.required -and $_.status -eq 'FAIL' })
      $unknownRequired = @($steps | Where-Object { $_.required -and $_.status -eq 'UNVERIFIED' })
      $overall = if ($fatalStatus -eq 'FAIL' -or $badRequired.Count -gt 0) { 'FAIL' } elseif ($fatalStatus -eq 'UNVERIFIED' -or $unknownRequired.Count -gt 0) { 'UNVERIFIED' } else { 'PASS' }
      if (-not $rootCause) {
        $firstBad = @($steps | Where-Object { $_.required -and $_.status -ne 'PASS' } | Select-Object -First 1)
        $rootCause = if ($firstBad) { "$($firstBad.name): $($firstBad.detail)" } else { 'none' }
      }
      $manifest = [ordered]@{ schemaVersion = 1; verifier = 'vrmine'; status = $overall; request = $config; baseCommit = $baseCommit; sourceHead = $sourceHead; snapshotTree = $snapshotTree; inputId = $inputId; changedFiles = @($changedFiles); sourceCheckout = $source; verificationRoot = $verificationRoot; verificationCheckout = $worktree; runId = $runId; runMode = $runMode; quarantinedResources = @($quarantinedResources.ToArray()); sourceStatusBefore = $sourceStatusBefore; surface = $surface; tools = $tools; startedAt = $startedAt.ToString('o'); finishedAt = $finishedAt.ToString('o'); steps = @($steps.ToArray()) }
      Write-JsonFile (Join-Path $state 'manifest.json') $manifest
      $summary = [ordered]@{ schemaVersion = 1; verifier = 'vrmine'; status = $overall; rootCause = $rootCause; baseCommit = $baseCommit; snapshotTree = $snapshotTree; inputId = $inputId; sourceCheckout = $source; verificationRoot = $verificationRoot; verificationCheckout = $worktree; runId = $runId; runMode = $runMode; quarantinedResources = @($quarantinedResources.ToArray()); developmentCheckoutUntouched = $true; evidence = @($evidenceRecords | ForEach-Object { $_.path }); startedAt = $startedAt.ToString('o'); finishedAt = $finishedAt.ToString('o'); steps = @($steps.ToArray()) }
      Write-JsonFile (Join-Path $state 'summary.json') $summary
      $hashes = [ordered]@{}
      foreach ($file in (Get-ChildItem -LiteralPath $state -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'hashes.json' })) {
        $relative = $file.FullName.Substring($state.Length).TrimStart('\').Replace('\', '/')
        $hashes[$relative] = Hash-File $file.FullName
      }
      Write-JsonFile (Join-Path $state 'hashes.json') ([ordered]@{ schemaVersion = 1; baseCommit = $baseCommit; snapshotTree = $snapshotTree; inputId = $inputId; files = $hashes })
      $summary | ConvertTo-Json -Depth 50
      if ($overall -eq 'PASS') { exit 0 }
      if ($overall -eq 'FAIL') { exit 1 }
      exit 2
    } catch {
      Write-Error "Could not write vrmine evidence: $($_.Exception.Message)"
      exit 2
    }
  } else {
    Write-Error (if ($rootCause) { $rootCause } else { 'vrmine verification could not initialize' })
    exit 2
  }
}
