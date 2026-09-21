import fs from 'node:fs';

const CONTROL_ORDER = ['deterministic_code', 'explicit_api', 'mcp', 'external_generator', 'cua', 'manual'];
const KIND_CAPABILITY = {
  world_prop: 'procedural_mesh',
  world_layout: 'procedural_mesh',
  rigged_asset: 'rigged_mesh',
  avatar: 'rigged_mesh',
};

export function routeProductionRequest(request, registry) {
  if (!request || typeof request !== 'object') throw new Error('request must be an object');
  const capability = KIND_CAPABILITY[request.kind];
  if (!capability) return blocked(request, `no registered capability for kind: ${request.kind ?? '<missing>'}`);

  const platforms = request.target?.platform;
  if (!Array.isArray(platforms) || platforms.length === 0) return blocked(request, 'target.platform must be a non-empty array');

  const providers = registry.capabilities?.[capability]?.providers ?? [];
  const eligible = providers.filter((provider) =>
    provider.asset_kinds?.includes(request.kind) &&
    platforms.every((platform) => provider.platforms?.includes(platform))
  );
  if (eligible.length === 0) return blocked(request, `no provider satisfies ${capability} for ${platforms.join(',')}`);

  const priority = registry.control_priority ?? CONTROL_ORDER;
  const sorted = [...eligible].sort((a, b) => {
    const control = priority.indexOf(a.control_mode) - priority.indexOf(b.control_mode);
    if (control !== 0) return control;
    const tier = String(a.tier).localeCompare(String(b.tier));
    if (tier !== 0) return tier;
    return a.id.localeCompare(b.id);
  });
  const selected = sorted[0];

  return {
    status: 'PLANNED',
    request: {kind: request.kind, concept: request.concept, platforms},
    capability,
    provider: selected.id,
    tier: selected.tier,
    control_mode: selected.control_mode,
    verifier: selected.verifier,
    expected_artifacts: selected.expected_artifacts,
    fallback: selected.fallback,
    evidence: 'UNVERIFIED'
  };
}

function blocked(request, reason) {
  return {
    status: 'BLOCKED',
    request: {kind: request?.kind ?? null, concept: request?.concept ?? null},
    reason,
    evidence: 'UNVERIFIED'
  };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const [requestPath, registryPath = 'config/capability-registry.json'] = process.argv.slice(2);
  if (!requestPath) throw new Error('usage: node scripts/route-production-request.mjs <request.json> [registry.json]');
  const request = JSON.parse(fs.readFileSync(requestPath, 'utf8'));
  const registry = JSON.parse(fs.readFileSync(registryPath, 'utf8'));
  process.stdout.write(`${JSON.stringify(routeProductionRequest(request, registry), null, 2)}\n`);
}
