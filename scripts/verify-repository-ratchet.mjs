import { access, readFile } from 'node:fs/promises';

const requiredPaths = [
  'pages/index.html',
  'pages/games/registry.js',
  'pages/assets/platform.js',
  'Assets',
  'ontology/project.yaml',
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

const forbiddenWorkflow = '.github/workflows/weekly-repo-research.yml';
try {
  await access(forbiddenWorkflow);
  throw new Error(`${forbiddenWorkflow} is unrelated to the canonical game flow and must stay removed`);
} catch (error) {
  if (error?.code !== 'ENOENT') throw error;
}

console.log('VRMine repository ratchet contract: PASS');
