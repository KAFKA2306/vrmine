import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import { createRunState, nextRunnableStage } from './production-run-state.mjs';
import { executeNextStage } from './production-executor.mjs';

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
