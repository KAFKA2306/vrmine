import assert from 'node:assert/strict';
import test from 'node:test';
import { canonicalProductionAdapters, canonicalPilot } from './production-adapters.mjs';

function state(overrides = {}) {
  return {
    request: {kind: 'world_prop', concept: 'cyber-alley-tea-nook'},
    artifacts: {raw: ['.artifacts/world-builds/cyber-alley-tea-nook/cyber-alley-tea-nook.glb']},
    ...overrides
  };
}

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

test('adapter fails closed for an unrelated concept or missing canonical GLB evidence', () => {
  assert.deepEqual(canonicalProductionAdapters(state({request: {kind: 'world_prop', concept: 'other'}})), {});
  const current = state({artifacts: {raw: []}});
  assert.equal(canonicalProductionAdapters(current).VALIDATE_STATIC({state: current}), null);
});

test('adapter rejects a GLB outside the canonical pilot artifact root', () => {
  const current = state({artifacts: {raw: ['/tmp/untrusted.glb']}});
  assert.equal(canonicalProductionAdapters(current).VALIDATE_STATIC({state: current}), null);
});
