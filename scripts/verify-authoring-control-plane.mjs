import fs from 'node:fs';

const path = 'config/authoring-control-plane.json';
const policy = JSON.parse(fs.readFileSync(path, 'utf8'));
const fail = (message) => { console.error(`FAIL: ${message}`); process.exitCode = 1; };

if (policy.authority !== 'repository_automation') fail('repository automation must remain canonical authority');
const expected = ['direct_code', 'editor_api', 'mcp', 'cua'];
if (JSON.stringify(policy.selection_order) !== JSON.stringify(expected)) fail('selection order must be direct code > editor API > MCP > CUA');
if (policy.discovery?.mode !== 'on_demand' || policy.discovery?.eager_tool_catalog !== false) fail('tool discovery must be on-demand');
const authorities = Object.entries(policy.providers ?? {}).filter(([, v]) => v.authority).map(([k]) => k);
if (authorities.length !== 1 || authorities[0] !== 'direct_code') fail('exactly one provider authority is required');
for (const [name, responsibility] of Object.entries(policy.responsibilities ?? {})) {
  if (!policy.providers[responsibility.primary]) fail(`${name}: unknown primary provider`);
  const seen = new Set([responsibility.primary]);
  for (const fallback of responsibility.fallback ?? []) {
    if (!policy.providers[fallback]) fail(`${name}: unknown fallback ${fallback}`);
    if (seen.has(fallback)) fail(`${name}: duplicate provider ${fallback}`);
    seen.add(fallback);
  }
}
const reconnect = policy.reconnect ?? {};
if (reconnect.strategy !== 'bounded_exponential_backoff' || !(reconnect.max_attempts > 0) || reconnect.on_exhausted !== 'UNVERIFIED') fail('reconnect must be bounded and exhaust to UNVERIFIED');
if (policy.failure?.silent_fallback !== false || policy.failure?.record_provider_failure !== true) fail('provider failure must be explicit and recorded');
if (!process.exitCode) console.log('PASS: authoring control plane contract');
