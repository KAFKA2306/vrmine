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
const logPath = path.join(evidenceDir, `perspective-cage-${timestamp}.log`);
const args = [
  '-batchmode',
  '-projectPath', projectRoot,
  '-executeMethod', 'PerspectiveCageVerification.BuildAndVerifyBatch',
  '-logFile', logPath,
];

console.log(`Unity: ${unityPath}`);
console.log(`Project: ${projectRoot}`);
console.log('Method: PerspectiveCageVerification.BuildAndVerifyBatch');
console.log(`Log: ${logPath}`);

const run = spawnSync(unityPath, args, { cwd: projectRoot, stdio: 'inherit' });
if (run.error) throw run.error;
if (!fs.existsSync(logPath)) throw new Error(`Unity did not create the expected log file: ${logPath}`);

const log = fs.readFileSync(logPath, 'utf8');
const fatalPatterns = [
  'Compilation failed',
  'Scripts have compiler errors',
  "Problem detected while opening the Scene file",
  'NullReferenceException',
];
for (const pattern of fatalPatterns) {
  if (log.includes(pattern)) throw new Error(`Unity regression detected: ${pattern}. See ${logPath}`);
}
if (run.status !== 0) throw new Error(`Perspective Cage Unity verification failed with exit code ${run.status}. See ${logPath}`);
if (!log.includes('Perspective Cage verification PASS')) throw new Error(`Unity exited 0 without Perspective Cage PASS marker. See ${logPath}`);

const scenePath = path.join(projectRoot, 'Assets', 'KafkaMade', 'VRMine', 'Puzzles', 'PerspectiveCage', 'Scenes', 'PerspectiveCage.unity');
const reportPath = path.join(projectRoot, 'Assets', 'KafkaMade', 'VRMine', 'Puzzles', 'PerspectiveCage', 'Verification', 'LatestPerspectiveCageVerification.txt');
if (!fs.existsSync(scenePath)) throw new Error(`Canonical scene was not generated: ${scenePath}`);
if (!fs.existsSync(reportPath)) throw new Error(`Unity verification report was not generated: ${reportPath}`);
const report = fs.readFileSync(reportPath, 'utf8');
if (!report.includes('Result: PASS')) throw new Error(`Unity report is not PASS: ${reportPath}`);

console.log(`PASS: Perspective Cage Unity build/verification. Scene: ${scenePath}`);
console.log(`Evidence: ${reportPath}`);
