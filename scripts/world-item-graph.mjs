import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const specDir = path.join(root, 'config', 'world-items');
const args = process.argv.slice(2);
const checkOnly = args.includes('--check');
const outputIndex = args.indexOf('--output');
const outputPath = outputIndex >= 0 ? args[outputIndex + 1] : null;

const fail = (message) => { throw new Error(message); };
const stable = (values) => [...values].sort((a, b) => a.id.localeCompare(b.id));

if (!fs.existsSync(specDir)) fail(`missing world-item spec directory: ${path.relative(root, specDir)}`);
if (outputIndex >= 0 && !outputPath) fail('--output requires a path');

const specFiles = fs.readdirSync(specDir)
  .filter((name) => name.endsWith('.json'))
  .sort();
if (specFiles.length === 0) fail('no world-item specs found');

const nodes = new Map();
const edges = new Map();
const warnings = [];
const skuIds = new Set();

const addNode = (node) => {
  const existing = nodes.get(node.id);
  if (existing) {
    if (JSON.stringify(existing) !== JSON.stringify(node)) fail(`node collision: ${node.id}`);
    return;
  }
  nodes.set(node.id, node);
};

const addEdge = (from, type, to) => {
  const id = `${from}|${type}|${to}`;
  edges.set(id, { id, from, type, to });
};

for (const filename of specFiles) {
  const source = path.posix.join('config/world-items', filename);
  const spec = JSON.parse(fs.readFileSync(path.join(specDir, filename), 'utf8'));
  if (!spec.id || typeof spec.id !== 'string') fail(`${source}: id must be a non-empty string`);
  if (skuIds.has(spec.id)) fail(`${source}: duplicate SKU id ${spec.id}`);
  skuIds.add(spec.id);

  const skuNode = `sku:${spec.id}`;
  addNode({ id: skuNode, type: 'SKU', key: spec.id, family: spec.family ?? null, source });

  if (!Array.isArray(spec.parts)) fail(`${source}: parts must be an array`);
  if (!spec.materials || typeof spec.materials !== 'object' || Array.isArray(spec.materials)) fail(`${source}: materials must be an object`);

  const partNames = new Set();
  const usedMaterials = new Set();
  for (const part of spec.parts) {
    if (!part.name || typeof part.name !== 'string') fail(`${source}: every part requires a name`);
    if (partNames.has(part.name)) fail(`${source}: duplicate part name ${part.name}`);
    partNames.add(part.name);
    if (!part.component || typeof part.component !== 'string') fail(`${source}: part ${part.name} requires component`);
    if (!part.material || typeof part.material !== 'string') fail(`${source}: part ${part.name} requires material`);
    if (!(part.material in spec.materials)) fail(`${source}: part ${part.name} references missing material ${part.material}`);
    usedMaterials.add(part.material);

    const partNode = `part:${spec.id}:${part.name}`;
    const generatorNode = `generator:${part.component}`;
    const materialNode = `material:${spec.id}:${part.material}`;
    addNode({ id: partNode, type: 'Component', key: part.name, sku: spec.id, source });
    addNode({ id: generatorNode, type: 'Generator', key: part.component });
    addNode({ id: materialNode, type: 'Material', key: part.material, sku: spec.id, source });
    addEdge(skuNode, 'CONTAINS', partNode);
    addEdge(partNode, 'GENERATED_BY', generatorNode);
    addEdge(partNode, 'USES', materialNode);
  }

  for (const material of Object.keys(spec.materials).sort()) {
    const materialNode = `material:${spec.id}:${material}`;
    addNode({ id: materialNode, type: 'Material', key: material, sku: spec.id, source });
    if (!usedMaterials.has(material)) warnings.push(`${source}: unused material ${material}`);
  }

  const variantIds = new Set();
  for (const variant of spec.variants ?? []) {
    if (!variant.id || typeof variant.id !== 'string') fail(`${source}: every variant requires an id`);
    if (variantIds.has(variant.id)) fail(`${source}: duplicate variant id ${variant.id}`);
    variantIds.add(variant.id);
    const variantNode = `variant:${spec.id}:${variant.id}`;
    addNode({ id: variantNode, type: 'Variant', key: variant.id, sku: spec.id, source });
    addEdge(skuNode, 'HAS_VARIANT', variantNode);

    for (const material of Object.keys(variant.material_overrides ?? {}).sort()) {
      if (!(material in spec.materials)) fail(`${source}: variant ${variant.id} overrides missing material ${material}`);
      addEdge(variantNode, 'OVERRIDES', `material:${spec.id}:${material}`);
    }
    for (const partName of Object.keys(variant.part_overrides ?? {}).sort()) {
      if (!partNames.has(partName)) fail(`${source}: variant ${variant.id} overrides missing part ${partName}`);
      addEdge(variantNode, 'OVERRIDES', `part:${spec.id}:${partName}`);
    }
  }
}

for (const edge of edges.values()) {
  if (!nodes.has(edge.from)) fail(`edge has missing source node: ${edge.id}`);
  if (!nodes.has(edge.to)) fail(`edge has missing target node: ${edge.id}`);
}

const adjacency = new Map();
for (const edge of edges.values()) {
  if (!adjacency.has(edge.from)) adjacency.set(edge.from, []);
  adjacency.get(edge.from).push(edge.to);
}
const visiting = new Set();
const visited = new Set();
const visit = (id, trail = []) => {
  if (visiting.has(id)) fail(`dependency cycle: ${[...trail, id].join(' -> ')}`);
  if (visited.has(id)) return;
  visiting.add(id);
  for (const next of adjacency.get(id) ?? []) visit(next, [...trail, id]);
  visiting.delete(id);
  visited.add(id);
};
for (const id of nodes.keys()) visit(id);

const degree = new Map([...nodes.keys()].map((id) => [id, 0]));
for (const edge of edges.values()) {
  degree.set(edge.from, degree.get(edge.from) + 1);
  degree.set(edge.to, degree.get(edge.to) + 1);
}
const orphanNodes = [...degree.entries()]
  .filter(([id, value]) => value === 0 && !id.startsWith('sku:'))
  .map(([id]) => id)
  .sort();
for (const id of orphanNodes) warnings.push(`orphan node ${id}`);

const graph = {
  schema_version: 1,
  authority: 'config/world-items/*.json',
  generated: true,
  summary: {
    specs: specFiles.length,
    nodes: nodes.size,
    edges: edges.size,
    warnings: warnings.length,
    by_type: [...nodes.values()].reduce((counts, node) => {
      counts[node.type] = (counts[node.type] ?? 0) + 1;
      return counts;
    }, {})
  },
  nodes: stable(nodes.values()),
  edges: stable(edges.values()),
  warnings: warnings.sort()
};

if (outputPath) {
  fs.mkdirSync(path.dirname(path.resolve(root, outputPath)), { recursive: true });
  fs.writeFileSync(path.resolve(root, outputPath), `${JSON.stringify(graph, null, 2)}\n`);
}

if (checkOnly) {
  console.log(JSON.stringify(graph.summary, null, 2));
  if (warnings.length) console.error(warnings.join('\n'));
} else if (!outputPath) {
  process.stdout.write(`${JSON.stringify(graph, null, 2)}\n`);
} else {
  console.log(JSON.stringify({ ...graph.summary, output: outputPath }, null, 2));
}
