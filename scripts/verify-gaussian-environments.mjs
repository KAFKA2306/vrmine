import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const path = new URL('../config/gaussian-environments.json', import.meta.url);
const document = JSON.parse(await readFile(path, 'utf8'));

assert.equal(document.version, 1, 'environment contract version must be 1');
assert.ok(Array.isArray(document.environments) && document.environments.length > 0, 'at least one environment is required');

const ids = new Set();
const statuses = new Set(['UNVERIFIED', 'PASS', 'FAIL']);
const targets = ['browser', 'unity', 'vrchat_pc', 'vrchat_android'];
const sha256 = /^[0-9a-f]{64}$/;
const commit = /^[0-9a-f]{40}$/;

for (const environment of document.environments) {
  assert.equal(typeof environment.id, 'string');
  assert.ok(environment.id.length > 0, 'environment id is required');
  assert.ok(!ids.has(environment.id), `duplicate environment id: ${environment.id}`);
  ids.add(environment.id);

  assert.equal(environment.type, 'gaussian-splat', `${environment.id}: unsupported type`);
  assert.equal(environment.format, 'ply', `${environment.id}: unsupported format`);

  assert.match(environment.source?.commit ?? '', commit, `${environment.id}: invalid source commit`);
  assert.match(environment.source?.sha256 ?? '', sha256, `${environment.id}: invalid source SHA-256`);
  assert.ok(Number.isSafeInteger(environment.source?.size_bytes) && environment.source.size_bytes > 0, `${environment.id}: invalid source size`);
  assert.ok(environment.source?.repository && environment.source?.path && environment.source?.url, `${environment.id}: source lineage is incomplete`);

  assert.ok(environment.provenance?.source_page, `${environment.id}: source page is required`);
  assert.ok(environment.provenance?.license?.name, `${environment.id}: license name is required`);
  assert.ok(environment.provenance?.license?.url, `${environment.id}: license URL is required`);

  for (const target of targets) {
    const value = environment.targets?.[target];
    assert.ok(value, `${environment.id}: missing target ${target}`);
    assert.ok(statuses.has(value.status), `${environment.id}/${target}: invalid status ${value.status}`);
    assert.ok(value.renderer, `${environment.id}/${target}: renderer is required`);
    assert.match(value.renderer_revision ?? '', commit, `${environment.id}/${target}: renderer revision must be a full commit SHA`);
  }
}

console.log(`Validated ${document.environments.length} Gaussian environment contract(s).`);
