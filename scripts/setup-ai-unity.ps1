param()
$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

node scripts/verify-ai-unity-setup.mjs

$nodeVersion = (& node -p "process.versions.node").Trim()
$parts = $nodeVersion.Split('.') | ForEach-Object { [int]$_ }
if ($parts[0] -lt 20 -or ($parts[0] -eq 20 -and $parts[1] -lt 19)) {
    throw "TunaSync requires Node 20.19+; found $nodeVersion"
}

if (-not (Get-Command uloop -ErrorAction SilentlyContinue)) {
    $installer = Join-Path $env:TEMP "uloop-v3.5.0-install.ps1"
    Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/hatayama/unity-cli-loop/v3.5.0/scripts/install.ps1" -OutFile $installer
    $actual = (Get-FileHash -Algorithm SHA256 $installer).Hash.ToLowerInvariant()
    $expected = "e97de8d921af3115bfa00544ec69da6136b9cb0e271966f1073777872cd69838"
    if ($actual -ne $expected) { throw "uloop installer SHA-256 mismatch: $actual" }
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer
    Remove-Item -Force $installer
}

& uloop -v
& npx -y tunasync-unity-mcp@2.6.8 doctor --json
Write-Host "AI Unity bridge is installed. Open Unity once and approve the TunaSync per-project consent dialog before live MCP use."
