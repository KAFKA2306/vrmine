import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";

const dir = fs.mkdtempSync(path.join(os.tmpdir(), "vrmine-ci-evidence-"));
const output = path.join(dir, "evidence.json");
const headSha = "a".repeat(40);
const eventSha = "b".repeat(40);
const env = {
  ...process.env,
  EXACT_HEAD_SHA: headSha,
  GITHUB_SHA: eventSha,
  GITHUB_REPOSITORY: "KAFKA2306/vrmine",
  GITHUB_RUN_ID: "123",
  GITHUB_RUN_ATTEMPT: "2"
};
let result = spawnSync(process.execPath, ["scripts/write-exact-head-ci-evidence.mjs", output], { env, encoding: "utf8" });
assert.equal(result.status, 0, result.stderr);
assert.deepEqual(JSON.parse(fs.readFileSync(output, "utf8")), {
  schema_version: 1,
  repository: "KAFKA2306/vrmine",
  exact_head_sha: headSha,
  event_sha: eventSha,
  run_id: "123",
  run_attempt: 2,
  merge_gate: "PASS"
});
result = spawnSync(process.execPath, ["scripts/write-exact-head-ci-evidence.mjs", output], {
  env: { ...env, EXACT_HEAD_SHA: "not-a-sha" }, encoding: "utf8"
});
assert.notEqual(result.status, 0);
assert.match(result.stderr, /EXACT_HEAD_SHA must be an exact 40-character commit SHA/);
fs.rmSync(dir, { recursive: true, force: true });
console.log("exact-head CI evidence contract: PASS");
