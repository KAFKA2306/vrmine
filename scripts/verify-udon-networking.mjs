import fs from 'node:fs';

const fail = (m) => { console.error(`FAIL: ${m}`); process.exitCode = 1; };
const contractPath = 'config/udon-networking.json';
if (!fs.existsSync(contractPath)) fail('missing Udon networking contract');
const c = JSON.parse(fs.readFileSync(contractPath, 'utf8'));

if (c.authority !== 'udon_networking') fail('authority must be singular');
for (const role of ['runtime', 'editor']) if (!Array.isArray(c.responsibilities?.[role]) || !c.responsibilities[role].length) fail(`missing ${role} responsibilities`);
if (c.state_sync?.preferred !== 'UdonSynced' || !c.state_sync.manual_sync_requires_request_serialization || !c.state_sync.ownership_required_before_mutation || !c.state_sync.late_join_state_must_be_serialized) fail('state-sync policy is incomplete');
const limits = c.network_limits ?? {};
if (!(limits.max_rpc_calls_per_second > 0) || !limits.forbid_rpc_for_persistent_state || !limits.forbid_request_serialization_in_update) fail('RPC/bandwidth misuse policy is incomplete');
if (!(limits.max_continuous_synced_bytes > 0) || !(limits.max_manual_synced_bytes > limits.max_continuous_synced_bytes)) fail('sync byte budgets are invalid');
if (!Array.isArray(c.unsupported_csharp) || !c.unsupported_csharp.length) fail('unsupported C# contract is empty');

const fixturePath = c.representative_gimmick?.source;
if (!fixturePath || !fs.existsSync(fixturePath)) fail('representative gimmick is missing');
else {
  const src = fs.readFileSync(fixturePath, 'utf8');
  for (const token of c.unsupported_csharp) if (src.includes(token)) fail(`unsupported C# token: ${token}`);
  for (const token of c.representative_gimmick.requires ?? []) if (!src.includes(token)) fail(`representative gimmick missing ${token}`);
  if (/\b(?:Update|LateUpdate|FixedUpdate)\s*\([^)]*\)[\s\S]{0,500}RequestSerialization\s*\(/m.test(src)) fail('RequestSerialization in frame loop');
  if (/SendCustomNetworkEvent\s*\([\s\S]{0,200}(?:Set|Toggle|State)/m.test(src)) fail('persistent state appears RPC-driven');
  const mutation = src.indexOf('enabledState =');
  const owner = src.indexOf('Networking.SetOwner');
  if (mutation < 0 || owner < 0 || owner > mutation) fail('ownership must be acquired before synced mutation');
}

if (!process.exitCode) console.log('PASS: Udon networking contract and representative gimmick');
