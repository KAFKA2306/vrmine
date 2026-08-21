import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const modes = {
  build: 'GaussianExhibitionPipeline.BuildAndVerifyBatch',
  registered: 'GaussianExhibitionVerification.VerifyRegisteredBatch',
  final: 'GaussianExhibitionVerification.VerifyBatch',
  sdk: 'GaussianExhibitionVerification.VerifySdkWorldBuilderBatch',
  performance: 'GaussianExhibitionVerification.VerifyPerformanceBatch',
};

const mode = process.argv[2] ?? 'registered';
const method = modes[mode];
if (!method) {
  throw new Error(`Unknown mode ${JSON.stringify(mode)}. Expected one of: ${Object.keys(modes).join(', ')}`);
}

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDir, '..');
const projectVersionPath = path.join(projectRoot, 'ProjectSettings', 'ProjectVersion.txt');
const projectVersion = fs.readFileSync(projectVersionPath, 'utf8').match(/^m_EditorVersion:\s*(.+)$/m)?.[1]?.trim();
if (!projectVersion) {
  throw new Error(`Could not read m_EditorVersion from ${projectVersionPath}`);
}
if (projectVersion !== '2022.3.22f1') {
  throw new Error(`Unsupported Unity version ${projectVersion}; expected 2022.3.22f1`);
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
const logPath = path.join(evidenceDir, `unity-${mode}-${timestamp}.log`);
const args = [
  '-batchmode',
  '-quit',
  '-projectPath', projectRoot,
  '-executeMethod', method,
  '-logFile', logPath,
];

console.log(`Unity: ${unityPath}`);
console.log(`Project: ${projectRoot}`);
console.log(`Mode: ${mode}`);
console.log(`Method: ${method}`);
console.log(`Log: ${logPath}`);

const run = spawnSync(unityPath, args, { cwd: projectRoot, stdio: 'inherit' });
if (run.error) throw run.error;
if (!fs.existsSync(logPath)) {
  throw new Error(`Unity did not create the expected log file: ${logPath}`);
}

const log = fs.readFileSync(logPath, 'utf8');
const knownRegressions = [
  'IndexOutOfRangeException: Index was outside the bounds of the array',
  'No PipelineManager found in scene',
  "Problem detected while opening the Scene file",
];
for (const pattern of knownRegressions) {
  if (log.includes(pattern)) {
    throw new Error(`Known Unity/VRChat regression reappeared: ${pattern}. See ${logPath}`);
  }
}
if (run.status !== 0) {
  throw new Error(`Unity verification failed with exit code ${run.status}. See ${logPath}`);
}

if (mode === 'build') {
  const marker = 'VRMine 3DGS final pipeline PASS';
  if (!log.includes(marker)) throw new Error(`Build verification exited 0 without the expected PASS marker. See ${logPath}`);
  console.log('PASS: final Gaussian build, bake and repository verification pipeline.');
} else if (mode === 'registered') {
  const evidencePath = path.join(evidenceDir, 'gaussian-u2-evidence.json');
  if (!fs.existsSync(evidencePath)) throw new Error(`Registered verification exited 0 but evidence is missing: ${evidencePath}`);
  console.log(`PASS: registered Unity verification. Evidence: ${evidencePath}`);
} else if (mode === 'performance') {
  const evidencePath = path.join(evidenceDir, 'gaussian-performance-evidence.json');
  if (!fs.existsSync(evidencePath)) throw new Error(`Performance verification exited 0 but evidence is missing: ${evidencePath}`);
  console.log(`PASS: performance evidence collection. Evidence: ${evidencePath}`);
} else if (mode === 'sdk') {
  const marker = 'Gaussian SDK world builder validation completed without exception';
  if (!log.includes(marker)) throw new Error(`SDK verification exited 0 without the expected completion marker. See ${logPath}`);
  console.log('PASS: SDK world builder validation path completed without exception.');
} else if (mode === 'final') {
  const marker = 'Gaussian exhibition verification PASS';
  if (!log.includes(marker)) throw new Error(`Final verification exited 0 without the expected PASS marker. See ${logPath}`);
  console.log('PASS: strict final Gaussian exhibition verification.');
}
