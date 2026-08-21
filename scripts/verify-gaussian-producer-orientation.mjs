import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

import { compileProducerOrientation } from './compile-gaussian-producer-orientation.mjs';

const registry = JSON.parse(await readFile('config/gaussian-splats.json', 'utf8'));
const exhibition = JSON.parse(await readFile('config/gaussian-exhibition.json', 'utf8'));
const result = compileProducerOrientation(registry, exhibition);

assert.ok(registry.environments.length >= 1, 'expected at least one Gaussian registry entry');
assert.equal(result.compiled_count, registry.environments.length, 'all registered artifacts require a deterministic basis override');
assert.equal(result.unresolved.length, 0, 'no coordinate-basis migration may remain unresolved');
assert.equal(result.physical_up_counts.review_required, registry.environments.length, 'basis audit must not claim physical gravity');
assert.deepEqual(Object.keys(result.physical_up_counts), ['review_required'], 'unexpected physical-up status in registry');
assert.ok(Object.values(result.authorities).every((authority) => ['producer-artifact-metadata', 'audited-legacy-basis'].includes(authority)));
assert.deepEqual(
  result.exhibition.import_overrides,
  exhibition.import_overrides,
  'committed import_overrides must exactly match the deterministic compiler output',
);
for (const entry of exhibition.import_overrides) {
  assert.equal(entry.alignment.scope, 'coordinate_basis_only');
  assert.equal(entry.alignment.physicalUpStatus, 'review_required');
  assert.equal(
    entry.alignment.authority,
    entry.id === 'nordic' ? 'producer-artifact-metadata' : 'audited-legacy-basis',
  );
}

console.log(`Gaussian producer orientation migration PASS: basis=${registry.environments.length} physical_up_review_required=${result.physical_up_counts.review_required}`);
