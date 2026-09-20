import assert from "node:assert/strict";
import fs from "node:fs";

const policy = JSON.parse(fs.readFileSync(new URL("../config/geometry-nodes-generation.json", import.meta.url), "utf8"));

assert.equal(policy.schema_version, 1);
assert.equal(policy.authority, "geometry_nodes_generation");
assert.equal(policy.generator.backend, "geometry_nodes");
assert.equal(policy.generator.tier, "P0");
assert.equal(policy.generator.source_required, true);
assert.equal(policy.generator.deterministic_seed_required, true);
assert.deepEqual(policy.generator.seed_range, [0, 4294967295]);
assert.equal(policy.shared_material.required, true);
assert.equal(policy.collision.proxy_required, true);
assert.equal(policy.collision.separate_from_render_mesh, true);
assert.equal(policy.unity_import.preset, "canonical_glb_consumer");
assert.equal(policy.unity_import.verification_task, "glb:verify-u2");

const entries = Object.entries(policy.categories);
assert.ok(entries.length >= 3, "at least three production categories are required");
const seeds = new Set();
for (const [name, category] of entries) {
  assert.ok(Number.isInteger(category.seed), `${name}: seed must be an integer`);
  assert.ok(category.seed >= policy.generator.seed_range[0] && category.seed <= policy.generator.seed_range[1], `${name}: seed out of range`);
  assert.ok(!seeds.has(category.seed), `${name}: seed must be category-unique`);
  seeds.add(category.seed);
  assert.ok(Array.isArray(category.exposed_parameters) && category.exposed_parameters.length >= 3, `${name}: exposed parameters required`);
  assert.equal(new Set(category.exposed_parameters).size, category.exposed_parameters.length, `${name}: duplicate exposed parameter`);
}

for (const artifact of ["procedural_source", "parameters", "blend", "glb", "collision_proxy", "manifest", "render_evidence"]) {
  assert.ok(policy.artifact_contract.required.includes(artifact), `missing artifact contract: ${artifact}`);
}
assert.equal(policy.artifact_contract.runtime_without_evidence, "UNVERIFIED");

console.log(`Geometry Nodes generation contract PASS (${entries.map(([name]) => name).join(", ")})`);
