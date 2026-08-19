param(
    [ValidateSet('registered', 'final', 'sdk', 'performance')]
    [string]$Mode = 'registered',
    [string]$UnityPath = $env:UNITY_EXE
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectVersionPath = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path $projectVersionPath)) {
    throw "ProjectVersion.txt not found: $projectVersionPath"
}

$versionLine = Get-Content $projectVersionPath | Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } | Select-Object -First 1
if (-not $versionLine) {
    throw 'Could not read m_EditorVersion from ProjectVersion.txt'
}
$unityVersion = ($versionLine -replace '^m_EditorVersion:\s*', '').Trim()
if ($unityVersion -ne '2022.3.22f1') {
    throw "Unsupported Unity version for this VRChat project: $unityVersion (expected 2022.3.22f1)"
}

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $defaultHubPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$unityVersion\Editor\Unity.exe"
    if (Test-Path $defaultHubPath) {
        $UnityPath = $defaultHubPath
    }
}
if ([string]::IsNullOrWhiteSpace($UnityPath) -or -not (Test-Path $UnityPath)) {
    throw "Unity $unityVersion was not found. Pass -UnityPath <Unity.exe> or set UNITY_EXE."
}

$methods = @{
    registered  = 'GaussianExhibitionVerification.VerifyRegisteredBatch'
    final       = 'GaussianExhibitionVerification.VerifyBatch'
    sdk         = 'GaussianExhibitionVerification.VerifySdkWorldBuilderBatch'
    performance = 'GaussianExhibitionVerification.VerifyPerformanceBatch'
}
$method = $methods[$Mode]

$evidenceDir = Join-Path $projectRoot 'Library\VRMine'
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$timestamp = Get-Date -Format 'yyyyMMddTHHmmss'
$logPath = Join-Path $evidenceDir "unity-$Mode-$timestamp.log"

Write-Host "Unity: $UnityPath"
Write-Host "Project: $projectRoot"
Write-Host "Mode: $Mode"
Write-Host "Method: $method"
Write-Host "Log: $logPath"

$unityArgs = @(
    '-batchmode',
    '-quit',
    '-projectPath', $projectRoot,
    '-executeMethod', $method,
    '-logFile', $logPath
)
& $UnityPath @unityArgs
$exitCode = $LASTEXITCODE

if (-not (Test-Path $logPath)) {
    throw "Unity did not create the expected log file: $logPath"
}

$log = Get-Content $logPath -Raw
$knownRegressionPatterns = @(
    'IndexOutOfRangeException: Index was outside the bounds of the array',
    'No PipelineManager found in scene',
    "Problem detected while opening the Scene file"
)
foreach ($pattern in $knownRegressionPatterns) {
    if ($log.Contains($pattern)) {
        throw "Known Unity/VRChat regression reappeared: $pattern. See $logPath"
    }
}

if ($exitCode -ne 0) {
    throw "Unity verification failed with exit code $exitCode. See $logPath"
}

switch ($Mode) {
    'registered' {
        $evidencePath = Join-Path $evidenceDir 'gaussian-u2-evidence.json'
        if (-not (Test-Path $evidencePath)) {
            throw "Registered verification exited 0 but evidence is missing: $evidencePath"
        }
        Write-Host "PASS: registered Unity verification. Evidence: $evidencePath"
    }
    'performance' {
        $evidencePath = Join-Path $evidenceDir 'gaussian-performance-evidence.json'
        if (-not (Test-Path $evidencePath)) {
            throw "Performance verification exited 0 but evidence is missing: $evidencePath"
        }
        Write-Host "PASS: performance evidence collection. Evidence: $evidencePath"
    }
    'sdk' {
        if (-not $log.Contains('Gaussian SDK world builder validation completed without exception')) {
            throw "SDK verification exited 0 without the expected completion marker. See $logPath"
        }
        Write-Host 'PASS: SDK world builder validation path completed without exception.'
    }
    'final' {
        if (-not $log.Contains('Gaussian exhibition verification PASS')) {
            throw "Final verification exited 0 without the expected PASS marker. See $logPath"
        }
        Write-Host 'PASS: strict final Gaussian exhibition verification.'
    }
}
