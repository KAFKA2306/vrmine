import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const allowedStatuses = new Set(['UNVERIFIED', 'PASS', 'FAIL']);
const allowedLicenseStatuses = new Set(['verified', 'needs_review']);
const requiredTargets = ['browser', 'unity', 'vrchat_pc', 'vrchat_android'];
const config = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));

assert.equal(config.schema_version, 1, 'unsupported gaussian fixture schema');
assert.equal(config.source_repository, 'KAFKA2306/AutoPhotogrammetry');
assert.match(config.source_commit, /^[0-9a-f]{40}$/);
assert.ok(config.renderers.browser);
assert.ok(config.renderers.unity_vrchat);
assert.ok(Array.isArray(config.environments), 'environments must be an array');
assert.equal(config.environments.length, 9, 'all nine measured AutoPhotogrammetry exports must be registered');

assert.deepEqual(Object.keys(config.defaults.targets).sort(), [...requiredTargets].sort());
for (const target of requiredTargets) {
  const entry = config.defaults.targets[target];
  assert.ok(allowedStatuses.has(entry.status), `${target} has invalid default status ${entry.status}`);
  assert.equal(entry.status, 'UNVERIFIED', `${target} must remain UNVERIFIED until target-specific runtime evidence exists`);
  assert.ok(config.renderers[entry.renderer], `${target} references unknown renderer ${entry.renderer}`);
}
for (const key of ['position', 'rotation_euler_degrees', 'scale']) {
  assert.equal(config.defaults.transform[key].length, 3, `${key} must contain three values`);
}

const ids = new Set();
const hashes = new Set();
let rightsReady = 0;
let rightsBlocked = 0;
for (const environment of config.environments) {
  assert.equal(typeof environment.id, 'string');
  assert.ok(environment.id.length > 0, 'environment id is required');
  assert.ok(!ids.has(environment.id), `duplicate environment id: ${environment.id}`);
  ids.add(environment.id);

  assert.equal(environment.type, 'gaussian-splat');
  assert.equal(environment.format, 'ply');
  assert.match(environment.source.sha256, /^[0-9a-f]{64}$/);
  assert.ok(!hashes.has(environment.source.sha256), `duplicate source hash: ${environment.source.sha256}`);
  hashes.add(environment.source.sha256);
  assert.ok(Number.isInteger(environment.source.size_bytes) && environment.source.size_bytes > 0);
  assert.ok(environment.source.path.endsWith('.ply'));

  const provenance = environment.source.provenance;
  assert.equal(provenance.catalog, 'KAFKA2306/AutoPhotogrammetry:sources/videos.json');
  assert.ok(provenance.title);
  assert.ok(provenance.source_page.startsWith('https://commons.wikimedia.org/'));
  assert.ok(allowedLicenseStatuses.has(provenance.license_status), `${environment.id} has invalid license status`);
  if (provenance.license_status === 'verified') {
    rightsReady++;
    assert.ok(provenance.author, `${environment.id} verified provenance needs author`);
    assert.ok(provenance.license, `${environment.id} verified provenance needs license`);
    assert.ok(provenance.license_url?.startsWith('https://'), `${environment.id} verified provenance needs license URL`);
  } else {
    rightsBlocked++;
    assert.equal(provenance.license, null, `${environment.id} unverified license must not be guessed`);
    assert.equal(provenance.license_url, null, `${environment.id} unverified license URL must not be guessed`);
  }
}

assert.equal(rightsReady, 5, 'expected five sources with directly verified rights evidence');
assert.equal(rightsBlocked, 4, 'expected four sources still requiring exact-file rights review');
console.log(`Validated ${config.environments.length} Gaussian Splat sources: rights verified=${rightsReady}, needs_review=${rightsBlocked}`);
