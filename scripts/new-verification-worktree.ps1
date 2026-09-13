param(
    [Parameter(Mandatory = $false)]
    [string]$Revision = "HEAD",

    [Parameter(Mandatory = $false)]
    [string]$StateRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-GitText {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }
    return ($output -join "`n").Trim()
}

$repoRoot = Invoke-GitText @("rev-parse", "--show-toplevel")
$repoRoot = [System.IO.Path]::GetFullPath($repoRoot)

if ([string]::IsNullOrWhiteSpace($StateRoot)) {
    $StateRoot = Join-Path (Split-Path $repoRoot -Parent) ".vrmine-verify"
}
$StateRoot = [System.IO.Path]::GetFullPath($StateRoot)

$revisionSha = Invoke-GitText @("rev-parse", "$Revision^{commit}")
$shortSha = $revisionSha.Substring(0, 12)
$runId = "{0}-{1}-{2}" -f ([DateTime]::UtcNow.ToString("yyyyMMddTHHmmssfffZ")), $shortSha, ([Guid]::NewGuid().ToString("N").Substring(0, 8))
$runRoot = Join-Path $StateRoot "runs"
$evidenceRoot = Join-Path $StateRoot "evidence"
$worktreePath = Join-Path $runRoot $runId
$evidencePath = Join-Path $evidenceRoot $runId

[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($evidencePath) | Out-Null

$manifestPath = Join-Path $evidencePath "allocation.json"
$manifest = [ordered]@{
    schema_version = 1
    run_id = $runId
    repository = $repoRoot
    revision = $revisionSha
    worktree = $worktreePath
    evidence = $evidencePath
    state = "ALLOCATING"
    created_at_utc = [DateTime]::UtcNow.ToString("o")
    recovery_policy = "allocate-new-run"
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

try {
    Invoke-GitText @("worktree", "add", "--detach", $worktreePath, $revisionSha) | Out-Null
    $manifest.state = "ALLOCATED"
    $manifest.completed_at_utc = [DateTime]::UtcNow.ToString("o")
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
} catch {
    $manifest.state = "UNVERIFIED"
    $manifest.failure = $_.Exception.Message
    $manifest.completed_at_utc = [DateTime]::UtcNow.ToString("o")
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    throw
}

[ordered]@{
    run_id = $runId
    revision = $revisionSha
    worktree = $worktreePath
    evidence = $evidencePath
    manifest = $manifestPath
} | ConvertTo-Json -Compress
