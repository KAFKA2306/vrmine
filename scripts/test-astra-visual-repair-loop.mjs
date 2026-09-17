#!/usr/bin/env node
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {runVisualRepairLoop} from './run-astra-visual-repair-loop.mjs';

const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'vrmine-repair-'));
const names = ['front_3_4','rear_3_4','left','right','top','geometry_diagnostic'];
const checks = ['scale_dimensions','floating_parts','mesh_intersection','topology','material_assignment','texture_uv','rig_joint_placement','collider_placement','camera_clipping','world_obstruction'];
function evidence(sha, failing = false) {
  const views = Object.fromEntries(names.map(name => {
    const file = `${sha[0]}-${name}.png`; fs.writeFileSync(path.join(dir, file), name); return [name, {path: file, sha256: 'a'.repeat(64)}];
  }));
  const result = Object.fromEntries(checks.map(name => [name, {status: failing && name === 'mesh_intersection' ? 'FAIL' : 'PASS', evidence: `${name} inspected`} ]));
  return {schema_version:1, artifact_sha256:sha, views, checks:result, verdict:failing?'FAIL':'PASS', repair_required:failing, repair_targets:failing?['mesh_intersection']:[]};
}
const initial = path.join(dir, 'initial.json'); const repaired = path.join(dir, 'repaired.json');
fs.writeFileSync(initial, JSON.stringify(evidence('b'.repeat(64), true)));
fs.writeFileSync(repaired, JSON.stringify(evidence('c'.repeat(64), false)));
const repair = path.join(dir, 'repair.mjs'); const rerender = path.join(dir, 'rerender.mjs');
fs.writeFileSync(repair, "import fs from 'node:fs'; fs.writeFileSync(process.argv[2] + '.called', process.argv.slice(2).join(','));");
fs.writeFileSync(rerender, "import fs from 'node:fs'; fs.writeFileSync('rerender.called', '1');");
const cwd = process.cwd(); process.chdir(dir);
try {
  const result = runVisualRepairLoop({initialEvidence:initial, repairCommand:process.execPath, repairArgs:[repair], rerenderCommand:process.execPath, rerenderArgs:[rerender], repairedEvidence:repaired});
  assert.equal(result.verdict, 'PASS'); assert.equal(result.repaired, true); assert.equal(result.attempts, 1);
  assert.equal(fs.readFileSync(path.join(dir, 'mesh_intersection.called'), 'utf8'), 'mesh_intersection');
  assert.ok(fs.existsSync(path.join(dir, 'rerender.called')));
  const unchanged = path.join(dir, 'unchanged.json'); fs.writeFileSync(unchanged, JSON.stringify(evidence('b'.repeat(64), false)));
  assert.throws(() => runVisualRepairLoop({initialEvidence:initial, repairCommand:process.execPath, repairArgs:[repair], rerenderCommand:process.execPath, rerenderArgs:[rerender], repairedEvidence:unchanged}), /new artifact revision/);
  const stillFail = path.join(dir, 'still-fail.json'); fs.writeFileSync(stillFail, JSON.stringify(evidence('d'.repeat(64), true)));
  assert.throws(() => runVisualRepairLoop({initialEvidence:initial, repairCommand:process.execPath, repairArgs:[repair], rerenderCommand:process.execPath, rerenderArgs:[rerender], repairedEvidence:stillFail}), /remains FAIL/);
} finally { process.chdir(cwd); fs.rmSync(dir, {recursive:true, force:true}); }
console.log('Astra visual repair loop: PASS');
