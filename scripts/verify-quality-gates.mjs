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
  'no_unresolved_blocking_review',
];
for (const item of expectedMerge) if (!mergeRequired.has(item)) fail(`PR merge gate missing ${item}`);

const releaseOnly = [
  'unity_editor_execution',
  'sdk_builder_validation',
  'actual_vrchat_client',
  'multiplayer_runtime_evidence',
  'release_performance_measurement',
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

const agentsPath = path.join(root, 'AGENTS.md');
const readmePath = path.join(root, 'README.md');
const taskfilePath = path.join(root, 'Taskfile.yml');
for (const requiredPath of [agentsPath, readmePath, taskfilePath]) {
  if (!fs.existsSync(requiredPath)) fail(`required repository guide is missing: ${path.relative(root, requiredPath)}`);
}

const agents = fs.readFileSync(agentsPath, 'utf8');
for (const requiredText of ['このファイルを、VRMineで作業するエージェント向けルールの正準とする。', 'CI/CDは必須', 'task check', 'U1', 'U5']) {
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
const readmeLines = new Set(fs.readFileSync(readmePath, 'utf8').split(/\r?\n/).map((line) => line.trim()));
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
  publicPagesPlainTextUrls: publicPages,
}, null, 2));
