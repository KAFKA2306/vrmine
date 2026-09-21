import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import { routeProductionRequest } from './route-production-request.mjs';

const registry = JSON.parse(fs.readFileSync('config/capability-registry.json', 'utf8'));

const base = {
  kind: 'world_prop',
  concept: 'japanese_cafe_table',
  dimensions_m: [0.75, 0.75, 0.70],
  target: {platform: ['pc', 'android'], engine: 'vrchat'},
  constraints: {collision: true, max_materials: 1},
  quality: {visual: 'booth_ready', topology: 'editable'}
};

test('selects deterministic P0 provider and preserves verifier authority', () => {
  const plan = routeProductionRequest(base, registry);
  assert.equal(plan.status, 'PLANNED');
  assert.equal(plan.capability, 'procedural_mesh');
  assert.equal(plan.provider, 'blender_bpy');
  assert.equal(plan.control_mode, 'deterministic_code');
  assert.equal(plan.verifier, 'world-build:pilot');
  assert.equal(plan.evidence, 'UNVERIFIED');
});

test('does not infer execution evidence from routing', () => {
  const plan = routeProductionRequest(base, registry);
  assert.equal(plan.evidence, 'UNVERIFIED');
  assert.ok(!('result' in plan));
});

test('fails closed when request kind has no registered capability', () => {
  const plan = routeProductionRequest({...base, kind: 'vrchat_gimmick'}, registry);
  assert.equal(plan.status, 'BLOCKED');
  assert.match(plan.reason, /no registered capability/);
});

test('fails closed when no provider supports every requested platform', () => {
  const constrained = structuredClone(registry);
  constrained.capabilities.procedural_mesh.providers.forEach((provider) => {
    provider.platforms = ['pc'];
  });
  const plan = routeProductionRequest(base, constrained);
  assert.equal(plan.status, 'BLOCKED');
  assert.match(plan.reason, /no provider satisfies/);
});
