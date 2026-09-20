import fs from 'node:fs';
import assert from 'node:assert/strict';

const c = JSON.parse(fs.readFileSync('config/world-performance.json', 'utf8'));
assert.equal(c.schema_version, 1);
assert.equal(c.authority, 'measured_profiler_evidence');
for (const metric of ['cpu_frame_ms','gpu_frame_ms','draw_calls','udon_frame_ms','physbone_frame_ms','skinned_meshes','mirror_frame_ms','probe_frame_ms','occlusion_frame_ms','network_sync_bytes_per_sec','texture_memory_mb']) assert(c.metrics.includes(metric), `missing metric: ${metric}`);
for (const platform of ['pc','android']) {
  const b = c.platforms[platform];
  assert(b.cpu_frame_ms_max > 0 && b.gpu_frame_ms_max > 0, `${platform} frame thresholds must be positive`);
  assert(b.texture_memory_mb_max > 0, `${platform} texture budget must be positive`);
}
assert.notDeepEqual(c.platforms.pc, c.platforms.android, 'PC and Android budgets must be distinct');
assert.equal(c.baseline.scene, 'canonical_world');
assert(c.baseline.required_samples >= 60, 'baseline needs representative sample count');
assert.equal(c.baseline.missing_measurement, 'UNVERIFIED');
assert(c.regression.relative_tolerance > 0 && c.regression.relative_tolerance <= 0.25, 'regression tolerance must be bounded');
assert.deepEqual(c.regression.states, ['PASS','FAIL','UNVERIFIED']);
assert.equal(c.regression.require_same_platform, true);
assert.equal(c.regression.require_exact_revision, true);
assert.equal(c.bottleneck.separate_cpu_gpu, true);
assert(c.bottleneck.cpu_bound_when_cpu_over_gpu_ratio > 1);
assert(c.bottleneck.gpu_bound_when_gpu_over_cpu_ratio > 1);
console.log('world performance contract: PASS');
