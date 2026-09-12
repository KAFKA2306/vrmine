import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';

const packagePath = 'config/world-items/packages/index.json';
const config = JSON.parse(readFileSync(packagePath, 'utf8'));
assert.equal(config.schema_version, 1, 'unsupported package schema');
assert.ok(Array.isArray(config.packages) && config.packages.length >= 3, 'packages missing');

const allowedKinds = new Set(['single', 'mini_set', 'theme_pack']);
const ids = new Set();
const kinds = new Set();
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
  }
}
for (const kind of allowedKinds) assert.ok(kinds.has(kind), `missing package kind: ${kind}`);

console.log(`PASS world-item packages: ${config.packages.length} packages; single/mini_set/theme_pack covered; all item/world references resolve`);
