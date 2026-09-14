import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

const packagePath = 'config/world-items/packages/index.json';
const config = JSON.parse(readFileSync(packagePath, 'utf8'));
assert.equal(config.schema_version, 1, 'unsupported package schema');
assert.ok(Array.isArray(config.packages) && config.packages.length >= 3, 'packages missing');

const sha256 = (filePath) => createHash('sha256').update(readFileSync(filePath)).digest('hex');
const allowedKinds = new Set(['single', 'mini_set', 'theme_pack']);
const ids = new Set();
const kinds = new Set();
const verifiedItems = new Set();

function verifyItemIdentity(item, required) {
  if (verifiedItems.has(item)) return;
  const specPath = path.join('config', 'world-items', `${item}.json`);
  const pagesPath = path.join('pages', 'io', 'items', item);
  const manifestPath = path.join(pagesPath, 'manifest.json');
  if (!existsSync(manifestPath) && !required) return;

  const stagedSpecPath = path.join(pagesPath, 'spec.json');
  const buildInputPath = path.join(pagesPath, 'build-input.sha256');
  for (const identityPath of [specPath, manifestPath, stagedSpecPath, buildInputPath]) {
    assert.ok(existsSync(identityPath), `${item}: package identity input missing: ${identityPath}`);
  }

  const spec = JSON.parse(readFileSync(specPath, 'utf8'));
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
  assert.equal(spec.id, item, `${item}: canonical spec id mismatch`);
  assert.equal(manifest.id, item, `${item}: Pages manifest id mismatch`);
  assert.equal(manifest.source_spec, specPath.replaceAll(path.sep, '/'), `${item}: manifest source_spec mismatch`);
  assert.equal(manifest.spec_sha256, sha256(specPath), `${item}: manifest canonical spec digest mismatch`);
  assert.equal(sha256(stagedSpecPath), sha256(specPath), `${item}: Pages spec differs from canonical spec`);

  const buildInput = readFileSync(buildInputPath, 'utf8').trim();
  assert.match(buildInput, /^[0-9a-f]{64}$/, `${item}: invalid build-input identity`);
  assert.ok(manifest.sha256 && typeof manifest.sha256 === 'object', `${item}: artifact hash manifest missing`);
  for (const [name, expected] of Object.entries(manifest.sha256)) {
    assert.match(name, /^[^/\\]+$/, `${item}: non-local artifact path in manifest: ${name}`);
    assert.match(expected, /^[0-9a-f]{64}$/, `${item}: invalid artifact digest: ${name}`);
    const artifactPath = path.join(pagesPath, name);
    assert.ok(existsSync(artifactPath), `${item}: packaged artifact missing: ${name}`);
    assert.equal(sha256(artifactPath), expected, `${item}: packaged artifact digest mismatch: ${name}`);
  }

  assert.ok(spec.license && typeof spec.license === 'object', `${item}: license/provenance missing`);
  assert.ok(typeof spec.license.provenance === 'string' && spec.license.provenance.trim(), `${item}: provenance missing`);
  assert.ok(typeof spec.license.status === 'string' && spec.license.status.trim(), `${item}: license status missing`);
  assert.notEqual(spec.license.status, 'FAILED', `${item}: license gate failed`);
  verifiedItems.add(item);
}

for (const pack of config.packages) {
  assert.match(pack.id, /^[a-z0-9-]+$/, `invalid package id: ${pack.id}`);
  assert.ok(!ids.has(pack.id), `duplicate package id: ${pack.id}`);
  ids.add(pack.id);
  assert.ok(allowedKinds.has(pack.kind), `${pack.id}: invalid kind`);
  kinds.add(pack.kind);
  assert.ok(Array.isArray(pack.items) && pack.items.length > 0, `${pack.id}: items missing`);
  assert.equal(new Set(pack.items).size, pack.items.length, `${pack.id}: duplicate item`);
  if (pack.kind === 'single') assert.equal(pack.items.length, 1, `${pack.id}: single must contain exactly one item`);
  if (pack.kind === 'mini_set') assert.ok(pack.items.length >= 3, `${pack.id}: mini_set requires at least three items`);
  if (pack.kind === 'theme_pack') {
    assert.ok(pack.items.length >= 5, `${pack.id}: theme_pack requires at least five items`);
    assert.equal(typeof pack.world_design, 'string', `${pack.id}: world_design missing`);
    assert.ok(existsSync(`config/world-design/generated/${pack.world_design}.json`), `${pack.id}: unknown world_design ${pack.world_design}`);
  }
  for (const item of pack.items) {
    assert.ok(existsSync(`config/world-items/${item}.json`), `${pack.id}: unknown item ${item}`);
    verifyItemIdentity(item, pack.kind === 'single');
  }
}
for (const kind of allowedKinds) assert.ok(kinds.has(kind), `missing package kind: ${kind}`);

console.log(`PASS world-item packages: ${config.packages.length} packages; ${verifiedItems.size} materialized packaged items bound to canonical specs, Pages manifests, build identities, provenance, and artifact hashes; single packages require materialization`);
