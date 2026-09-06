import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function fail(message) {
  console.error(`Quality gates FAIL: ${message}`);
  process.exit(1);
}

const policyPath = path.join(root, 'config', 'quality-gates.json');
const policy = JSON.parse(fs.readFileSync(policyPath, 'utf8'));

if (policy.schema_version !== 1) fail('schema_version must be 1');
if (policy.policy !== 'merge_and_release_are_independent_gates') fail('unexpected policy');

const mergeRequired = new Set(policy.pr_merge_gate?.required ?? []);
const mergeNotRequired = new Set(policy.pr_merge_gate?.not_required ?? []);
for (const item of ['scope_complete', 'static_contracts_pass', 'repository_u1_pass', 'changed_surface_tests_pass', 'pr_mergeable']) {
  if (!mergeRequired.has(item)) fail(`PR merge gate missing ${item}`);
}
for (const item of mergeNotRequired) {
  if (mergeRequired.has(item)) fail(`PR merge gate also requires excluded item: ${item}`);
}

const rules = policy.state_rules ?? {};
if (rules.generated_asset_visual_review_may_block_merge !== false) fail('visual review must not block generated assets');
if (rules.generated_asset_manual_approval_may_block_merge !== false) fail('manual approval must not block generated assets');
if (rules.generated_asset_flow_continues_to_merge_after_generation !== true) fail('generated asset flow must continue to merge');

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
  canonicalProductionUrl,
  catalogUrl,
  publicHomeSections: homeSections,
  docsDirectoryAbsent: true
}, null, 2));
