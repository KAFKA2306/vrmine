import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const allowedStatuses = new Set(['UNVERIFIED', 'PASS', 'FAIL']);
const requiredTargets = ['browser', 'unity', 'vrchat_pc', 'vrchat_android'];
const config = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));

assert.equal(config.schema_version, 1, 'unsupported gaussian fixture schema');
assert.ok(Array.isArray(config.environments), 'environments must be an array');
assert.equal(config.environments.length, 1, 'one canonical fixture is expected until another consumer is proven');

const ids = new Set();
for (const environment of config.environments) {
  assert.equal(typeof environment.id, 'string');
  assert.ok(environment.id.length > 0, 'environment id is required');
  assert.ok(!ids.has(environment.id), `duplicate environment id: ${environment.id}`);
  ids.add(environment.id);

  assert.equal(environment.type, 'gaussian-splat');
  assert.equal(environment.format, 'ply');
  assert.match(environment.source.commit, /^[0-9a-f]{40}$/);
  assert.match(environment.source.sha256, /^[0-9a-f]{64}$/);
  assert.ok(Number.isInteger(environment.source.size_bytes) && environment.source.size_bytes > 0);
  assert.ok(environment.source.repository);
  assert.ok(environment.source.path.endsWith('.ply'));
  assert.ok(environment.source.raw_url.startsWith('https://raw.githubusercontent.com/'));

  const provenance = environment.source.provenance;
  assert.ok(provenance.catalog);
  assert.ok(provenance.title);
  assert.ok(provenance.source_page.startsWith('https://commons.wikimedia.org/'));
  assert.ok(provenance.author);
  assert.ok(provenance.license);
  assert.ok(provenance.license_url.startsWith('https://'));

  assert.deepEqual(Object.keys(environment.targets).sort(), [...requiredTargets].sort());
  for (const target of requiredTargets) {
    const entry = environment.targets[target];
    assert.ok(allowedStatuses.has(entry.status), `${target} has invalid status ${entry.status}`);
    assert.ok(entry.renderer, `${target} renderer is required`);
  }

  assert.deepEqual(environment.transform.position.length, 3);
  assert.deepEqual(environment.transform.rotation_euler_degrees.length, 3);
  assert.deepEqual(environment.transform.scale.length, 3);
}

console.log(`Validated ${config.environments.length} Gaussian Splat fixture(s): ${[...ids].join(', ')}`);
