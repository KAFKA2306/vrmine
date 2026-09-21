import assert from 'node:assert/strict';
import test from 'node:test';
import { canonicalProductionAdapters, canonicalPilot, canonicalPilotB } from './production-adapters.mjs';

function state(overrides = {}) {
  return {
    request: {kind: 'world_prop', concept: 'cyber-alley-tea-nook'},
    artifacts: {raw: ['.artifacts/world-builds/cyber-alley-tea-nook/world.glb']},
    ...overrides
  };
}

test('canonical pilot binds GENERATE to the existing world build factory and declares its GLB', () => {
  const command = canonicalProductionAdapters(state()).GENERATE;
  assert.equal(command.command, 'blender');
  assert.deepEqual(command.args, [
    '-b', '--python-exit-code', '1', '--python', 'scripts/world_build_factory.py', '--',
    canonicalPilot.plan, canonicalPilot.root
  ]);
  assert.deepEqual(command.artifacts, {raw: [canonicalPilot.glb]});
});

test('canonical Pilot B binds GENERATE to the existing reproducible rigged-asset builder', () => {
  const command = canonicalProductionAdapters(state({
    request: {kind: 'rigged_asset', concept: canonicalPilotB.concept},
    artifacts: {raw: []}
  })).GENERATE;
  assert.equal(command.command, 'blender');
  assert.deepEqual(command.args, [
    '-b', '--python-exit-code', '1', '--python', 'scripts/build_astra_rigged_pilot.py', '--', canonicalPilotB.root
  ]);
  assert.deepEqual(command.artifacts, {raw: [canonicalPilotB.glb]});
});

test('canonical pilot binds VALIDATE_STATIC to the independent Blender verifier', () => {
  const current = state();
  const adapter = canonicalProductionAdapters(current).VALIDATE_STATIC;
  const command = adapter({state: current});
  assert.equal(command.command, 'blender');
  assert.deepEqual(command.args, [
    '-b', '--python-exit-code', '1', '--python', 'scripts/verify_world_build.py', '--',
    '.artifacts/world-builds/cyber-alley-tea-nook/build-plan.json',
    '.artifacts/world-builds/cyber-alley-tea-nook'
  ]);
});

test('canonical pilot binds RENDER to independent non-empty evidence verification', () => {
  const command = canonicalProductionAdapters(state()).RENDER;
  assert.equal(command.command, process.execPath);
  assert.deepEqual(command.args, ['scripts/verify-production-renders.mjs', canonicalPilot.root, ...canonicalPilot.renders]);
  assert.deepEqual(canonicalPilot.renders, [
    'view-hero.png', 'view-top.png', 'view-social-core.png', 'view-retreat.png', 'view-circulation.png'
  ]);
});

test('canonical pilot binds UNITY_IMPORT to the existing Unity 2022 GLB consumer verifier', () => {
  const current = state();
  const command = canonicalProductionAdapters(current).UNITY_IMPORT({state: current});
  assert.equal(command.command, process.execPath);
  assert.deepEqual(command.args, ['scripts/run-world-build-unity.mjs', canonicalPilot.root]);
  assert.deepEqual(command.artifacts, {unity: [canonicalPilot.unityEvidence]});
});

test('adapter fails closed for an unrelated concept or missing canonical GLB evidence', () => {
  assert.deepEqual(canonicalProductionAdapters(state({request: {kind: 'world_prop', concept: 'other'}})), {});
  assert.deepEqual(canonicalProductionAdapters(state({request: {kind: 'rigged_asset', concept: 'other'}})), {});
  const current = state({artifacts: {raw: []}});
  const adapters = canonicalProductionAdapters(current);
  assert.equal(adapters.VALIDATE_STATIC({state: current}), null);
  assert.equal(adapters.UNITY_IMPORT({state: current}), null);
});

test('adapter rejects a GLB outside the canonical pilot artifact root', () => {
  const current = state({artifacts: {raw: ['/tmp/untrusted.glb']}});
  const adapters = canonicalProductionAdapters(current);
  assert.equal(adapters.VALIDATE_STATIC({state: current}), null);
  assert.equal(adapters.UNITY_IMPORT({state: current}), null);
});
