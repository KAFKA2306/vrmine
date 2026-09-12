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

if (!rootNodeId || rootNodeId.startsWith('--')) {
  fail('usage: node scripts/world-item-build-order.mjs --node <graph-node-id> [--graph <graph.json>]');
}
if (graphIndex >= 0 && (!graphPath || graphPath.startsWith('--'))) fail('--graph requires a path');

const loadGraph = () => {
  if (graphPath) return JSON.parse(fs.readFileSync(path.resolve(root, graphPath), 'utf8'));

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
const dependencies = new Map();
for (const edge of graph.edges) {
  const edgeId = edge.id ?? `${edge.from}|${edge.type}|${edge.to}`;
  if (!nodes.has(edge.from) || !nodes.has(edge.to)) fail(`invalid edge: ${edgeId}`);
  if (!reverse.has(edge.to)) reverse.set(edge.to, []);
  reverse.get(edge.to).push(edge.from);
  if (!dependencies.has(edge.from)) dependencies.set(edge.from, []);
  dependencies.get(edge.from).push(edge.to);
}
for (const values of reverse.values()) values.sort();
for (const values of dependencies.values()) values.sort();

const selected = new Set([rootNodeId]);
const queue = [rootNodeId];
for (let index = 0; index < queue.length; index += 1) {
  for (const dependent of reverse.get(queue[index]) ?? []) {
    if (selected.has(dependent)) continue;
    selected.add(dependent);
    queue.push(dependent);
  }
}

const remaining = new Set(selected);
const emitted = new Set();
const order = [];
while (remaining.size > 0) {
  const ready = [...remaining]
    .filter((id) => (dependencies.get(id) ?? []).every((dependency) => !selected.has(dependency) || emitted.has(dependency)))
    .sort();
  if (ready.length === 0) fail(`dependency cycle inside affected subgraph rooted at ${rootNodeId}`);

  for (const id of ready) {
    remaining.delete(id);
    emitted.add(id);
    order.push({
      index: order.length,
      id,
      type: nodes.get(id).type,
      key: nodes.get(id).key,
      source: nodes.get(id).source ?? null
    });
  }
}

const position = new Map(order.map((entry) => [entry.id, entry.index]));
for (const edge of graph.edges) {
  if (!selected.has(edge.from) || !selected.has(edge.to)) continue;
  if (position.get(edge.to) >= position.get(edge.from)) {
    fail(`invalid build order: dependency ${edge.to} must precede dependent ${edge.from}`);
  }
}

const byType = order.reduce((counts, entry) => {
  counts[entry.type] = (counts[entry.type] ?? 0) + 1;
  return counts;
}, {});

process.stdout.write(`${JSON.stringify({
  root: nodes.get(rootNodeId),
  summary: {
    selected: selected.size,
    by_type: byType
  },
  order
}, null, 2)}\n`);
