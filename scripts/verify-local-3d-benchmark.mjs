import fs from 'node:fs';
import assert from 'node:assert/strict';

const path = process.argv[2] ?? 'config/local-3d-benchmark.json';
const contract = JSON.parse(fs.readFileSync(path, 'utf8'));

assert.equal(contract.authority_issue, 431);
assert.equal(contract.status, 'UNVERIFIED', 'benchmark must remain UNVERIFIED until measured evidence exists');
assert.ok(contract.reference_set.length >= 8, 'reference set must cover the fixed production cases');
const categories = new Set(contract.reference_set.map(x => x.category));
for (const category of ['furniture','plant','foodware','industrial','japanese-street','asymmetric','thin-structure','multipart']) assert.ok(categories.has(category), `missing reference category ${category}`);
assert.equal(new Set(contract.reference_set.map(x => x.id)).size, contract.reference_set.length, 'reference ids must be unique');

for (const backend of ['triposr','hunyuan3d-2.1','trellis.2','pixal3d','kaininja']) assert.ok(contract.backends[backend], `missing backend ${backend}`);
assert.equal(contract.backends.kaininja.availability, 'UNAVAILABLE');
assert.equal(contract.backends.kaininja.evidence_required, true);

for (const artifact of ['input','raw','processed','renders','metrics.json','manifest.json']) assert.ok(contract.required_artifacts.includes(artifact), `missing artifact ${artifact}`);
for (const field of ['backend','model_version','commit','seed','local','license','source_url']) assert.ok(contract.required_provenance.includes(field), `missing provenance ${field}`);
for (const metric of ['geometry.reference_fidelity','geometry.non_manifold','geometry.degenerate_faces','geometry.triangles','production.uv','production.pbr_channels','production.semantic_parts','runtime.peak_vram_mb','runtime.elapsed_sec']) assert.ok(contract.required_metrics.includes(metric), `missing metric ${metric}`);

assert.deepEqual(contract.gpu_profiles.map(x => x.vram_gb).sort((a,b)=>a-b), [16,48]);
assert.ok(contract.promotion.minimum_compared_backends >= 3);
assert.equal(contract.promotion.requires_unity_verification, true);
assert.equal(contract.promotion.recommended_backend, null, 'do not invent a benchmark winner');

console.log(JSON.stringify({status:'PASS', authority_issue:431, references:contract.reference_set.length, backends:Object.keys(contract.backends).length}));
