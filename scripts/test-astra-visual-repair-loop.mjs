#!/usr/bin/env node
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { runVisualRepairLoop } from "./run-astra-visual-repair-loop.mjs";

const dir = fs.mkdtempSync(path.join(os.tmpdir(), "vrmine-repair-"));
const checks = ["scale_dimensions", "floating_parts", "mesh_intersection", "topology", "material_assignment", "texture_uv", "rig_joints", "colliders", "camera_clipping", "layout_obstruction"];
const verdict = (artifact, render, failing = false) => ({
  schema_version: 1, kind: "astra_visual_verdict", artifact_sha256: artifact,
  render_sha256: { front_3_4: render },
  checks: Object.fromEntries(checks.map((name) => [name, { status: failing && name === "mesh_intersection" ? "FAIL" : "PASS", evidence: `${name} inspected` }])),
  verdict: failing ? "FAIL" : "PASS",
  repair: { required: failing, instructions: failing ? ["separate intersecting mesh parts"] : [] }
});
const initial = path.join(dir, "initial.json");
const repaired = path.join(dir, "repaired.json");
fs.writeFileSync(initial, JSON.stringify(verdict("a".repeat(64), "b".repeat(64), true)));
fs.writeFileSync(repaired, JSON.stringify(verdict("c".repeat(64), "d".repeat(64), false)));
const calls = [];
const result = runVisualRepairLoop({ initialEvidence: initial, repairedEvidence: repaired, repairCommand: ["repair-tool", "--apply"], rerenderCommand: ["render-tool", "--all-views"], executor: (command) => calls.push(command) });
assert.deepEqual(calls, [["repair-tool", "--apply", "separate intersecting mesh parts"], ["render-tool", "--all-views"]]);
assert.deepEqual(result, { verdict: "PASS", repaired: true, attempts: 1, artifact_sha256: "c".repeat(64) });

const alreadyPass = path.join(dir, "pass.json");
fs.writeFileSync(alreadyPass, JSON.stringify(verdict("e".repeat(64), "f".repeat(64), false)));
assert.equal(runVisualRepairLoop({ initialEvidence: alreadyPass }).repaired, false);

const unchanged = path.join(dir, "unchanged.json");
fs.writeFileSync(unchanged, JSON.stringify(verdict("a".repeat(64), "d".repeat(64), false)));
assert.throws(() => runVisualRepairLoop({ initialEvidence: initial, repairedEvidence: unchanged, repairCommand: ["repair"], rerenderCommand: ["render"], executor: () => {} }), /new artifact revision/);
const staleRender = path.join(dir, "stale-render.json");
fs.writeFileSync(staleRender, JSON.stringify(verdict("c".repeat(64), "b".repeat(64), false)));
assert.throws(() => runVisualRepairLoop({ initialEvidence: initial, repairedEvidence: staleRender, repairCommand: ["repair"], rerenderCommand: ["render"], executor: () => {} }), /new render evidence/);
fs.rmSync(dir, { recursive: true, force: true });
console.log("Astra visual repair loop: PASS");
