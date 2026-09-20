import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const revision=process.env.EXACT_HEAD;
if(!revision) throw new Error('EXACT_HEAD is required');
const root=process.env.EVIDENCE_RUN_DIR||'artifacts/evidence-ladder/sample';
fs.rmSync(root,{recursive:true,force:true});
fs.mkdirSync(path.join(root,'renders'),{recursive:true});
const write=(p,v)=>fs.writeFileSync(path.join(root,p),typeof v==='string'?v:JSON.stringify(v,null,2)+'\n');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex');
const verify=o=>Number.isFinite(o.width_m)&&o.width_m>0&&Number.isFinite(o.height_m)&&o.height_m>0;
const svg=(o,label)=>`<svg xmlns="http://www.w3.org/2000/svg" width="320" height="180"><rect width="100%" height="100%" fill="white"/><rect x="80" y="40" width="${Math.max(1,o.width_m*80)}" height="${Math.max(1,o.height_m*80)}" fill="none" stroke="black"/><text x="10" y="20">${label}</text></svg>\n`;

const spec={asset_id:'evidence_sample',width_m:1,height_m:1};
write('spec.json',spec);
const generated={...spec,width_m:-1};
write('generated.json',generated); write('renders/before.svg',svg(generated,'generated'));
const firstPass=verify(generated);
if(firstPass) throw new Error('sample must exercise repair path');
const repaired={...generated,width_m:spec.width_m};
write('repaired.json',repaired); write('renders/after.svg',svg(repaired,'repaired'));
if(!verify(repaired)) throw new Error('repair did not satisfy verifier');
const artifacts=['spec.json','generated.json','repaired.json','renders/before.svg','renders/after.svg'];
const manifest={run_id:'evidence-ladder-sample',revision,artifacts:artifacts.map(p=>({path:p,sha256:hash(p)}))};
write('manifest.json',manifest);
const stages=Array.from({length:9},(_,i)=>({id:`E${i}`,status:i<=3?'PASS':'UNVERIFIED'}));
write('result.json',{run_id:manifest.run_id,revision,artifact_manifest:'manifest.json',events:['generate','verify:FAIL','repair','rerender','verify:PASS'],stages});
console.log(root);
