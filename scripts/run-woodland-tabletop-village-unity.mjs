import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDir, '..');
const projectVersionPath = path.join(projectRoot, 'ProjectSettings', 'ProjectVersion.txt');
const projectVersion = fs.readFileSync(projectVersionPath, 'utf8').match(/^m_EditorVersion:\s*(.+)$/m)?.[1]?.trim();
if (projectVersion !== '2022.3.22f1') {
  throw new Error(`Woodland Tabletop Village requires Unity 2022.3.22f1; project declares ${projectVersion ?? 'UNKNOWN'}`);
}

const specPath = path.join(projectRoot, 'config', 'world-design', 'generated', 'woodland-tabletop-village-v0.json');
if (!fs.existsSync(specPath)) throw new Error(`Missing canonical woodland spec: ${specPath}`);
const spec = JSON.parse(fs.readFileSync(specPath, 'utf8'));
if (spec?.meta?.platform_target !== 'CROSS_PLATFORM_QUEST') throw new Error('Woodland spec must remain CROSS_PLATFORM_QUEST.');
if (spec?.runtime_budget?.realtime_light_count !== 0) throw new Error('Woodland spec must require zero realtime lights.');
if (!Array.isArray(spec?.blockout?.anchors) || spec.blockout.anchors.length !== 8) throw new Error('Woodland spec must contain exactly eight canonical blockout anchors.');

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
const logPath = path.join(evidenceDir, `woodland-tabletop-village-${timestamp}.log`);
const scenePath = path.join(projectRoot, 'Assets', 'KafkaMade', 'VRMine', 'Scenes', 'WoodlandTabletopVillage.unity');

const run = spawnSync(unityPath, [
  '-batchmode',
  '-projectPath', projectRoot,
  '-executeMethod', 'WoodlandTabletopVillageSceneBuilder.BuildBatch',
  '-logFile', logPath,
  '-quit',
], {
  cwd: projectRoot,
  env: { ...process.env },
  stdio: 'inherit',
});
if (run.error) throw run.error;
if (run.status !== 0) throw new Error(`Woodland Tabletop Village Unity build failed with exit code ${run.status}. See ${logPath}`);
if (!fs.existsSync(scenePath) || fs.statSync(scenePath).size === 0) {
  throw new Error(`Unity completed without materializing the scene: ${scenePath}`);
}

console.log(`PASS: Woodland Tabletop Village scene built and verified from canonical spec. Scene: ${scenePath}`);
console.log(`Unity log: ${logPath}`);