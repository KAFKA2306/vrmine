import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const sourcePath = 'config/world-design/generated/cyber-alley-tea-nook.json';
const consumerPath = 'scripts/world-design-to-blender-plan.mjs';
const source = JSON.parse(fs.readFileSync(sourcePath, 'utf8'));

const clone = () => structuredClone(source);
const run = (spec) => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'vrmine-world-drc-'));
  const specPath = path.join(dir, 'spec.json');
  fs.writeFileSync(specPath, JSON.stringify(spec));
  try {
    return execFileSync(process.execPath, [consumerPath, specPath], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
};

const expectFail = (name, mutate, expected) => {
  const spec = clone();
  mutate(spec);
  let stderr = '';
  try {
    run(spec);
    assert.fail(`${name}: destructive fixture unexpectedly passed`);
  } catch (error) {
    if (error?.code === 'ERR_ASSERTION') throw error;
    stderr = String(error.stderr ?? error.message ?? '');
  }
  assert.match(stderr, expected, `${name}: wrong failure boundary`);
};

const baseline = JSON.parse(run(clone()));
assert.equal(baseline.source_spec_sha256.length, 64);
assert.ok(baseline.social_core.seats.length >= 3);
assert.ok(baseline.circulation_contract.waypoints_m.length >= 2);

expectFail('spawn outside room', (spec) => {
  spec.world_build.spawn.position_m[0] = spec.spatial_geometry.overall_width_m;
}, /spawn lies outside room bounds/);

expectFail('circulation clearance', (spec) => {
  spec.world_build.circulation.waypoints_m[0][0] = spec.spatial_geometry.overall_width_m / 2 - 0.01;
}, /violates half-clearance from room wall/);

expectFail('activity unreachable', (spec) => {
  spec.world_build.activity_anchor.position_m = [spec.spatial_geometry.overall_width_m / 2 - 0.1, spec.spatial_geometry.overall_depth_m / 2 - 0.1, 0];
  spec.world_build.activity_anchor.approach_clearance_m = 0.01;
}, /circulation does not reach the activity anchor approach zone/);

expectFail('retreat unreachable', (spec) => {
  spec.world_build.retreat.center_m = [-spec.spatial_geometry.overall_width_m / 2 + 0.1, spec.spatial_geometry.overall_depth_m / 2 - 0.1, 0];
  spec.world_build.circulation.minimum_clearance_m = 0.1;
}, /circulation does not reach the retreat zone/);

expectFail('invalid hero camera', (spec) => {
  spec.world_build.hero_view.position_m[1] = spec.spatial_geometry.overall_depth_m;
}, /hero_view\.position lies outside room bounds/);

console.log(JSON.stringify({
  status: 'PASS',
  baseline: sourcePath,
  destructive_fixtures: 5,
  verified_failures: [
    'spawn-outside-room',
    'circulation-clearance',
    'activity-unreachable',
    'retreat-unreachable',
    'invalid-hero-camera'
  ]
}, null, 2));
