import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const policyPath = path.join(root, 'config', 'quality-gates.json');

function fail(message) {
  console.error(`Quality gates FAIL: ${message}`);
  process.exit(1);
}

const policy = JSON.parse(fs.readFileSync(policyPath, 'utf8'));
if (policy.schema_version !== 1) fail('schema_version must be 1');
if (policy.policy !== 'merge_and_release_are_independent_gates') fail('policy identifier changed unexpectedly');

const mergeRequired = new Set(policy.pr_merge_gate?.required ?? []);
const mergeNotRequired = new Set(policy.pr_merge_gate?.not_required ?? []);
const releaseCandidateRequired = new Set(policy.release_candidate_gate?.required ?? []);
const releaseRequired = new Set(policy.product_release_gate?.required ?? []);

const expectedMerge = [
  'scope_complete',
  'static_contracts_pass',
  'repository_u1_pass',
  'changed_surface_tests_pass',
  'pr_mergeable',
];
for (const item of expectedMerge) if (!mergeRequired.has(item)) fail(`PR merge gate missing ${item}`);

const releaseOnly = [
  'unity_editor_execution',
  'sdk_builder_validation',
  'actual_vrchat_client',
  'multiplayer_runtime_evidence',
  'release_performance_measurement',
  'visual_review',
  'manual_approval',
  'draft_state',
  'unresolved_review',
];
for (const item of releaseOnly) {
  if (!mergeNotRequired.has(item)) fail(`PR merge gate must explicitly exclude release-only evidence ${item}`);
  if (mergeRequired.has(item)) fail(`release-only evidence appears in PR merge required set: ${item}`);
}

for (const item of ['exact_main_commit', 'exact_unity_toolchain', 'unity_compile_pass', 'canonical_scene_integrity_pass', 'sdk_builder_blocking_validation_zero']) {
  if (!releaseCandidateRequired.has(item)) fail(`release candidate gate missing ${item}`);
}

for (const item of ['release_candidate_gate_pass', 'actual_client_single_player_clear', 'reset_and_replay', 'two_client_public_state_sync', 'late_join_reconstruction', 'owner_transition_recovery', 'runtime_evidence_tied_to_exact_commit']) {
  if (!releaseRequired.has(item)) fail(`product release gate missing ${item}`);
}

const rules = policy.state_rules ?? {};
if (rules.implementation_issue_may_close_after_merge_gate !== true) fail('implementation issues must be closable after merge gate');
if (rules.release_issue_may_close_without_product_release_gate !== false) fail('release issue must stay open until product release gate passes');
if (rules.epic_may_claim_product_complete_without_product_release_gate !== false) fail('epic must not claim product completion before release gate');
if (rules.lower_evidence_may_be_promoted_to_actual_client_pass !== false) fail('lower evidence must never be promoted to actual-client PASS');
if (rules.generated_asset_visual_review_may_block_merge !== false) fail('generated asset visual review must never block merge');
if (rules.generated_asset_manual_approval_may_block_merge !== false) fail('generated asset manual approval must never block merge');
if (rules.generated_asset_flow_continues_to_merge_after_generation !== true) fail('generated asset flow must continue to merge after generation');

const agentsPath = path.join(root, 'AGENTS.md');
const readmePath = path.join(root, 'README.md');
const taskfilePath = path.join(root, 'Taskfile.yml');
for (const requiredPath of [agentsPath, readmePath, taskfilePath]) {
  if (!fs.existsSync(requiredPath)) fail(`required repository guide is missing: ${path.relative(root, requiredPath)}`);
}

const agents = fs.readFileSync(agentsPath, 'utf8');
for (const requiredText of ['このファイルを、VRMineで作業するエージェント向けルールの正準とする。', 'CI/CDは必須', 'task check', 'U1', 'U5', 'appearance is evidence for the user, not a merge gate', 'hero, front, rear, left, right, and top']) {
  if (!agents.includes(requiredText)) fail(`AGENTS.md is missing required rule: ${requiredText}`);
}

const forbiddenRepoLocalSkillPaths = [
  'agr.toml',
  'agr.lock',
  'skills/unity-vrc-verification',
];
for (const relativePath of forbiddenRepoLocalSkillPaths) {
  if (fs.existsSync(path.join(root, relativePath))) fail(`retired repo-local skill path returned: ${relativePath}`);
}

const taskfile = fs.readFileSync(taskfilePath, 'utf8');
for (const retiredCommand of ['skills:init:', 'skills:add:', 'skills:sync:', 'uvx agr']) {
  if (taskfile.includes(retiredCommand)) fail(`retired skill command returned to Taskfile.yml: ${retiredCommand}`);
}

const generatedWorkflowPath = path.join(root, '.github', 'workflows', 'retro-cafe.yml');
if (!fs.existsSync(generatedWorkflowPath)) fail('generated-asset automatic merge workflow is missing');
const generatedWorkflow = fs.readFileSync(generatedWorkflowPath, 'utf8');
for (const requiredText of [
  'gh pr merge',
  'gh pr comment',
  'gh issue comment',
  'gh pr edit',
  'gh issue edit',
  'view-hero.png',
  'view-front.png',
  'view-rear.png',
  'view-left.png',
  'view-right.png',
  'view-top.png',
]) {
  if (!generatedWorkflow.includes(requiredText)) fail(`generated-asset workflow is missing required automatic integration behavior: ${requiredText}`);
}

const publicPages = [
  'https://kafka2306.github.io/vrmine/',
  'https://kafka2306.github.io/vrmine/games/perspective-cage/',
  'https://kafka2306.github.io/vrmine/games/stich-meister/',
  'https://kafka2306.github.io/vrmine/games/answer-impostor/',
  'https://kafka2306.github.io/vrmine/games/abyss-invasion/',
  'https://kafka2306.github.io/vrmine/3dgs/',
  'https://kafka2306.github.io/vrmine/organizers/',
  'https://kafka2306.github.io/vrmine/events/demo/',
];
const readme = fs.readFileSync(readmePath, 'utf8');
const readmeRows = readme.split(/\r?\n/);
const firstNonEmptyReadmeLine = readmeRows.find((line) => line.trim().length > 0)?.trim();
if (firstNonEmptyReadmeLine !== publicPages[0]) {
  fail(`README.md first non-empty line must be canonical production URL: ${publicPages[0]}`);
}
const readmeLines = new Set(readmeRows.map((line) => line.trim()));
for (const publicUrl of publicPages) {
  if (!readmeLines.has(publicUrl)) fail(`README.md must contain the public Pages URL as its own plain-text line: ${publicUrl}`);
}

console.log(JSON.stringify({
  status: 'PASS',
  policy: policy.policy,
  mergeRequired: [...mergeRequired],
  mergeExplicitlyDoesNotRequire: [...mergeNotRequired],
  releaseCandidateRequired: [...releaseCandidateRequired],
  productReleaseRequired: [...releaseRequired],
  repositoryGuide: 'AGENTS.md',
  retiredRepoLocalSkillPathsAbsent: true,
  canonicalProductionUrlFirstInReadme: true,
  publicPagesPlainTextUrls: publicPages,
}, null, 2));
