import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import { createRunState, nextRunnableStage } from './production-run-state.mjs';
import { classifyStageResult, executeNextStage, executeUntilBlocked } from './production-executor.mjs';

const read = (file) => JSON.parse(fs.readFileSync(file, 'utf8'));
const registry = read('config/capability-registry.json');
const graph = read('config/production-stage-graph.json');
const request = {
  kind: 'world_prop', concept: 'executor_fixture',
  target: {platform: ['pc'], engine: 'vrchat'},
  constraints: {collision: true, max_materials: 1},
  quality: {visual: 'booth_ready', topology: 'editable'}
};
const ok = () => ({status: 0});

function advanceToStatic(state) {
  executeNextStage(state, {runner: ok});
  executeNextStage(state, {
    adapters: {'GENERATE:blender_bpy': {command: 'producer', args: []}},
    runner: () => ({status: 0, artifacts: {raw: ['artifacts/raw/executor.glb']}})
  });
}

test('executes only the dependency-ready stage and records command evidence', () => {
  const state = createRunState('executor-001', request, registry, graph);
  executeNextStage(state, {runner: ok});
  assert.equal(state.stages[0].status, 'PASS');
  assert.match(state.stages[0].evidence[0], /^executor:/);
  assert.equal(nextRunnableStage(state).stage, 'GENERATE');
});

test('missing adapter fails closed and activates the configured provider fallback', () => {
  const state = createRunState('executor-002', request, registry, graph);
  executeNextStage(state, {runner: ok});
  executeNextStage(state, {adapters: {}});
  assert.equal(state.stages[1].provider, 'geometry_nodes');
  assert.equal(state.stages[1].status, 'UNVERIFIED');
  assert.equal(nextRunnableStage(state).stage, 'GENERATE');
  executeNextStage(state, {adapters: {}});
  assert.equal(state.stages[1].status, 'BLOCKED');
  assert.equal(state.evidence, 'BLOCKED');
});

test('provider adapter is selected by stage and provider and artifacts are recorded', () => {
  const state = createRunState('executor-003', request, registry, graph);
  executeNextStage(state, {runner: ok});
  const calls = [];
  executeNextStage(state, {
    adapters: {'GENERATE:blender_bpy': {command: 'producer', args: ['--request', 'executor-003']}},
    runner: (command) => {
      calls.push(command);
      return {status: 0, artifacts: {raw: ['artifacts/raw/executor-003.glb']}};
    }
  });
  assert.deepEqual(calls, [{command: 'producer', args: ['--request', 'executor-003']}]);
  assert.equal(state.stages[1].status, 'PASS');
  assert.deepEqual(state.artifacts.raw, ['artifacts/raw/executor-003.glb']);
  assert.equal(nextRunnableStage(state).stage, 'VALIDATE_STATIC');
});

test('nonzero adapter exit is BLOCKED and never promoted to PASS', () => {
  const state = createRunState('executor-004', request, registry, graph);
  executeNextStage(state, {runner: ok});
  executeNextStage(state, {
    adapters: {'GENERATE:blender_bpy': {command: 'producer', args: []}},
    runner: () => ({status: 9})
  });
  assert.equal(state.stages[1].provider, 'geometry_nodes');
  assert.equal(state.stages[1].status, 'UNVERIFIED');
  assert.equal(state.evidence, 'UNVERIFIED');
});

test('independent verifier can classify a successful process as REPAIRABLE', () => {
  const state = createRunState('executor-005', request, registry, graph);
  advanceToStatic(state);
  executeNextStage(state, {
    adapters: {VALIDATE_STATIC: {command: 'geometry-verifier', args: []}},
    runner: () => ({status: 0, verdict: 'REPAIRABLE', evidence: ['geometry:non-manifold=2']})
  });
  assert.equal(state.stages[2].status, 'REPAIRABLE');
  assert.equal(state.evidence, 'REPAIRABLE');
  assert.ok(state.stages[2].evidence.includes('geometry:non-manifold=2'));
});

test('repair success reruns the same verifier and can advance to PASS', () => {
  const state = createRunState('executor-006', request, registry, graph);
  advanceToStatic(state);
  let verifierCalls = 0;
  const adapters = {
    VALIDATE_STATIC: {command: 'geometry-verifier', args: []},
    'REPAIR:VALIDATE_STATIC': {command: 'geometry-repair', args: []}
  };
  const runner = (command) => {
    if (command.command === 'geometry-repair') return {status: 0, evidence: ['repair:merged-by-distance']};
    verifierCalls += 1;
    return verifierCalls === 1
      ? {status: 0, verdict: 'REPAIRABLE', evidence: ['geometry:non-manifold=2']}
      : {status: 0, verdict: 'PASS', evidence: ['geometry:non-manifold=0']};
  };
  executeNextStage(state, {adapters, runner});
  executeNextStage(state, {adapters, runner});
  assert.equal(state.stages[2].status, 'UNVERIFIED');
  assert.equal(state.stages[2].repair_attempts, 1);
  executeNextStage(state, {adapters, runner});
  assert.equal(state.stages[2].status, 'PASS');
  assert.ok(state.stages[2].evidence.includes('repair:merged-by-distance'));
});

test('repair failures stop at the configured attempt limit', () => {
  const state = createRunState('executor-007', request, registry, graph);
  advanceToStatic(state);
  executeNextStage(state, {
    adapters: {VALIDATE_STATIC: {command: 'geometry-verifier', args: []}},
    runner: () => ({status: 0, verdict: 'REPAIRABLE', evidence: ['geometry:non-manifold=2']})
  });
  const result = executeUntilBlocked(state, {
    adapters: {'REPAIR:VALIDATE_STATIC': {command: 'geometry-repair', args: []}},
    runner: () => ({status: 3}),
    maxRepairAttempts: 2
  });
  assert.equal(state.stages[2].repair_attempts, 2);
  assert.equal(state.stages[2].status, 'BLOCKED');
  assert.equal(state.evidence, 'BLOCKED');
  assert.equal(result.executed.length, 2);
});

test('verifier PASS requires a successful process exit', () => {
  assert.throws(() => classifyStageResult({status: 2, verdict: 'PASS'}), /cannot report PASS/);
  assert.throws(() => classifyStageResult({status: 0, verdict: 'UNVERIFIED'}), /invalid verifier verdict/);
});
