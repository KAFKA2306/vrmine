import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDir, '..');
const projectVersionPath = path.join(projectRoot, 'ProjectSettings', 'ProjectVersion.txt');
const projectVersion = fs.readFileSync(projectVersionPath, 'utf8').match(/^m_EditorVersion:\s*(.+)$/m)?.[1]?.trim();
if (!projectVersion) throw new Error(`Could not read m_EditorVersion from ${projectVersionPath}`);
if (projectVersion !== '2022.3.22f1') throw new Error(`Unsupported Unity version ${projectVersion}; expected 2022.3.22f1`);

const sourcePath = process.env.VRMINE_GLB_PATH;
if (!sourcePath || !fs.existsSync(sourcePath)) {
  throw new Error('VRMINE_GLB_PATH must point to the materialized GLB under test.');
}
for (const name of ['VRMINE_GLB_SHA256', 'VRMINE_GLB_VERTEX_COUNT', 'VRMINE_GLB_TRIANGLE_COUNT']) {
  if (!process.env[name]) throw new Error(`${name} is required so U2 cannot pass without an explicit source contract.`);
}

let unityPath = process.env.UNITY_EXE;
if (!unityPath && process.platform === 'win32') {
  const programFiles = process.env.ProgramFiles ?? process.env.PROGRAMFILES ?? 'C:\\Program Files';
  const hubPath = path.join(programFiles, 'Unity', 'Hub', 'Editor', projectVersion, 'Editor', 'Unity.exe');
  if (fs.existsSync(hubPath)) unityPath = hubPath;
}
if (!unityPath || !fs.existsSync(unityPath)) {
  throw new Error(`Unity ${projectVersion} was not found. Set UNITY_EXE to the exact Unity executable path.`);
}

const evidenceDir = path.join(projectRoot, 'Library', 'VRMine');
fs.mkdirSync(evidenceDir, { recursive: true });
const timestamp = new Date().toISOString().replaceAll(':', '').replaceAll('-', '').replace(/\.\d{3}Z$/, 'Z');
const logPath = path.join(evidenceDir, `glb-consumer-${timestamp}.log`);
const evidencePath = path.join(evidenceDir, 'glb-consumer-evidence.json');
if (fs.existsSync(evidencePath)) fs.rmSync(evidencePath);

const args = [
  '-batchmode',
  '-projectPath', projectRoot,
  '-executeMethod', 'GlbConsumerVerification.VerifyBatch',
  '-logFile', logPath,
  '-quit',
];

console.log(`Unity: ${unityPath}`);
console.log(`Project: ${projectRoot}`);
console.log(`GLB: ${path.resolve(sourcePath)}`);
console.log(`Expected SHA-256: ${process.env.VRMINE_GLB_SHA256}`);
console.log(`Expected vertices: ${process.env.VRMINE_GLB_VERTEX_COUNT}`);
console.log(`Expected triangles: ${process.env.VRMINE_GLB_TRIANGLE_COUNT}`);
console.log(`Evidence: ${evidencePath}`);

const run = spawnSync(unityPath, args, {
  cwd: projectRoot,
  env: process.env,
  stdio: 'inherit',
});
if (run.error) throw run.error;
if (!fs.existsSync(logPath)) throw new Error(`Unity did not create the expected log file: ${logPath}`);
if (run.status !== 0) throw new Error(`GLB Unity consumer verification failed with exit code ${run.status}. See ${logPath}`);
if (!fs.existsSync(evidencePath)) throw new Error(`Unity did not create the expected evidence file: ${evidencePath}`);

const evidence = JSON.parse(fs.readFileSync(evidencePath, 'utf8'));
if (evidence.status !== 'PASS') throw new Error(`Unity evidence is not PASS: ${evidencePath}`);
if (evidence.unityVersion !== projectVersion) {
  throw new Error(`Unity version drift: expected=${projectVersion}, actual=${evidence.unityVersion}`);
}
if (evidence.sourceSha256 !== process.env.VRMINE_GLB_SHA256.toLowerCase()) {
  throw new Error(`Evidence SHA-256 drift: expected=${process.env.VRMINE_GLB_SHA256}, actual=${evidence.sourceSha256}`);
}
if (evidence.vertexCount !== Number(process.env.VRMINE_GLB_VERTEX_COUNT)) {
  throw new Error(`Evidence vertex drift: expected=${process.env.VRMINE_GLB_VERTEX_COUNT}, actual=${evidence.vertexCount}`);
}
if (evidence.triangleCount !== Number(process.env.VRMINE_GLB_TRIANGLE_COUNT)) {
  throw new Error(`Evidence triangle drift: expected=${process.env.VRMINE_GLB_TRIANGLE_COUNT}, actual=${evidence.triangleCount}`);
}

console.log(`PASS: Unity GLB consumer verification. Evidence: ${evidencePath}`);
