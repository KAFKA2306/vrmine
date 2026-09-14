import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const modes = {
  registered: 'GaussianExhibitionVerification.VerifyRegisteredBatch',
  final: 'GaussianExhibitionVerification.VerifyBatch',
  sdk: 'GaussianExhibitionVerification.VerifySdkWorldBuilderBatch',
  performance: 'GaussianExhibitionVerification.VerifyPerformanceBatch',
  build: 'GaussianWorldBuildMeasurement.MeasureBatch',
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

const evidenceDir = path.resolve(process.env.VRMINE_EVIDENCE_DIR ?? path.join(projectRoot, 'Library', 'VRMine'));
fs.mkdirSync(evidenceDir, { recursive: true });
const timestamp = new Date().toISOString().replaceAll(':', '').replaceAll('-', '').replace(/\.\d{3}Z$/, 'Z');
const logPath = path.join(evidenceDir, `unity-${mode}-${timestamp}.log`);

function unityArgs(executeMethod, logFile, { quit = true } = {}) {
  return [
    '-batchmode',
    ...(quit ? ['-quit'] : []),
    '-projectPath', projectRoot,
    '-executeMethod', executeMethod,
    '-logFile', logFile,
  ];
}

const args = unityArgs(method, logPath, { quit: mode !== 'build' });

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

function requireBuildEvidence() {
  const evidencePath = path.join(evidenceDir, 'gaussian-build-evidence.json');
  if (!fs.existsSync(evidencePath)) throw new Error(`Build measurement exited 0 but evidence is missing: ${evidencePath}`);
  const evidence = JSON.parse(fs.readFileSync(evidencePath, 'utf8'));
  if (evidence.status !== 'MEASURED_BUILD_COMPLETE' || !Number.isSafeInteger(evidence.bundleBytes) || evidence.bundleBytes <= 0 || !/^[0-9a-f]{64}$/.test(evidence.sha256 ?? '')) {
    throw new Error(`Build evidence is incomplete: ${JSON.stringify(evidence)}`);
  }
  console.log(`PASS: VRChat world bundle measured at ${evidence.bundleBytes} bytes, sha256=${evidence.sha256}. Evidence: ${evidencePath}`);
}

if (mode === 'registered') {
  const evidencePath = path.join(evidenceDir, 'gaussian-u2-evidence.json');
  if (!fs.existsSync(evidencePath)) throw new Error(`Registered verification exited 0 but evidence is missing: ${evidencePath}`);
  console.log(`PASS: registered Unity verification. Evidence: ${evidencePath}`);
} else if (mode === 'performance') {
  const evidencePath = path.join(evidenceDir, 'gaussian-performance-evidence.json');
  if (!fs.existsSync(evidencePath)) throw new Error(`Performance verification exited 0 but evidence is missing: ${evidencePath}`);
  console.log(`PASS: performance evidence collection. Evidence: ${evidencePath}`);
} else if (mode === 'build') {
  requireBuildEvidence();
} else if (mode === 'sdk') {
  const marker = 'Gaussian SDK world builder validation completed without exception';
  if (!log.includes(marker)) throw new Error(`SDK verification exited 0 without the expected completion marker. See ${logPath}`);
  console.log('PASS: SDK world builder validation path completed without exception.');
} else if (mode === 'final') {
  const marker = 'Gaussian exhibition verification PASS';
  if (!log.includes(marker)) throw new Error(`Final verification exited 0 without the expected PASS marker. See ${logPath}`);
  console.log('PASS: strict final Gaussian exhibition verification.');

  const buildLogPath = path.join(evidenceDir, `unity-build-${timestamp}.log`);
  const buildRun = spawnSync(unityPath, unityArgs(modes.build, buildLogPath, { quit: false }), { cwd: projectRoot, stdio: 'inherit' });
  if (buildRun.error) throw buildRun.error;
  if (!fs.existsSync(buildLogPath)) throw new Error(`Unity did not create the build log file: ${buildLogPath}`);
  const buildLog = fs.readFileSync(buildLogPath, 'utf8');
  for (const pattern of knownRegressions) {
    if (buildLog.includes(pattern)) throw new Error(`Known Unity/VRChat regression reappeared during build: ${pattern}. See ${buildLogPath}`);
  }
  if (buildRun.status !== 0) throw new Error(`VRChat bundle measurement failed with exit code ${buildRun.status}. See ${buildLogPath}`);
  requireBuildEvidence();
}
