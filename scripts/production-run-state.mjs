import fs from 'node:fs';
import path from 'node:path';
import { planProductionRequest } from './plan-production-request.mjs';

const buckets = ['raw', 'normalized', 'optimized', 'reports', 'renders', 'unity', 'evidence'];
const terminalStates = new Set(['PASS', 'REPAIRABLE', 'BLOCKED']);

export function createRunState(runId, request, registry, stageGraph) {
  if (!/^[a-zA-Z0-9._-]+$/.test(runId)) throw new Error('run_id must be filesystem-safe');
  const plan = planProductionRequest(request, registry, stageGraph);
  if (plan.status !== 'PLANNED') return plan;
  return {
    schema_version: 1,
    run_id: runId,
    request,
    plan,
    artifacts: Object.fromEntries(buckets.map((name) => [name, []])),
    stages: plan.stages.map(({index, stage, status, expected_outputs, provider}) => ({
      index, stage, status, attempts: 0, expected_outputs, evidence: [], provider: provider ?? null
    })),
    evidence: 'UNVERIFIED'
  };
}

export function nextRunnableStage(state) {
  for (const stage of state.stages) {
    if (stage.status === 'PASS') continue;
    if (stage.status === 'BLOCKED') return null;
    const requirements = state.plan.stages[stage.index]?.requires ?? [];
    const ready = requirements.every((name) => state.stages.find((item) => item.stage === name)?.status === 'PASS');
    return ready ? stage : null;
  }
  return null;
}

export function applyStageResult(state, stageName, result) {
  if (!terminalStates.has(result.status)) throw new Error(`invalid stage result: ${result.status}`);
  const stage = state.stages.find((item) => item.stage === stageName);
  if (!stage) throw new Error(`unknown stage: ${stageName}`);
  const runnable = nextRunnableStage(state);
  if (!runnable || runnable.stage !== stageName) throw new Error(`stage is not runnable: ${stageName}`);
  if (!Array.isArray(result.evidence) || result.evidence.length === 0) throw new Error('stage result requires evidence');

  stage.attempts += 1;
  stage.status = result.status;
  stage.evidence.push(...result.evidence);
  for (const [bucket, entries] of Object.entries(result.artifacts ?? {})) {
    if (!buckets.includes(bucket) || !Array.isArray(entries)) throw new Error(`invalid artifact bucket: ${bucket}`);
    state.artifacts[bucket].push(...entries);
  }

  if (result.status === 'BLOCKED' && stage.provider && state.plan.fallback) {
    stage.provider = state.plan.fallback;
    stage.status = 'UNVERIFIED';
    state.plan.provider = state.plan.fallback;
    state.plan.fallback = null;
  }
  state.evidence = state.stages.every((item) => item.status === 'PASS') ? 'PASS' :
    state.stages.some((item) => item.status === 'BLOCKED') ? 'BLOCKED' :
    state.stages.some((item) => item.status === 'REPAIRABLE') ? 'REPAIRABLE' : 'UNVERIFIED';
  return state;
}

export function saveRun(runRoot, state) {
  const file = path.join(runRoot, state.run_id, 'result.json');
  fs.writeFileSync(file, `${JSON.stringify(state, null, 2)}\n`);
  return file;
}

export function persistRun(runRoot, state) {
  const dir = path.join(runRoot, state.run_id);
  fs.mkdirSync(dir, {recursive: false});
  for (const name of ['artifacts/raw', 'artifacts/normalized', 'artifacts/optimized', 'reports', 'renders', 'unity', 'evidence']) {
    fs.mkdirSync(path.join(dir, name), {recursive: true});
  }
  fs.writeFileSync(path.join(dir, 'request.json'), `${JSON.stringify(state.request, null, 2)}\n`, {flag: 'wx'});
  fs.writeFileSync(path.join(dir, 'plan.json'), `${JSON.stringify(state.plan, null, 2)}\n`, {flag: 'wx'});
  fs.writeFileSync(path.join(dir, 'result.json'), `${JSON.stringify(state, null, 2)}\n`, {flag: 'wx'});
  return dir;
}

export function loadRun(runRoot, runId) {
  const file = path.join(runRoot, runId, 'result.json');
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const [command, runId, requestPath, runRoot = 'runs'] = process.argv.slice(2);
  if (!['init', 'resume'].includes(command) || !runId) throw new Error('usage: node scripts/production-run-state.mjs <init|resume> <run-id> [request.json] [run-root]');
  if (command === 'resume') {
    const state = loadRun(runRoot, runId);
    process.stdout.write(`${JSON.stringify({run_id: runId, next_stage: nextRunnableStage(state)}, null, 2)}\n`);
  } else {
    if (!requestPath) throw new Error('init requires request.json');
    const read = (file) => JSON.parse(fs.readFileSync(file, 'utf8'));
    const state = createRunState(runId, read(requestPath), read('config/capability-registry.json'), read('config/production-stage-graph.json'));
    if (state.status && state.status !== 'PLANNED') {
      process.stdout.write(`${JSON.stringify(state, null, 2)}\n`);
      process.exitCode = 2;
    } else {
      const dir = persistRun(runRoot, state);
      process.stdout.write(`${JSON.stringify({run_id: runId, directory: dir, next_stage: nextRunnableStage(state)}, null, 2)}\n`);
    }
  }
}
