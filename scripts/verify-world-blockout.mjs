import fs from 'node:fs';

const specPath = process.argv[2] ?? 'config/world-design/generated/woodland-tabletop-village-v0.json';
const fail = (message) => { throw new Error(`World Blockout FAIL: ${message}`); };
const spec = JSON.parse(fs.readFileSync(specPath, 'utf8'));

const finite = (value, path) => {
  if (!Number.isFinite(value)) fail(`${path} must be a finite number`);
  return value;
};
const positive = (value, path) => {
  const n = finite(value, path);
  if (n <= 0) fail(`${path} must be > 0`);
  return n;
};
const vec = (value, length, path) => {
  if (!Array.isArray(value) || value.length !== length) fail(`${path} must be a ${length}-vector`);
  return value.map((v, i) => finite(v, `${path}[${i}]`));
};
const nonEmptyStrings = (value, path) => {
  if (!Array.isArray(value) || value.length < 1 || value.some((v) => typeof v !== 'string' || v.length === 0)) {
    fail(`${path} must be a non-empty string array`);
  }
  return value;
};

const blockout = spec.blockout;
if (!blockout || typeof blockout !== 'object') fail('blockout is required');
if (blockout.coordinate_system !== 'meters_xyz_z_up') fail('blockout.coordinate_system must be meters_xyz_z_up');
const tabletopTop = finite(blockout.tabletop_top_z_m, 'blockout.tabletop_top_z_m');

const tabletop = (spec.spatial_geometry?.zones ?? []).find((zone) => zone.zone_id === 'woodland_tabletop');
if (!tabletop) fail('spatial_geometry.zones must include woodland_tabletop');
if (tabletop.shape !== 'circle') fail('woodland_tabletop.shape must be circle');
const tabletopRadius = positive(tabletop.diameter_m, 'woodland_tabletop.diameter_m') / 2;
const ceilingHeight = positive(spec.world_build?.ceiling_height_m, 'world_build.ceiling_height_m');

if (!Array.isArray(blockout.anchors) || blockout.anchors.length < 1) fail('blockout.anchors must be non-empty');
const anchorIds = new Set();
for (const [index, anchor] of blockout.anchors.entries()) {
  const base = `blockout.anchors[${index}]`;
  if (!anchor || typeof anchor !== 'object') fail(`${base} must be an object`);
  if (typeof anchor.id !== 'string' || anchor.id.length === 0) fail(`${base}.id is required`);
  if (anchorIds.has(anchor.id)) fail(`duplicate anchor id: ${anchor.id}`);
  anchorIds.add(anchor.id);
  const position = vec(anchor.position_m, 3, `${base}.position_m`);
  const footprint = vec(anchor.footprint_m, 2, `${base}.footprint_m`);
  footprint.forEach((v, i) => positive(v, `${base}.footprint_m[${i}]`));
  const height = positive(anchor.height_m, `${base}.height_m`);
  if (typeof anchor.role !== 'string' || anchor.role.length === 0) fail(`${base}.role is required`);
  if (position[2] < tabletopTop - 1e-6) fail(`${anchor.id} starts below tabletop surface`);
  if (position[2] + height > ceilingHeight + 1e-6) fail(`${anchor.id} exceeds room ceiling`);

  const halfX = footprint[0] / 2;
  const halfY = footprint[1] / 2;
  for (const dx of [-halfX, halfX]) {
    for (const dy of [-halfY, halfY]) {
      if (Math.hypot(position[0] + dx, position[1] + dy) > tabletopRadius + 1e-6) {
        fail(`${anchor.id} footprint exceeds woodland_tabletop`);
      }
    }
  }
}

const compositionAxis = nonEmptyStrings(blockout.composition_axis, 'blockout.composition_axis');
for (const id of compositionAxis) {
  if (!anchorIds.has(id)) fail(`composition_axis references missing anchor ${id}`);
}

const worldSymbols = blockout.anchors.filter((anchor) => anchor.role === 'world_symbol');
if (worldSymbols.length !== 1) fail(`expected exactly one world_symbol anchor, got ${worldSymbols.length}`);
const worldSymbol = worldSymbols[0];
const activityAnchor = vec(spec.world_build?.activity_anchor?.position_m, 3, 'world_build.activity_anchor.position_m');
const symbolPosition = vec(worldSymbol.position_m, 3, `anchor ${worldSymbol.id}.position_m`);
if (Math.hypot(activityAnchor[0] - symbolPosition[0], activityAnchor[1] - symbolPosition[1], activityAnchor[2] - symbolPosition[2]) > 0.25) {
  fail('world_build.activity_anchor must resolve to the world_symbol blockout anchor');
}

const heroTarget = vec(spec.world_build?.hero_view?.target_m, 3, 'world_build.hero_view.target_m');
const symbolFootprint = vec(worldSymbol.footprint_m, 2, `anchor ${worldSymbol.id}.footprint_m`);
if (Math.hypot(heroTarget[0] - symbolPosition[0], heroTarget[1] - symbolPosition[1]) > Math.max(...symbolFootprint) / 2 + 0.25) {
  fail('hero_view target does not frame the primary world_symbol');
}
if (heroTarget[2] < symbolPosition[2] || heroTarget[2] > symbolPosition[2] + worldSymbol.height_m) {
  fail('hero_view target height does not intersect the primary world_symbol');
}

const lifeTraces = nonEmptyStrings(spec.life_traces, 'life_traces');
if (lifeTraces.length < 3) fail('at least three life_traces are required for blockout acceptance');
const motionSystems = spec.motion_budget?.systems;
if (!Array.isArray(motionSystems)) fail('motion_budget.systems must be an array');
if (!Number.isInteger(spec.motion_budget?.initial_animated_systems)) fail('motion_budget.initial_animated_systems must be an integer');
if (motionSystems.length !== spec.motion_budget.initial_animated_systems) fail('motion_budget count does not match systems');
if (motionSystems.length > 3) fail('initial blockout may contain at most three animated systems');

nonEmptyStrings(spec.lighting_design?.practical_lights, 'lighting_design.practical_lights');
nonEmptyStrings(spec.soundscape?.layers, 'soundscape.layers');
nonEmptyStrings(spec.acceptance?.must_pass, 'acceptance.must_pass');
if (!Array.isArray(spec.acceptance?.must_not_include_yet)) fail('acceptance.must_not_include_yet must be an array');

console.log(JSON.stringify({
  status: 'PASS',
  spec: specPath,
  theme: spec.meta?.theme_name ?? null,
  anchors: blockout.anchors.length,
  composition_axis: compositionAxis,
  primary_landmark: worldSymbol.id,
  life_traces: lifeTraces.length,
  animated_systems: motionSystems.length
}, null, 2));
