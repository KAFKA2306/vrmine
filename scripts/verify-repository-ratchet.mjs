import { access, readFile } from 'node:fs/promises';

const requiredPaths = [
  'pages/index.html',
  'pages/games/registry.js',
  'pages/assets/platform.js',
  'Assets',
  'ontology/project.yaml',
  'Taskfile.yml',
];

for (const path of requiredPaths) {
  await access(path);
}

const readme = await readFile('README.md', 'utf8');
for (const phrase of [
  '## 正準ユーザーフロー',
  '### 非目標',
  '### Ratchet KPI',
  '主要KPIは3つに限定します。',
]) {
  if (!readme.includes(phrase)) {
    throw new Error(`README canonical contract missing: ${phrase}`);
  }
}

const agents = await readFile('AGENTS.md', 'utf8');
for (const phrase of [
  '## Complexity Ratchet',
  'Reuse an existing canonical component before adding a new abstraction.',
  'Do not reduce tests, observability, fail-fast behavior, or U1–U5 evidence merely to reduce LOC.',
  'Use the existing `Taskfile.yml` interface rather than adding parallel shell/PowerShell/npm command surfaces for the same intent.',
]) {
  if (!agents.includes(phrase)) {
    throw new Error(`AGENTS complexity contract missing: ${phrase}`);
  }
}

const forbiddenWorkflow = '.github/workflows/weekly-repo-research.yml';
try {
  await access(forbiddenWorkflow);
  throw new Error(`${forbiddenWorkflow} is unrelated to the canonical game flow and must stay removed`);
} catch (error) {
  if (error?.code !== 'ENOENT') throw error;
}

console.log('VRMine repository ratchet contract: PASS');
