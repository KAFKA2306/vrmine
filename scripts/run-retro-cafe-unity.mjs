import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDir, '..');
const projectVersionPath = path.join(projectRoot, 'ProjectSettings', 'ProjectVersion.txt');
const projectVersion = fs.readFileSync(projectVersionPath, 'utf8').match(/^m_EditorVersion:\s*(.+)$/m)?.[1]?.trim();
if (projectVersion !== '2022.3.22f1') {
  throw new Error(`Retro Cafe U2 requires Unity 2022.3.22f1; project declares ${projectVersion ?? 'UNKNOWN'}`);
}

const sourceDir = path.resolve(process.env.VRMINE_CAFE_SOURCE_DIR ?? path.join(projectRoot, '.artifacts', 'retro-cafe'));
const manifestPath = path.join(sourceDir, 'manifest.json');
if (!fs.existsSync(manifestPath)) throw new Error(`Missing Retro Cafe manifest: ${manifestPath}`);

const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
const names = ['pendant-light', 'table-lamp', 'wall-light', 'round-table', 'stool',
  'side-table', 'cup', 'saucer', 'tray', 'vase'];
if (!Array.isArray(manifest.models) || manifest.models.length !== names.length) {
  throw new Error('Retro Cafe manifest must contain exactly ten models.');
}
for (const name of names) {
  if (!manifest.models.some((model) => model.name === name)) throw new Error(`Manifest missing model: ${name}`);
  const fbx = path.join(sourceDir, `${name}.fbx`);
  if (!fs.existsSync(fbx)) throw new Error(`Missing Retro Cafe FBX: ${fbx}`);
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
const evidencePath = path.join(evidenceDir, 'retro-cafe-u2.json');
if (fs.existsSync(evidencePath)) fs.rmSync(evidencePath);
const timestamp = new Date().toISOString().replaceAll(':', '').replaceAll('-', '').replace(/\.\d{3}Z$/, 'Z');
const logPath = path.join(evidenceDir, `retro-cafe-u2-${timestamp}.log`);

const run = spawnSync(unityPath, [
  '-batchmode',
  '-projectPath', projectRoot,
  '-executeMethod', 'RetroCafePrefabBuilder.BuildAndVerifyBatch',
  '-logFile', logPath,
  '-quit',
], {
  cwd: projectRoot,
  env: { ...process.env, VRMINE_CAFE_SOURCE_DIR: sourceDir },
  stdio: 'inherit',
});
if (run.error) throw run.error;
if (run.status !== 0) throw new Error(`Retro Cafe Unity U2 failed with exit code ${run.status}. See ${logPath}`);
if (!fs.existsSync(evidencePath)) throw new Error(`Unity did not create evidence: ${evidencePath}`);

const evidence = JSON.parse(fs.readFileSync(evidencePath, 'utf8'));
if (evidence.status !== 'PASS') throw new Error(`Retro Cafe Unity evidence is not PASS: ${evidencePath}`);
if (evidence.unityVersion !== projectVersion) {
  throw new Error(`Unity version drift: expected=${projectVersion}, actual=${evidence.unityVersion}`);
}
if (evidence.prefabCount !== names.length || !Array.isArray(evidence.models) || evidence.models.length !== names.length) {
  throw new Error(`Expected ten verified prefabs, evidence says ${evidence.prefabCount}`);
}
for (const name of names) {
  const prefab = path.join(projectRoot, 'Assets', 'KafkaMade', 'VRMine', 'RetroCafe', 'Prefabs', `${name}.prefab`);
  if (!fs.existsSync(prefab)) throw new Error(`Verified prefab file is missing: ${prefab}`);
}

console.log(`PASS: Retro Cafe Unity U2 created and verified ten prefabs. Evidence: ${evidencePath}`);
