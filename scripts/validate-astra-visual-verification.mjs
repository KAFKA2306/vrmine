#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';

const REQUIRED_VIEWS = ['front_3_4', 'rear_3_4', 'left', 'right', 'top', 'geometry_diagnostic'];
const REQUIRED_CHECKS = ['scale_dimensions', 'floating_parts', 'mesh_intersection', 'topology', 'material_assignment', 'texture_uv', 'rig_joint_placement', 'collider_placement', 'camera_clipping', 'world_obstruction'];
const SHA256 = /^[0-9a-f]{64}$/;
const CHECK_STATUS = new Set(['PASS', 'FAIL', 'NOT_APPLICABLE']);

function fail(message) { throw new Error(`visual verification contract: ${message}`); }
function exactKeys(value, required, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) fail(`${label} must be an object`);
  const keys = Object.keys(value).sort();
  const expected = [...required].sort();
  if (JSON.stringify(keys) !== JSON.stringify(expected)) fail(`${label} keys differ from contract`);
}

export function validateVisualVerification(data, root = process.cwd()) {
  const topKeys = Object.keys(data).sort();
  const allowed = ['artifact_sha256', 'checks', 'repair_required', 'repair_targets', 'schema_version', 'verdict', 'views'];
  if (topKeys.some(key => !allowed.includes(key))) fail('unknown top-level field');
  if (data.schema_version !== 1) fail('schema_version must be 1');
  if (!SHA256.test(data.artifact_sha256 ?? '')) fail('artifact_sha256 must be lowercase SHA-256');

  exactKeys(data.views, REQUIRED_VIEWS, 'views');
  for (const name of REQUIRED_VIEWS) {
    exactKeys(data.views[name], ['path', 'sha256'], `view ${name}`);
    const view = data.views[name];
    if (typeof view.path !== 'string' || !view.path) fail(`${name} path missing`);
    if (!SHA256.test(view.sha256 ?? '')) fail(`${name} sha256 invalid`);
    const resolved = path.resolve(root, view.path);
    if (!fs.existsSync(resolved) || !fs.statSync(resolved).isFile()) fail(`${name} evidence file missing: ${view.path}`);
  }

  exactKeys(data.checks, REQUIRED_CHECKS, 'checks');
  const failures = [];
  for (const name of REQUIRED_CHECKS) {
    exactKeys(data.checks[name], ['evidence', 'status'], `check ${name}`);
    const check = data.checks[name];
    if (!CHECK_STATUS.has(check.status)) fail(`${name} status invalid`);
    if (typeof check.evidence !== 'string' || !check.evidence.trim()) fail(`${name} evidence missing`);
    if (check.status === 'FAIL') failures.push(name);
  }

  const expectedVerdict = failures.length ? 'FAIL' : 'PASS';
  if (data.verdict !== expectedVerdict) fail(`verdict ${data.verdict} contradicts checks; expected ${expectedVerdict}`);
  if (data.repair_required !== (failures.length > 0)) fail('repair_required contradicts failed checks');
  const targets = data.repair_targets ?? [];
  if (!Array.isArray(targets) || targets.some(value => typeof value !== 'string' || !value)) fail('repair_targets must be non-empty strings');
  if (new Set(targets).size !== targets.length) fail('repair_targets must be unique');
  if (failures.length && targets.length === 0) fail('failed verification requires repair_targets');
  if (!failures.length && targets.length) fail('passing verification cannot declare repair_targets');
  return {verdict: expectedVerdict, failed_checks: failures};
}

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(new URL(import.meta.url).pathname)) {
  const evidencePath = process.argv[2];
  if (!evidencePath) fail('usage: validate-astra-visual-verification.mjs <evidence.json>');
  const absolute = path.resolve(evidencePath);
  const result = validateVisualVerification(JSON.parse(fs.readFileSync(absolute, 'utf8')), path.dirname(absolute));
  process.stdout.write(`${JSON.stringify(result)}\n`);
}
