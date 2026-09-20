import fs from 'node:fs';
import assert from 'node:assert/strict';
const c=JSON.parse(fs.readFileSync('config/evidence-ladder.json','utf8'));
assert.equal(c.schema_version,1);
assert.deepEqual(c.states,['PASS','FAIL','UNVERIFIED']);
assert.deepEqual(c.stages.map(s=>s.id),['E0','E1','E2','E3','E4','E5','E6','E7','E8']);
for(let i=0;i<c.stages.length;i++) {
  const expected=i===0?[]:[`E${i-1}`];
  assert.deepEqual(c.stages[i].requires,expected,`${c.stages[i].id} must depend only on the preceding evidence stage`);
}
assert.equal(c.rules.exact_revision_required,true);
assert.equal(c.rules.missing_evidence,'UNVERIFIED');
assert.equal(c.rules.lower_stage_pass_does_not_promote_higher_stage,true);
assert.equal(c.rules.machine_verifier_independent_of_agent_claim,true);
assert.equal(c.rules.repair_requires_same_verifier_rerun,true);
assert.equal(c.rules.artifacts_require_manifest,true);
assert.deepEqual(c.run_contract.repair_loop,['generate','verify','repair','rerender','verify']);
for(const f of ['run_id','revision','artifact_manifest','stages']) assert(c.run_contract.required_fields.includes(f),`missing run field ${f}`);
console.log('evidence ladder contract: PASS');
