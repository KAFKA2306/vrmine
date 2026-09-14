import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const expectedVersion = '2022.3.22f1';
const projectVersion = fs.readFileSync(path.join(projectRoot, 'ProjectSettings', 'ProjectVersion.txt'), 'utf8').match(/^m_EditorVersion:\s*(.+)$/m)?.[1]?.trim();
if (projectVersion !== expectedVersion) throw new Error(`Unity version mismatch: ${projectVersion} (expected ${expectedVersion})`);

const unityPath = process.env.UNITY_EXE ?? path.join(process.env.ProgramFiles ?? 'C:\\Program Files', 'Unity', 'Hub', 'Editor', expectedVersion, 'Editor', 'Unity.exe');
if (!fs.existsSync(unityPath)) throw new Error(`Unity executable not found: ${unityPath}`);
const evidenceDir = path.resolve(process.env.VRMINE_EVIDENCE_DIR ?? path.join(projectRoot, 'Library', 'VRMine'));
fs.mkdirSync(evidenceDir, { recursive: true });

const results = [];
for (const platform of ['editmode', 'playmode']) {
  const stamp = new Date().toISOString().replaceAll(':', '').replaceAll('-', '').replace(/\.\d{3}Z$/, 'Z');
  const logPath = path.join(evidenceDir, `unity-tests-${platform}-${stamp}.log`);
  const resultPath = path.join(evidenceDir, `unity-tests-${platform}-${stamp}.json`);
  const method = platform === 'editmode' ? 'VRMineBatchVerification.RunEditMode' : 'VRMineBatchVerification.RunPlayMode';
  const run = spawnSync(unityPath, ['-batchmode', '-projectPath', projectRoot, '-executeMethod', method, '-logFile', logPath], {
    cwd: projectRoot,
    env: { ...process.env, VRMINE_EVIDENCE_DIR: evidenceDir, VRMINE_EVIDENCE_FILE: resultPath },
    stdio: 'inherit',
    timeout: 180000,
  });
  const record = { platform, method, unityVersion: expectedVersion, exitCode: run.status ?? 127, logPath, resultPath, error: run.error ? String(run.error) : null };
  results.push(record);
  let evidence = null;
  if (fs.existsSync(resultPath)) {
    try { evidence = JSON.parse(fs.readFileSync(resultPath, 'utf8')); } catch { evidence = null; }
  }
  if (run.error || run.status !== 0 || !evidence || evidence.status !== 'PASS' || evidence.mode !== platform) {
    fs.writeFileSync(path.join(evidenceDir, 'unity-tests-evidence.json'), JSON.stringify({ schemaVersion: 1, status: 'FAIL', results }, null, 2) + '\n');
    process.exit(1);
  }
}
fs.writeFileSync(path.join(evidenceDir, 'unity-tests-evidence.json'), JSON.stringify({ schemaVersion: 1, status: 'PASS', results }, null, 2) + '\n');
console.log(`PASS: Unity EditMode and PlayMode tests. Evidence: ${evidenceDir}`);
