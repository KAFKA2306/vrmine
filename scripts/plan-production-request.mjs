import fs from 'node:fs';
import { routeProductionRequest } from './route-production-request.mjs';

export function planProductionRequest(request, registry, stageGraph) {
  const route = routeProductionRequest(request, registry);
  if (route.status !== 'PLANNED') return route;
  const graph = stageGraph.graphs?.[request.kind];
  if (!Array.isArray(graph) || graph.length === 0) {
    return {...route, status: 'BLOCKED', reason: `no stage graph for kind: ${request.kind}`};
  }
  return {
    status: 'PLANNED',
    request: route.request,
    capability: route.capability,
    provider: route.provider,
    fallback: route.fallback,
    evidence: 'UNVERIFIED',
    stages: graph.map((node, index) => ({
      index,
      stage: node.stage,
      requires: node.requires,
      expected_outputs: node.outputs,
      provider: ['GENERATE', 'NORMALIZE'].includes(node.stage) ? route.provider : null,
      verifier: node.stage === 'VALIDATE_STATIC' ? route.verifier : null,
      status: 'UNVERIFIED'
    }))
  };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const [requestPath, registryPath = 'config/capability-registry.json', graphPath = 'config/production-stage-graph.json'] = process.argv.slice(2);
  if (!requestPath) throw new Error('usage: node scripts/plan-production-request.mjs <request.json> [registry.json] [stage-graph.json]');
  const read = (path) => JSON.parse(fs.readFileSync(path, 'utf8'));
  process.stdout.write(`${JSON.stringify(planProductionRequest(read(requestPath), read(registryPath), read(graphPath)), null, 2)}\n`);
}
