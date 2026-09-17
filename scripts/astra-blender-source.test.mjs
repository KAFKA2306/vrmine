import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execFileSync } from "node:child_process";

const root = path.resolve(import.meta.dirname, "..");
const spec = "config/world-items/cafe-side-table-01.json";
const compile = (out) => execFileSync(process.execPath, ["scripts/compile-astra-blender-source.mjs", spec, out], { cwd: root });
const first = fs.mkdtempSync(path.join(os.tmpdir(), "vrmine-astra-source-a-"));
const second = fs.mkdtempSync(path.join(os.tmpdir(), "vrmine-astra-source-b-"));
compile(first);
compile(second);

for (const name of ["parameters.json", "generate.py", "source-manifest.json"]) {
  assert.deepEqual(fs.readFileSync(path.join(first, name)), fs.readFileSync(path.join(second, name)), `${name} must be deterministic`);
}
const parameters = JSON.parse(fs.readFileSync(path.join(first, "parameters.json"), "utf8"));
const manifest = JSON.parse(fs.readFileSync(path.join(first, "source-manifest.json"), "utf8"));
const source = fs.readFileSync(path.join(first, "generate.py"), "utf8");
assert.equal(parameters.kind, "astra_blender_parameters");
assert.equal(parameters.source.canonical_spec, spec);
assert.deepEqual(parameters.dimensions_m, [0.62, 0.62, 0.72]);
assert.ok(Number.isInteger(parameters.deterministic_seed));
assert.equal(parameters.generator_authority, "scripts/world_item_factory.py");
assert.equal(manifest.kind, "astra_blender_source");
assert.equal(manifest.deterministic_seed, parameters.deterministic_seed);
assert.deepEqual(manifest.artifacts, ["parameters.json", "generate.py"]);
for (const name of manifest.artifacts) {
  const digest = crypto.createHash("sha256").update(fs.readFileSync(path.join(first, name))).digest("hex");
  assert.equal(manifest.sha256[name], digest);
}
assert.match(source, /EXPECTED_SPEC_SHA256/);
assert.match(source, /world_item_factory\.py/);
assert.match(source, /runpy\.run_path/);
fs.rmSync(first, { recursive: true, force: true });
fs.rmSync(second, { recursive: true, force: true });
console.log("astra Blender source package contract: PASS");
