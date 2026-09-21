import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import { planProductionRequest } from './plan-production-request.mjs';

const read = (path) => JSON.parse(fs.readFileSync(path, 'utf8'));
const registry = read('config/capability-registry.json');
const graph = read('config/production-stage-graph.json');
const base = {
  kind: 'world_prop', concept: 'japanese_cafe_table',
  target: {platform: ['pc', 'android'], engine: 'vrchat'},
  constraints: {collision: true, max_materials: 1},
  quality: {visual: 'booth_ready', topology: 'editable'}
};

test('builds a deterministic world-prop stage graph from the routed provider', () => {
  const plan = planProductionRequest(base, registry, graph);
  assert.equal(plan.status, 'PLANNED');
  assert.deepEqual(plan.stages.map((s) => s.stage), ['SPEC','GENERATE','VALIDATE_STATIC','RENDER','UNITY_IMPORT','PLATFORM_VERIFY']);
  assert.equal(plan.stages.find((s) => s.stage === 'GENERATE').provider, 'blender_bpy');
  assert.equal(plan.stages.find((s) => s.stage === 'VALIDATE_STATIC').verifier, 'world-build:pilot');
  assert.ok(plan.stages.every((s) => s.status === 'UNVERIFIED'));
});

test('rigged assets include normalization without adding irrelevant world stages', () => {
  const plan = planProductionRequest({...base, kind: 'rigged_asset'}, registry, graph);
  assert.ok(plan.stages.some((s) => s.stage === 'NORMALIZE'));
  assert.ok(!plan.stages.some((s) => s.stage === 'PUBLISH'));
});

test('fails closed when a routed kind has no stage graph', () => {
  const broken = structuredClone(graph);
  delete broken.graphs.world_prop;
  const plan = planProductionRequest(base, registry, broken);
  assert.equal(plan.status, 'BLOCKED');
  assert.match(plan.reason, /no stage graph/);
  assert.equal(plan.evidence, 'UNVERIFIED');
});
