import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

import { compileProducerOrientation } from './compile-gaussian-producer-orientation.mjs';

const registry = JSON.parse(await readFile('config/gaussian-splats.json', 'utf8'));
const exhibition = JSON.parse(await readFile('config/gaussian-exhibition.json', 'utf8'));
assert.equal(exhibition.basis_contract, 'config/gaussian-basis-contract.json');
const basisContract = JSON.parse(await readFile(exhibition.basis_contract, 'utf8'));
const result = compileProducerOrientation(registry, exhibition, { basisContract });
const registeredCount = registry.environments.length;

assert.ok(registeredCount > 0, 'Gaussian registry must contain at least one artifact');
assert.equal(result.compiled_count, registeredCount, 'every registered artifact requires a deterministic basis override');
assert.equal(result.unresolved.length, 0, 'no coordinate-basis migration may remain unresolved');
assert.equal(
  result.physical_up_counts.review_required,
  registeredCount,
  'basis contract must not claim physical gravity for any registered artifact',
);
assert.equal(Object.keys(result.physical_up_counts).length, 1, 'unexpected physical-up status in current artifact set');
assert.deepEqual(
  result.exhibition.import_overrides,
  exhibition.import_overrides,
  'committed import_overrides must exactly match the deterministic compiler output',
);
for (const entry of exhibition.import_overrides) {
  assert.equal(entry.alignment.scope, 'coordinate_basis_only');
  assert.equal(entry.alignment.physicalUpStatus, 'review_required');
  assert.equal(entry.alignment.authority, 'artifact-set-basis-contract');
}

console.log(
  `Gaussian producer orientation contract PASS: basis=${registeredCount} physical_up_review_required=${registeredCount}`,
);
