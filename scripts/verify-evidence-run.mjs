import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.env.EVIDENCE_RUN_DIR||'artifacts/evidence-ladder/sample';
const expected=process.env.EXACT_HEAD;
assert(expected,'EXACT_HEAD is required');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const result=read('result.json'), manifest=read('manifest.json');
assert.equal(result.revision,expected); assert.equal(manifest.revision,expected);
assert.equal(result.artifact_manifest,'manifest.json');
assert.deepEqual(result.events,['generate','verify:FAIL','repair','rerender','verify:PASS']);
assert.deepEqual(result.stages.map(s=>s.id),Array.from({length:9},(_,i)=>`E${i}`));
assert.deepEqual(result.stages.map(s=>s.status),['PASS','PASS','PASS','PASS','UNVERIFIED','UNVERIFIED','UNVERIFIED','UNVERIFIED','UNVERIFIED']);
for(const a of manifest.artifacts){
  const file=path.join(root,a.path); assert(fs.existsSync(file),`missing ${a.path}`);
  const actual=crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'); assert.equal(actual,a.sha256,`hash mismatch ${a.path}`);
}
const generated=read('generated.json'), repaired=read('repaired.json');
assert(generated.width_m<=0,'pre-repair artifact must independently fail');
assert(repaired.width_m>0&&repaired.height_m>0,'repaired artifact must independently pass');
assert(fs.readFileSync(path.join(root,'renders/before.svg'),'utf8').includes('generated'));
assert(fs.readFileSync(path.join(root,'renders/after.svg'),'utf8').includes('repaired'));
console.log('evidence run: PASS');
