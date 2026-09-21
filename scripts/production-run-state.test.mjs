import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { createRunState, loadRun, nextRunnableStage, persistRun } from './production-run-state.mjs';

const read = (file) => JSON.parse(fs.readFileSync(file, 'utf8'));
const registry = read('config/capability-registry.json');
const graph = read('config/production-stage-graph.json');
const request = {
  kind: 'world_prop', concept: 'japanese_cafe_table',
  target: {platform: ['pc', 'android'], engine: 'vrchat'},
  constraints: {collision: true, max_materials: 1},
  quality: {visual: 'booth_ready', topology: 'editable'}
};

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
  state.stages[0].status = 'PASS';
  assert.equal(nextRunnableStage(state).stage, 'GENERATE');
  state.stages[1].status = 'BLOCKED';
  assert.equal(nextRunnableStage(state), null);
});

test('keeps every unexecuted stage and run evidence UNVERIFIED', () => {
  const state = createRunState('table-003', request, registry, graph);
  assert.equal(state.evidence, 'UNVERIFIED');
  assert.ok(state.stages.every((stage) => stage.status === 'UNVERIFIED' && stage.attempts === 0));
});
