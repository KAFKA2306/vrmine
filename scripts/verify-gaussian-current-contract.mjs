import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const exhibition = JSON.parse(await readFile('config/gaussian-exhibition.json', 'utf8'));
const registry = JSON.parse(await readFile('config/gaussian-splats.json', 'utf8'));
const basis = JSON.parse(await readFile('config/gaussian-basis-contract.json', 'utf8'));
const materializer = await readFile('scripts/materialize-gaussian-sources.mjs', 'utf8');
const compiler = await readFile('scripts/compile-gaussian-producer-orientation.mjs', 'utf8');
const workflow = await readFile('.github/workflows/3dgs-contracts.yml', 'utf8');

const retiredMarker = ['audited-', 'leg', 'acy-basis'].join('');
const retiredSourceMode = ['leg', 'acy-url'].join('');
const retiredWord = ['leg', 'acy'].join('');

assert.equal(Object.hasOwn(exhibition, 'final_expected_exhibits'), false);
assert.equal(exhibition.import_overrides.length, registry.environments.length);
assert.equal(basis.status, 'accepted');
assert.equal(basis.scope, 'coordinate_basis_only');
assert.equal(basis.physical_up.status, 'review_required');
assert.equal(basis.canonical_frame.physical_gravity_claimed, false);

for (const entry of exhibition.import_overrides) {
  assert.equal(entry.alignment.authority, 'artifact-set-basis-contract');
  assert.notEqual(entry.alignment.authority, retiredMarker);
}

assert.equal(compiler.includes(retiredMarker), false);
assert.equal(compiler.toLowerCase().includes(retiredWord), false);
assert.equal(materializer.includes(retiredSourceMode), false);
assert.equal(materializer.includes('download_url'), false);
assert.equal(workflow.includes('STATIC_HF_TOKEN'), false);
assert.equal(workflow.includes('secrets.HF_TOKEN'), false);
assert.equal(workflow.includes('repository_secret'), false);

console.log(`Gaussian current-only contract PASS: registered=${registry.environments.length}, artifact resolver only, explicit basis contract, OIDC only`);
