import fs from 'node:fs';
import assert from 'node:assert/strict';
const c=JSON.parse(fs.readFileSync('config/avatar-build-stack.json','utf8'));
assert.equal(c.source_policy.mutate_source_assets,false);
assert.equal(c.source_policy.build_clone_only,true);
assert.equal(c.optimization.target,'build_clone');
assert(c.optimization.allowed.length>=5);
assert.equal(c.metrics.require_diff,true);
assert.equal(c.visual_regression.required,true);
assert.equal(c.visual_regression.missing_execution,'UNVERIFIED');
assert.deepEqual(c.visual_regression.states,['PASS','FAIL','UNVERIFIED']);
assert(c.visual_regression.views.length>=4);
for(const p of ['pc','android']) { const b=c.platform_budgets[p]; assert(b.max_physbone_components>0); assert(b.max_physbone_affected_transforms>0); assert(b.max_texture_memory_bytes>0); }
assert(c.platform_budgets.android.max_physbone_components<c.platform_budgets.pc.max_physbone_components);
assert(c.platform_budgets.android.max_physbone_affected_transforms<c.platform_budgets.pc.max_physbone_affected_transforms);
assert(c.platform_budgets.android.max_texture_memory_bytes<c.platform_budgets.pc.max_texture_memory_bytes);
const authorities=new Map();
for(const t of c.tools.filter(t=>!t.optional)) { if(authorities.has(t.authority)) throw new Error(`duplicate authority: ${t.authority}`); authorities.set(t.authority,t.id); }
assert.equal(c.validation.reject_source_mutation,true);
assert.equal(c.validation.require_build_clone,true);
assert.equal(c.validation.require_visual_regression,true);
assert.equal(c.validation.require_platform_specific_budgets,true);
console.log('avatar build stack: PASS');
