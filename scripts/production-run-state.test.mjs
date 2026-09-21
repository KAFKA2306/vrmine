import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { applyStageResult, createRunState, loadRun, nextRunnableStage, persistRun } from './production-run-state.mjs';

const read = (file) => JSON.parse(fs.readFileSync(file, 'utf8'));
const registry = read('config/capability-registry.json');
const graph = read('config/production-stage-graph.json');
const request = {
  kind: 'world_prop', concept: 'japanese_cafe_table',
  target: {platform: ['pc', 'android'], engine: 'vrchat'},
  constraints: {collision: true, max_materials: 1},
  quality: {visual: 'booth_ready', topology: 'editable'}
};

const pass = (state, stage, evidence = `${stage.toLowerCase()}.json`) =>
  applyStageResult(state, stage, {status: 'PASS', evidence: [evidence]});

test('persists one isolated run directory with immutable request and plan', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'vrmine-run-'));
  const state = createRunState('table-001', request, registry, graph);
  const dir = persistRun(root, state);
  assert.ok(fs.existsSync(path.join(dir, 'artifacts/raw')));
  assert.ok(fs.existsSync(path.join(dir, 'artifacts/normalized')));
  assert.ok(fs.existsSync(path.join(dir, 'artifacts/optimized')));
  assert.deepEqual(loadRun(root, 'table-001'), state);
  assert.throws(() => persistRun(root, state));
});

test('resume selects only the first dependency-ready non-PASS stage', () => {
  const state = createRunState('table-002', request, registry, graph);
  assert.equal(nextRunnableStage(state).stage, 'SPEC');
  pass(state, 'SPEC');
  assert.equal(nextRunnableStage(state).stage, 'GENERATE');
});

test('keeps every unexecuted stage and run evidence UNVERIFIED', () => {
  const state = createRunState('table-003', request, registry, graph);
  assert.equal(state.evidence, 'UNVERIFIED');
  assert.ok(state.stages.every((stage) => stage.status === 'UNVERIFIED' && stage.attempts === 0));
});

test('requires evidence and rejects out-of-order stage mutation', () => {
  const state = createRunState('table-004', request, registry, graph);
  assert.throws(() => applyStageResult(state, 'GENERATE', {status: 'PASS', evidence: ['generate.json']}), /not runnable/);
  assert.throws(() => applyStageResult(state, 'SPEC', {status: 'PASS', evidence: []}), /requires evidence/);
});

test('REPAIRABLE keeps the same stage runnable and records each attempt', () => {
  const state = createRunState('table-005', request, registry, graph);
  applyStageResult(state, 'SPEC', {status: 'REPAIRABLE', evidence: ['spec-fail.json']});
  assert.equal(nextRunnableStage(state).stage, 'SPEC');
  assert.equal(state.stages[0].attempts, 1);
  assert.equal(state.evidence, 'REPAIRABLE');
  pass(state, 'SPEC', 'spec-fixed.json');
  assert.equal(state.stages[0].attempts, 2);
  assert.equal(nextRunnableStage(state).stage, 'GENERATE');
});

test('BLOCKED provider uses one configured fallback then becomes terminal', () => {
  const state = createRunState('table-006', request, registry, graph);
  pass(state, 'SPEC');
  assert.equal(state.stages[1].provider, 'blender_bpy');
  applyStageResult(state, 'GENERATE', {status: 'BLOCKED', evidence: ['bpy-blocked.json']});
  assert.equal(state.stages[1].provider, 'geometry_nodes');
  assert.equal(state.stages[1].status, 'UNVERIFIED');
  assert.equal(state.plan.fallback, null);
  assert.equal(nextRunnableStage(state).stage, 'GENERATE');
  applyStageResult(state, 'GENERATE', {status: 'BLOCKED', evidence: ['gn-blocked.json']});
  assert.equal(state.stages[1].status, 'BLOCKED');
  assert.equal(nextRunnableStage(state), null);
  assert.equal(state.evidence, 'BLOCKED');
});

test('run becomes PASS only when every planned stage has verified evidence', () => {
  const state = createRunState('table-007', request, registry, graph);
  for (const stage of state.stages) pass(state, stage.stage);
  assert.equal(state.evidence, 'PASS');
  assert.ok(state.stages.every((stage) => stage.status === 'PASS' && stage.evidence.length > 0));
});
