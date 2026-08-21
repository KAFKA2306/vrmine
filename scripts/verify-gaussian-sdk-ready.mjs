import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(scriptDir, '..');
const evidenceDir = path.join(root, 'Library', 'VRMine');
const statusPath = path.join(evidenceDir, 'gaussian-sdk-readiness.json');
const registryPath = path.join(root, 'config', 'gaussian-splats.json');
const exhibitionPath = path.join(root, 'config', 'gaussian-exhibition.json');
const registry = JSON.parse(fs.readFileSync(registryPath, 'utf8'));
const exhibition = JSON.parse(fs.readFileSync(exhibitionPath, 'utf8'));

if (!Array.isArray(registry.environments) || registry.environments.length !== 20) {
  throw new Error(`Final Gaussian SDK readiness requires exactly 20 registered environments; got ${registry.environments?.length ?? 'missing'}`);
}

fs.mkdirSync(evidenceDir, { recursive: true });

const state = {
  schema_version: 1,
  goal: 'final-20-gaussian-sdk-readiness',
  started_at: new Date().toISOString(),
  status: 'running',
  registered_artifacts: registry.environments.length,
  presentation: {
    imported_target_extent_m: exhibition.import_defaults?.normalize?.target_extent_m ?? null,
    presentation_scale_multiplier: exhibition.presentation_scale_multiplier ?? null,
  },
  steps: [],
  evidence: {},
  client_validation: {
    status: 'not_run',
    reason: 'This runner validates repository, Unity Editor and VRChat SDK readiness only. Actual VRChat client launch is a separate evidence gate.',
  },
};

function persist() {
  const temporary = `${statusPath}.tmp`;
  fs.writeFileSync(temporary, `${JSON.stringify(state, null, 2)}\n`, 'utf8');
  fs.renameSync(temporary, statusPath);
}

function runStep(name, command, args) {
  const step = {
    name,
    command: [command, ...args],
    started_at: new Date().toISOString(),
    status: 'running',
  };
  state.steps.push(step);
  persist();

  const run = spawnSync(command, args, {
    cwd: root,
    env: process.env,
    encoding: 'utf8',
    stdio: ['inherit', 'pipe', 'pipe'],
    maxBuffer: 64 * 1024 * 1024,
  });
  if (run.stdout) process.stdout.write(run.stdout);
  if (run.stderr) process.stderr.write(run.stderr);

  step.completed_at = new Date().toISOString();
  step.exit_code = run.status;
  if (run.error || run.status !== 0) {
    step.status = 'failed';
    step.error = run.error ? String(run.error) : `exit code ${run.status}`;
    state.status = 'failed';
    state.failed_step = name;
    state.completed_at = new Date().toISOString();
    persist();
    throw run.error ?? new Error(`${name} failed with exit code ${run.status}`);
  }
  step.status = 'passed';
  persist();
}

function readEvidence(filename) {
  const evidencePath = path.join(evidenceDir, filename);
  if (!fs.existsSync(evidencePath)) throw new Error(`Required evidence is missing: ${evidencePath}`);
  return { path: path.relative(root, evidencePath), payload: JSON.parse(fs.readFileSync(evidencePath, 'utf8')) };
}

function assertEqual(actual, expected, label) {
  if (actual !== expected) throw new Error(`${label}: expected ${expected}, got ${actual}`);
}

function validateEvidence() {
  const u2 = readEvidence('gaussian-u2-evidence.json');
  const performance = readEvidence('gaussian-performance-evidence.json');
  const payload = u2.payload;

  assertEqual(payload.registered, 20, 'registered source count');
  assertEqual(payload.gaussianSplatObjects, 20, 'active GaussianSplatObject count');
  assertEqual(payload.prefabs, 20, 'Gaussian prefab count');
  assertEqual(payload.exhibits, 20, 'exhibit count');
  assertEqual(payload.pads, 20, 'pad count');
  assertEqual(payload.labels, 20, 'label count');
  assertEqual(payload.renderers, 1, 'GaussianSplatRenderer count');
  assertEqual(payload.descriptors, 1, 'VRCSceneDescriptor count');
  assertEqual(payload.pipelineManagers, 1, 'PipelineManager count');
  assertEqual(payload.missingScripts, 0, 'missing script count');
  assertEqual(payload.canonicalBuildSceneOnly, true, 'canonical build scene exclusivity');
  assertEqual(payload.sceneDirty, false, 'saved scene dirty state');
  assertEqual(payload.measurements?.length, 20, 'per-splat measurement count');

  const measurements = payload.measurements ?? [];
  const floorTolerance = 0.01;
  const floorFailures = measurements.filter((entry) => Math.abs(entry.floorBottom) > floorTolerance);
  if (floorFailures.length !== 0) {
    throw new Error(`floor alignment failures=${floorFailures.length}; tolerance=${floorTolerance}m`);
  }

  const multiplier = Number(exhibition.presentation_scale_multiplier);
  const importedTarget = Number(exhibition.import_defaults?.normalize?.target_extent_m);
  if (!Number.isFinite(multiplier) || multiplier !== 2) {
    throw new Error(`presentation_scale_multiplier must be exactly 2.0; got ${exhibition.presentation_scale_multiplier}`);
  }
  if (!Number.isFinite(importedTarget) || Math.abs(importedTarget - 1) > 1e-9) {
    throw new Error(`reusable import target extent must remain exactly 1.0m; got ${exhibition.import_defaults?.normalize?.target_extent_m}`);
  }
  const presentedTarget = importedTarget * multiplier;
  const extentTolerance = 0.05;
  const extentFailures = measurements.filter((entry) => Math.abs(entry.extent - presentedTarget) > extentTolerance);
  if (extentFailures.length !== 0) {
    throw new Error(`presented extent failures=${extentFailures.length}; target=${presentedTarget}m tolerance=${extentTolerance}m`);
  }

  const perf = performance.payload;
  assertEqual(perf.registered, 20, 'performance registered count');
  assertEqual(perf.exhibits, 20, 'performance exhibit count');
  assertEqual(perf.renderers, 1, 'performance renderer count');
  assertEqual(perf.sourcePlyFiles, 20, 'materialized source PLY count');
  if (!(perf.sourcePlyBytes > 0)) throw new Error(`sourcePlyBytes must be > 0; got ${perf.sourcePlyBytes}`);
  if (!(perf.importedAssetFiles > 0)) throw new Error(`importedAssetFiles must be > 0; got ${perf.importedAssetFiles}`);
  assertEqual(perf.status, 'MEASURED_FINAL_COUNT', 'performance final-count status');

  state.evidence = {
    unity_registered: u2.path,
    performance: performance.path,
    measurements: {
      count: measurements.length,
      presented_target_extent_m: presentedTarget,
      extent_tolerance_m: extentTolerance,
      extent_failures: extentFailures.length,
      floor_tolerance_m: floorTolerance,
      floor_failures: floorFailures.length,
    },
    counts: {
      prefabs: payload.prefabs,
      exhibits: payload.exhibits,
      renderers: payload.renderers,
      descriptors: payload.descriptors,
      pipeline_managers: payload.pipelineManagers,
      missing_scripts: payload.missingScripts,
      source_ply_files: perf.sourcePlyFiles,
    },
  };
}

persist();

try {
  runStep('install-vrc-get', 'node', ['scripts/install-vrc-get.mjs']);
  runStep('verify-vpm', 'node', ['scripts/verify-vpm.mjs']);
  runStep('materialize-pinned-renderer', 'node', ['scripts/materialize-gaussian-renderer.mjs']);
  runStep('materialize-and-hash-verify-final-20-ply', 'node', ['scripts/materialize-gaussian-sources.mjs']);
  runStep('unity-build-bake-and-final-verify', 'node', ['scripts/run-gaussian-unity.mjs', 'build']);
  runStep('unity-collect-20-splat-measurements', 'node', ['scripts/run-gaussian-unity.mjs', 'registered']);
  runStep('vrchat-sdk-world-builder-validation', 'node', ['scripts/run-gaussian-unity.mjs', 'sdk']);
  runStep('unity-performance-evidence', 'node', ['scripts/run-gaussian-unity.mjs', 'performance']);
  validateEvidence();
  state.status = 'sdk_ready';
  state.completed_at = new Date().toISOString();
  persist();
  console.log(`Gaussian SDK readiness PASS: ${path.relative(root, statusPath)}`);
  console.log('Actual VRChat client validation remains a separate not_run gate by design.');
} catch (error) {
  if (state.status !== 'failed') {
    state.status = 'failed';
    state.failed_step = state.failed_step ?? 'evidence-validation';
    state.error = String(error);
    state.completed_at = new Date().toISOString();
    persist();
  }
  throw error;
}
