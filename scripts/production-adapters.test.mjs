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

function pilotBState(artifacts = {raw: [canonicalPilotB.glb], normalized: [canonicalPilotB.normalizedGlb]}) {
  return state({request: {kind: 'rigged_asset', concept: canonicalPilotB.concept}, artifacts});
}

test('canonical pilot binds GENERATE to the existing world build factory and declares its GLB', () => {
  const command = canonicalProductionAdapters(state()).GENERATE;
  assert.equal(command.command, 'blender');
  assert.deepEqual(command.args, ['-b', '--python-exit-code', '1', '--python', 'scripts/world_build_factory.py', '--', canonicalPilot.plan, canonicalPilot.root]);
  assert.deepEqual(command.artifacts, {raw: [canonicalPilot.glb]});
});

test('canonical Pilot B binds GENERATE to the existing reproducible rigged-asset builder', () => {
  const command = canonicalProductionAdapters(pilotBState({raw: []})).GENERATE;
  assert.equal(command.command, 'blender');
  assert.deepEqual(command.args, ['-b', '--python-exit-code', '1', '--python', 'scripts/build_astra_rigged_pilot.py', '--', canonicalPilotB.root]);
  assert.deepEqual(command.artifacts, {raw: [canonicalPilotB.glb]});
});

test('canonical Pilot B snapshots raw GLB into a distinct normalized artifact with before/after evidence', () => {
  const current = pilotBState({raw: [canonicalPilotB.glb]});
  const command = canonicalProductionAdapters(current).NORMALIZE({state: current});
  assert.equal(command.command, process.execPath);
  assert.deepEqual(command.args, ['scripts/normalize-production-artifact.mjs', canonicalPilotB.root, canonicalPilotB.glb, canonicalPilotB.normalizedGlb]);
  assert.notEqual(canonicalPilotB.glb, canonicalPilotB.normalizedGlb);
  assert.deepEqual(command.artifacts, {normalized: [canonicalPilotB.normalizedGlb], evidence: [canonicalPilotB.normalizationEvidence]});
});

test('canonical Pilot B binds VALIDATE_STATIC to its independent Blender verifier', () => {
  const current = pilotBState();
  const command = canonicalProductionAdapters(current).VALIDATE_STATIC({state: current});
  assert.equal(command.command, 'blender');
  assert.deepEqual(command.args, ['-b', '--python-exit-code', '1', '--python', 'scripts/verify_astra_rigged_pilot.py', '--', canonicalPilotB.root]);
});

test('canonical Pilot B binds RENDER to the generated six-view evidence verifier', () => {
  const current = pilotBState();
  const command = canonicalProductionAdapters(current).RENDER({state: current});
  assert.equal(command.command, process.execPath);
  assert.deepEqual(command.args, ['scripts/verify-production-renders.mjs', canonicalPilotB.root, ...canonicalPilotB.renders]);
  assert.deepEqual(command.artifacts, {renders: canonicalPilotB.renders.map((name) => `${canonicalPilotB.root}/${name}`)});
  assert.deepEqual(canonicalPilotB.renders, ['front_3_4.png', 'rear_3_4.png', 'left_side.png', 'right_side.png', 'top_overview.png', 'geometry_diagnostic.png']);
});

test('canonical Pilot B binds UNITY_IMPORT to its existing Unity GLB consumer verifier', () => {
  const current = pilotBState();
  const command = canonicalProductionAdapters(current).UNITY_IMPORT({state: current});
  assert.equal(command.command, process.execPath);
  assert.deepEqual(command.args, ['scripts/run-astra-rigged-pilot-unity.mjs', canonicalPilotB.root]);
  assert.deepEqual(command.artifacts, {unity: [canonicalPilotB.unityEvidence]});
});

test('canonical Pilot B adapters fail closed without canonical stage artifacts', () => {
  for (const artifacts of [{raw: []}, {raw: ['/tmp/untrusted.glb']}, {raw: [canonicalPilotB.glb], normalized: []}, {raw: [canonicalPilotB.glb], normalized: ['/tmp/untrusted.glb']}]) {
    const current = pilotBState(artifacts);
    const adapters = canonicalProductionAdapters(current);
    if (artifacts.raw[0] !== canonicalPilotB.glb) assert.equal(adapters.NORMALIZE({state: current}), null);
    assert.equal(adapters.VALIDATE_STATIC({state: current}), null);
    assert.equal(adapters.RENDER({state: current}), null);
    assert.equal(adapters.UNITY_IMPORT({state: current}), null);
  }
});

test('canonical pilot binds VALIDATE_STATIC to the independent Blender verifier', () => {
  const current = state();
  const command = canonicalProductionAdapters(current).VALIDATE_STATIC({state: current});
  assert.equal(command.command, 'blender');
  assert.deepEqual(command.args, ['-b', '--python-exit-code', '1', '--python', 'scripts/verify_world_build.py', '--', '.artifacts/world-builds/cyber-alley-tea-nook/build-plan.json', '.artifacts/world-builds/cyber-alley-tea-nook']);
});

test('canonical pilot binds RENDER to independent non-empty evidence verification', () => {
  const command = canonicalProductionAdapters(state()).RENDER;
  assert.equal(command.command, process.execPath);
  assert.deepEqual(command.args, ['scripts/verify-production-renders.mjs', canonicalPilot.root, ...canonicalPilot.renders]);
  assert.deepEqual(canonicalPilot.renders, ['view-hero.png', 'view-top.png', 'view-social-core.png', 'view-retreat.png', 'view-circulation.png']);
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
