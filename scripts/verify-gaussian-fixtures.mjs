import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const allowedStatuses = new Set(['UNVERIFIED', 'PASS', 'FAIL', 'BLOCKED']);
const requiredTargets = ['browser', 'unity', 'vrchat_pc', 'vrchat_android'];
const config = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));

assert.equal(config.schema_version, 2, 'unsupported gaussian fixture schema');
assert.equal(config.source_repository, 'KAFKA2306/AutoPhotogrammetry');
assert.match(config.source_commit, /^[0-9a-f]{40}$/);
assert.equal(config.source_catalog, 'sources/videos.json');
assert.ok(config.renderers.browser);
assert.ok(config.renderers.unity_vrchat);
assert.ok(Array.isArray(config.environments), 'environments must be an array');
assert.equal(config.environments.length, 20, 'final exhibition contract must contain exactly 20 entries');

const ids = new Set();
const hashes = new Set();
const displayIndexes = new Set();
for (const environment of config.environments) {
  assert.equal(typeof environment.id, 'string');
  assert.ok(environment.id.length > 0, 'environment id is required');
  assert.ok(!ids.has(environment.id), `duplicate environment id: ${environment.id}`);
  ids.add(environment.id);

  assert.ok(Number.isInteger(environment.display_index), `${environment.id} display_index must be an integer`);
  assert.ok(environment.display_index >= 1 && environment.display_index <= 20, `${environment.id} display_index out of range`);
  assert.ok(!displayIndexes.has(environment.display_index), `duplicate display_index: ${environment.display_index}`);
  displayIndexes.add(environment.display_index);

  assert.equal(environment.type, 'gaussian-splat');
  assert.equal(environment.format, 'ply');
  assert.equal(environment.presentation_target_extent_m, 1.0, `${environment.id} presentation target must be 1m`);
  assert.equal(environment.upstream?.video_id, environment.id, `${environment.id} must trace directly to the same upstream video id`);
  assert.equal(Object.hasOwn(environment.source, 'provenance'), false, `${environment.id} must not duplicate upstream provenance/license data`);

  assert.match(environment.source.sha256, /^[0-9a-f]{64}$/);
  assert.ok(!hashes.has(environment.source.sha256), `duplicate source hash: ${environment.source.sha256}`);
  hashes.add(environment.source.sha256);
  assert.ok(Number.isInteger(environment.source.size_bytes) && environment.source.size_bytes > 0);
  assert.ok(environment.source.path.endsWith('.ply'));
  assert.equal(environment.source.artifact_id, `autophotogrammetry/${environment.id}/splat`);

  assert.equal(environment.playback?.status, 'ready_untrusted', `${environment.id} playback must be ready_untrusted`);
  assert.equal(environment.playback?.requires_untrusted_urls, true, `${environment.id} Wikimedia playback requires untrusted URLs`);
  assert.match(environment.playback?.url ?? '', /^https:\/\/upload\.wikimedia\.org\//, `${environment.id} playback URL must be a Wikimedia HTTPS media URL`);

  assert.deepEqual(Object.keys(environment.targets).sort(), [...requiredTargets].sort(), `${environment.id} must define all target states`);
  for (const target of requiredTargets) {
    const entry = environment.targets[target];
    assert.ok(allowedStatuses.has(entry.status), `${environment.id}/${target} has invalid status ${entry.status}`);
    assert.equal(entry.status, 'UNVERIFIED', `${environment.id}/${target} must remain UNVERIFIED until target-specific runtime evidence exists`);
    assert.ok(config.renderers[entry.renderer], `${environment.id}/${target} references unknown renderer ${entry.renderer}`);
  }
}

assert.deepEqual([...displayIndexes].sort((a, b) => a - b), Array.from({ length: 20 }, (_, index) => index + 1));
console.log(`Validated canonical Gaussian exhibition manifest: entries=${config.environments.length}, provenance_duplicates=0, playback_ready_untrusted=20`);
