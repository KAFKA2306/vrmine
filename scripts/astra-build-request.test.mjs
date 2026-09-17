import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execFileSync } from "node:child_process";

const root = path.resolve(import.meta.dirname, "..");
const spec = "config/world-items/cafe-bar-stool-01.json";
const run = () => JSON.parse(execFileSync(process.execPath, ["scripts/compile-astra-build-request.mjs", spec], { cwd: root, encoding: "utf8" }));
const first = run();
const second = run();

assert.deepEqual(first, second, "same canonical spec must compile deterministically");
assert.equal(first.kind, "astra_build_request");
assert.equal(first.source.canonical_spec, spec);
assert.equal(first.intent.id, "cafe-bar-stool-01");
assert.deepEqual(first.intent.dimensions_m, [0.42, 0.42, 0.74]);
assert.equal(first.constraints.generation_method, "procedural");
assert.equal(first.constraints.manual_mesh_edit_as_authority, false);
assert.ok(Number.isInteger(first.constraints.deterministic_seed));
assert.equal(first.verification.independent_static_gate, true);
assert.equal(first.verification.visual_loop.repair_on_fail, true);
assert.equal(first.verification.platforms.android, "UNVERIFIED");
assert.equal(first.toolchain_policy.production_tier, "P0");
assert.ok(first.constraints.license.provenance.length > 0);
assert.match(first.source.sha256, /^[0-9a-f]{64}$/);

const out = path.join(os.tmpdir(), `vrmine-astra-${process.pid}.json`);
execFileSync(process.execPath, ["scripts/compile-astra-build-request.mjs", spec, out], { cwd: root });
assert.deepEqual(JSON.parse(fs.readFileSync(out, "utf8")), first);
fs.rmSync(out, { force: true });
console.log("astra build request contract: PASS");
