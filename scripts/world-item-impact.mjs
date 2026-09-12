import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const root = process.cwd();
const args = process.argv.slice(2);
const nodeIndex = args.indexOf('--node');
const graphIndex = args.indexOf('--graph');
const rootNodeId = nodeIndex >= 0 ? args[nodeIndex + 1] : null;
const graphPath = graphIndex >= 0 ? args[graphIndex + 1] : null;

const fail = (message) => {
  console.error(message);
  process.exit(1);
};

if (!rootNodeId) fail('usage: node scripts/world-item-impact.mjs --node <graph-node-id> [--graph <graph.json>]');
if (nodeIndex >= 0 && !rootNodeId) fail('--node requires a graph node id');
if (graphIndex >= 0 && !graphPath) fail('--graph requires a path');

const loadGraph = () => {
  if (graphPath) {
    return JSON.parse(fs.readFileSync(path.resolve(root, graphPath), 'utf8'));
  }

  const result = spawnSync(process.execPath, ['scripts/world-item-graph.mjs'], {
    cwd: root,
    encoding: 'utf8'
  });
  if (result.status !== 0) {
    process.stderr.write(result.stderr || 'failed to derive world-item graph\n');
    process.exit(result.status ?? 1);
  }
  return JSON.parse(result.stdout);
};

const graph = loadGraph();
if (!Array.isArray(graph.nodes) || !Array.isArray(graph.edges)) fail('graph must contain nodes and edges arrays');

const nodes = new Map(graph.nodes.map((node) => [node.id, node]));
if (!nodes.has(rootNodeId)) fail(`unknown graph node: ${rootNodeId}`);

const reverse = new Map();
for (const edge of graph.edges) {
  if (!nodes.has(edge.from) || !nodes.has(edge.to)) fail(`invalid edge: ${edge.id ?? `${edge.from}|${edge.type}|${edge.to}`}`}`);
  if (!reverse.has(edge.to)) reverse.set(edge.to, []);
  reverse.get(edge.to).push({ from: edge.from, type: edge.type });
}
for (const incoming of reverse.values()) incoming.sort((a, b) => a.from.localeCompare(b.from) || a.type.localeCompare(b.type));

const queue = [{ id: rootNodeId, distance: 0 }];
const seen = new Map([[rootNodeId, { distance: 0, via: null }]]);
for (let index = 0; index < queue.length; index += 1) {
  const current = queue[index];
  for (const incoming of reverse.get(current.id) ?? []) {
    if (seen.has(incoming.from)) continue;
    const next = {
      distance: current.distance + 1,
      via: { edge_type: incoming.type, depends_on: current.id }
    };
    seen.set(incoming.from, next);
    queue.push({ id: incoming.from, distance: next.distance });
  }
}

const affected = [...seen.entries()]
  .filter(([id]) => id !== rootNodeId)
  .map(([id, meta]) => ({
    id,
    type: nodes.get(id).type,
    key: nodes.get(id).key,
    distance: meta.distance,
    via: meta.via
  }))
  .sort((a, b) => a.distance - b.distance || a.id.localeCompare(b.id));

const byType = affected.reduce((counts, node) => {
  counts[node.type] = (counts[node.type] ?? 0) + 1;
  return counts;
}, {});

process.stdout.write(`${JSON.stringify({
  root: nodes.get(rootNodeId),
  summary: {
    affected: affected.length,
    by_type: byType
  },
  affected
}, null, 2)}\n`);
