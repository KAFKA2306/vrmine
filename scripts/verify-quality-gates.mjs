import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function fail(message) {
  console.error(`Quality gates FAIL: ${message}`);
  process.exit(1);
}

function expectExactArray(actual, expected, label) {
  if (!Array.isArray(actual)) fail(`${label} must be an array`);
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    fail(`${label} changed: ${JSON.stringify(actual)}`);
  }
}

const policyPath = path.join(root, 'config', 'quality-gates.json');
const policy = JSON.parse(fs.readFileSync(policyPath, 'utf8'));

if (policy.schema_version !== 1) fail('schema_version must be 1');
if (policy.policy !== 'merge_and_release_are_independent_gates') fail('unexpected policy');

const expectedMergeRequired = [
  'scope_complete',
  'static_contracts_pass',
  'repository_u1_pass',
  'changed_surface_tests_pass',
  'pr_mergeable',
];
const expectedReleaseCandidateRequired = [
  'exact_main_commit',
  'exact_unity_toolchain',
  'unity_compile_pass',
  'canonical_scene_integrity_pass',
  'sdk_builder_blocking_validation_zero',
];
const expectedProductReleaseRequired = [
  'release_candidate_gate_pass',
  'actual_client_single_player_clear',
  'wrong_input_recovery',
  'reset_and_replay',
  'two_client_public_state_sync',
  'late_join_reconstruction',
  'owner_transition_recovery',
  'playthrough_duration_recorded',
  'runtime_evidence_tied_to_exact_commit',
];
expectExactArray(policy.pr_merge_gate?.required, expectedMergeRequired, 'PR merge required gates');
expectExactArray(
  policy.release_candidate_gate?.required,
  expectedReleaseCandidateRequired,
  'release candidate required gates',
);
expectExactArray(
  policy.product_release_gate?.required,
  expectedProductReleaseRequired,
  'product release required gates',
);

const mergeRequired = new Set(policy.pr_merge_gate?.required ?? []);
const mergeNotRequired = new Set(policy.pr_merge_gate?.not_required ?? []);
for (const item of mergeNotRequired) {
  if (mergeRequired.has(item)) fail(`PR merge gate also requires excluded item: ${item}`);
}

const authoring = policy.authoring_evidence_policy ?? {};
expectExactArray(authoring.states, ['PASS', 'FAIL', 'UNVERIFIED'], 'authoring evidence states');
const preferredExecutionOrder = [
  'repository_automation',
  'version_compatible_mcp',
  'computer_use',
];
expectExactArray(
  authoring.preferred_execution_order,
  preferredExecutionOrder,
  'authoring execution order',
);

const adapters = authoring.adapters ?? {};
for (const adapterId of preferredExecutionOrder) {
  if (!adapters[adapterId]) fail(`missing authoring adapter: ${adapterId}`);
  if (adapters[adapterId].version_compatibility_required !== true) {
    fail(`${adapterId} must require version compatibility`);
  }
}
if (adapters.repository_automation.machine_readable !== true) {
  fail('repository automation must be machine-readable');
}
if (adapters.repository_automation.may_produce_stage_evidence !== true) {
  fail('repository automation must be allowed to produce stage evidence');
}
if (adapters.version_compatible_mcp.machine_readable !== true) {
  fail('MCP control must be machine-readable');
}
if (adapters.version_compatible_mcp.may_produce_stage_evidence !== true) {
  fail('version-compatible MCP must be allowed to produce stage evidence');
}
if (adapters.computer_use.role !== 'gui_fallback_only') {
  fail('Computer Use must remain GUI fallback only');
}
if (adapters.computer_use.machine_readable !== false) {
  fail('Computer Use must not be represented as machine-readable evidence');
}
if (adapters.computer_use.may_produce_stage_evidence !== false) {
  fail('Computer Use must not promote stage evidence');
}

const evidenceRules = authoring.rules ?? {};
for (const rule of [
  'missing_producer_is_unverified',
  'missing_measurement_is_unverified',
  'tool_success_is_not_runtime_success',
  'computer_use_observation_is_not_release_evidence',
  'evidence_must_name_exact_revision',
  'authoring_adapter_success_may_not_promote_release_gate',
]) {
  if (evidenceRules[rule] !== true) fail(`authoring evidence rule must be true: ${rule}`);
}

const knowledgePath = path.join(root, 'config', 'agent-knowledge.json');
const knowledge = JSON.parse(fs.readFileSync(knowledgePath, 'utf8'));
const toolSelection = knowledge.authoring_tool_selection ?? {};
expectExactArray(
  toolSelection.preferred_order,
  preferredExecutionOrder,
  'agent authoring tool order',
);
const toolRules = toolSelection.rules ?? {};
for (const rule of [
  'prefer_machine_readable_interfaces',
  'version_sensitive_adapter_requires_compatibility_check',
  'computer_use_requires_gui_only_reason',
  'computer_use_may_not_promote_release_evidence',
  'orchestrator_model_claim_is_not_evidence',
]) {
  if (toolRules[rule] !== true) fail(`agent tool-selection rule must be true: ${rule}`);
}

const rules = policy.state_rules ?? {};
if (rules.generated_asset_visual_review_may_block_merge !== false) fail('visual review must not block generated assets');
if (rules.generated_asset_manual_approval_may_block_merge !== false) fail('manual approval must not block generated assets');
if (rules.generated_asset_flow_continues_to_merge_after_generation !== true) fail('generated asset flow must continue to merge');
if (rules.lower_evidence_may_be_promoted_to_actual_client_pass !== false) {
  fail('lower evidence must never be promoted to actual-client PASS');
}

for (const relativePath of ['README.md', 'AGENTS.md', 'Taskfile.yml', 'config/quality-gates.json']) {
  if (!fs.existsSync(path.join(root, relativePath))) fail(`missing canonical file: ${relativePath}`);
}
if (fs.existsSync(path.join(root, 'docs'))) fail('docs/ must not duplicate canonical documentation');

const canonicalProductionUrl = 'https://kafka2306.github.io/vrmine/';
const catalogUrl = 'https://kafka2306.github.io/vrmine/io/';
const readmeRows = fs.readFileSync(path.join(root, 'README.md'), 'utf8').split(/\r?\n/);
const firstNonEmptyLine = readmeRows.find((line) => line.trim())?.trim();
if (firstNonEmptyLine !== canonicalProductionUrl) fail('README.md must start with the canonical production URL');
const readmeLines = new Set(readmeRows.map((line) => line.trim()));
for (const url of [canonicalProductionUrl, catalogUrl]) {
  if (!readmeLines.has(url)) fail(`README.md missing canonical public URL: ${url}`);
}

const publicHomePath = path.join(root, 'pages', 'index.html');
if (!fs.existsSync(publicHomePath)) fail('missing public Home: pages/index.html');
const publicHome = fs.readFileSync(publicHomePath, 'utf8');

const allowedHomeSections = new Set(['games', 'assets']);
const homeSections = [...publicHome.matchAll(/<section\b[^>]*\bid="([^"]+)"/g)].map((match) => match[1]);
if (homeSections.length !== allowedHomeSections.size) {
  fail(`public Home section count must be ${allowedHomeSections.size}: ${homeSections.join(', ')}`);
}
for (const section of homeSections) {
  if (!allowedHomeSections.has(section)) fail(`public Home contains non-product section: ${section}`);
}
for (const section of allowedHomeSections) {
  if (!homeSections.includes(section)) fail(`public Home missing required section: ${section}`);
}

if (/https:\/\/github\.com\/KAFKA2306\/vrmine(?:[/"?#]|$)/.test(publicHome)) {
  fail('public Home must not link to repository, Issue, or PR surfaces');
}

const forbiddenHomeFragments = [
  'VR development',
  '実装途中も、成果として見える',
  'Platform',
  '増やしやすく、壊れにくい',
  'release gate',
  'ClientSim',
  'workstream',
  'mainへ統合済み',
];
for (const fragment of forbiddenHomeFragments) {
  if (publicHome.includes(fragment)) fail(`public Home contains engineering-status prose: ${fragment}`);
}

console.log(JSON.stringify({
  status: 'PASS',
  policy: policy.policy,
  authoringEvidenceStates: authoring.states,
  authoringExecutionOrder: authoring.preferred_execution_order,
  canonicalProductionUrl,
  catalogUrl,
  publicHomeSections: homeSections,
  docsDirectoryAbsent: true,
}, null, 2));
