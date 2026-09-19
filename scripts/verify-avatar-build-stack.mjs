import fs from 'node:fs';
import assert from 'node:assert/strict';
const p=JSON.parse(fs.readFileSync('config/avatar-build-stack.json','utf8'));
assert.equal(p.source_policy.mutate_source_assets,false,'source assets must remain immutable');
assert.equal(p.source_policy.build_clone_only,true,'transformations must target build clone');
const tools=new Map(p.tools.map(t=>[t.id,t]));
for(const id of p.build_order) assert(tools.has(id),`unknown build tool ${id}`);
for(let i=1;i<p.build_order.length;i++) assert(tools.get(p.build_order[i-1]).order<=tools.get(p.build_order[i]).order,'build order must be monotonic');
const active=p.tools.filter(t=>!t.optional);
const authorities=new Map();
for(const t of active){ if(authorities.has(t.authority)) throw new Error(`duplicate authority ${t.authority}: ${authorities.get(t.authority)}, ${t.id}`); authorities.set(t.authority,t.id); }
for(const t of p.tools) for(const x of t.exclusive_with??[]) assert.notEqual(p.responsibilities[t.authority],t.id===x?t.id:x,`exclusive optimizers active: ${t.id}/${x}`);
for(const [role,id] of Object.entries(p.responsibilities)) { assert(tools.has(id),`missing owner ${id}`); assert.equal(tools.get(id).authority,role,`owner mismatch for ${role}`); }
assert(p.metrics.require_diff,'before/after diff required');
assert(p.metrics.before_after.includes('triangles')&&p.metrics.before_after.includes('texture_memory_bytes'),'core metrics missing');
assert.equal(p.validation.reject_duplicate_authority,true);
assert.equal(p.validation.reject_source_mutation,true);
console.log('avatar build stack: PASS');
