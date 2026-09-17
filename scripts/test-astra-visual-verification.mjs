#!/usr/bin/env node
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {validateVisualVerification} from './validate-astra-visual-verification.mjs';

const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'vrmine-visual-'));
const views = {};
for (const name of ['front_3_4', 'rear_3_4', 'left', 'right', 'top', 'geometry_diagnostic']) {
  const file = `${name}.png`;
  fs.writeFileSync(path.join(dir, file), name);
  views[name] = {path: file, sha256: 'a'.repeat(64)};
}
const checks = {};
for (const name of ['scale_dimensions', 'floating_parts', 'mesh_intersection', 'topology', 'material_assignment', 'texture_uv', 'rig_joint_placement', 'collider_placement', 'camera_clipping', 'world_obstruction']) {
  checks[name] = {status: 'PASS', evidence: `${name} inspected`};
}
const base = {schema_version: 1, artifact_sha256: 'b'.repeat(64), views, checks, verdict: 'PASS', repair_required: false, repair_targets: []};
assert.deepEqual(validateVisualVerification(base, dir), {verdict: 'PASS', failed_checks: []});

const failed = structuredClone(base);
failed.checks.mesh_intersection.status = 'FAIL';
failed.verdict = 'FAIL';
failed.repair_required = true;
failed.repair_targets = ['mesh_intersection'];
assert.deepEqual(validateVisualVerification(failed, dir), {verdict: 'FAIL', failed_checks: ['mesh_intersection']});

for (const mutate of [
  value => { value.verdict = 'PASS'; },
  value => { value.repair_required = false; },
  value => { value.repair_targets = []; },
  value => { delete value.views.top; },
  value => { value.checks.topology.evidence = ''; },
]) {
  const invalid = structuredClone(failed);
  mutate(invalid);
  assert.throws(() => validateVisualVerification(invalid, dir));
}

const missingEvidence = structuredClone(base);
missingEvidence.views.left.path = 'missing.png';
assert.throws(() => validateVisualVerification(missingEvidence, dir));
fs.rmSync(dir, {recursive: true, force: true});
console.log('Astra visual verification contract: PASS');
