import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const root = process.cwd();
const policyPath = path.join(root, 'config', 'verification-worktree-policy.json');
const allocatorPath = path.join(root, 'scripts', 'new-verification-worktree.ps1');
const ignorePath = path.join(root, '.gitignore');

const fail = (message) => {
  console.error(`verification-worktree-policy: FAIL: ${message}`);
  process.exit(1);
};

const policy = JSON.parse(fs.readFileSync(policyPath, 'utf8'));
const allocator = fs.readFileSync(allocatorPath, 'utf8');
const gitignore = fs.readFileSync(ignorePath, 'utf8');

if (policy.schema_version !== 1) fail('schema_version must be 1');
if (policy.normal_run?.delete_existing_run !== false) fail('normal runs must not delete existing runs');
if (policy.normal_run?.reuse_failed_run !== false) fail('failed runs must not be reused');
if (policy.normal_run?.reuse_partial_run !== false) fail('partial runs must not be reused');
if (policy.normal_run?.collision_strategy !== 'allocate-new-run') fail('collisions must allocate a new run');
if (policy.normal_run?.retry_strategy !== 'allocate-new-run') fail('retries must allocate a new run');
if (policy.normal_run?.stale_resource_strategy !== 'record-and-skip') fail('stale resources must be recorded and skipped');
if (policy.destructive_recovery?.allowed !== false) fail('destructive recovery must remain disabled');
if (policy.cleanup?.owner !== 'maintenance-job') fail('cleanup must be isolated to a maintenance job');
if (policy.cleanup?.normal_run_behavior !== 'defer') fail('normal runs must defer cleanup');
if (policy.approval_avoidance?.destructive_cleanup_must_not_block_normal_run !== true) fail('destructive cleanup must not block normal runs');
if (policy.approval_avoidance?.approval_required_for_retry !== false) fail('retry must not require approval');

const forbidden = [
  /git\s+worktree\s+remove/i,
  /git\s+clean\b/i,
  /git\s+reset\s+--hard/i,
  /Remove-Item\b/i,
  /rm\s+-rf\b/i,
];
for (const pattern of forbidden) {
  if (pattern.test(allocator)) fail(`allocator contains destructive recovery pattern: ${pattern}`);
}

if (!/\[Guid\]::NewGuid\(\)/.test(allocator)) fail('allocator must include a nonce to prevent path reuse');
if (!/worktree\", \"add\", \"--detach\"/.test(allocator)) fail('allocator must create a detached worktree');
if (!gitignore.split(/\r?\n/).includes('/.vrmine-verify/')) fail('.gitignore must ignore repo-local .vrmine-verify fallback state');

console.log('verification-worktree-policy: PASS');
