import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { assertProductionEligible } from "./assert-astra-production-eligibility.mjs";

const root = path.resolve(import.meta.dirname, "..");
const spec = "config/world-items/cafe-bar-stool-01.json";
const compile = () => JSON.parse(execFileSync(process.execPath, ["scripts/compile-astra-toolchain-manifest.mjs", spec], { cwd: root, encoding: "utf8" }));
const first = compile();
const second = compile();
const request = JSON.parse(execFileSync(process.execPath, ["scripts/compile-astra-build-request.mjs", spec], { cwd: root, encoding: "utf8" }));
const canonicalSpec = JSON.parse(fs.readFileSync(path.join(root, spec), "utf8"));
const unityVersion = fs.readFileSync(path.join(root, "ProjectSettings/ProjectVersion.txt"), "utf8").match(/^m_EditorVersion: (.+)$/m)[1];
const sdkVersion = JSON.parse(fs.readFileSync(path.join(root, "Packages/manifest.json"), "utf8")).dependencies["com.vrchat.worlds"];
const head = execFileSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8" }).trim();
const policy = JSON.parse(fs.readFileSync(path.join(root, "config/astra-toolchain-policy.json"), "utf8"));

assert.deepEqual(first, second, "toolchain manifest must be deterministic for an exact HEAD");
assert.equal(first.$schema, "config/astra-toolchain-manifest.schema.json");
assert.equal(first.source.canonical_spec_sha256, request.source.sha256);
assert.equal(first.source.repository_commit, head);
assert.deepEqual(first.provenance, {
  generated_with: "gpt-6-astra",
  generation_method: "procedural",
  human_reviewed: false,
  source_asset_license: canonicalSpec.license.name,
  source_asset_license_status: canonicalSpec.license.status,
  source_asset_provenance: canonicalSpec.license.provenance,
  source_asset_urls: canonicalSpec.license.source_urls ?? []
});
assert.equal(first.policy.tier, "P0");
assert.deepEqual(first.policy.control_priority, request.toolchain_policy.control_priority);
assert.equal(first.policy.experimental, false);
assert.equal(first.tools.find((tool) => tool.name === "Unity")?.version, unityVersion);
assert.equal(first.tools.find((tool) => tool.name === "VRChat SDK Worlds")?.version, sdkVersion);
assert.deepEqual(Object.values(first.runtime).map((stage) => stage.status), Array(5).fill("UNVERIFIED"));
assert.equal(assertProductionEligible(first, policy).status, "PASS");

const p1 = structuredClone(first);
p1.policy.tier = "P1";
p1.tools.push({
  name: "external-mesh-generator",
  version: "1",
  role: "mesh_generation",
  control_mode: "code",
  tier: "P1",
  source: "https://example.invalid/generator",
  license: "commercial-use-permitted",
  exact_revision: "asset-sha256:0123456789abcdef"
});
assert.equal(assertProductionEligible(p1, policy).status, "PASS");
for (const field of ["source", "license", "exact_revision"]) {
  const invalid = structuredClone(p1);
  invalid.tools.at(-1)[field] = null;
  assert.throws(() => assertProductionEligible(invalid, policy), /requires source, license, and exact_revision provenance/);
}

const experimental = structuredClone(first);
experimental.policy.tier = "P2";
experimental.policy.experimental = true;
experimental.tools[0].tier = "P2";
assert.throws(() => assertProductionEligible(experimental, policy), /not production eligible/);
console.log("astra toolchain manifest contract: PASS");
