import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const specPath = process.argv[2];
if (!specPath) throw new Error('usage: node scripts/world-design-to-blender-plan.mjs <world-design-spec.json>');

const spec = JSON.parse(fs.readFileSync(specPath, 'utf8'));
const fail = (message) => { throw new Error(`World Build Plan FAIL: ${message}`); };
const finite = (value, name) => {
  if (!Number.isFinite(value)) fail(`${name} must be finite`);
  return Number(value);
};
const vec3 = (value, name) => {
  if (!Array.isArray(value) || value.length !== 3) fail(`${name} must be [x,y,z]`);
  return value.map((entry, index) => finite(entry, `${name}[${index}]`));
};
const vec2 = (value, name) => {
  if (!Array.isArray(value) || value.length !== 2) fail(`${name} must be [x,y]`);
  return value.map((entry, index) => finite(entry, `${name}[${index}]`));
};
const strings = (value, name) => {
  if (!Array.isArray(value) || value.some((entry) => typeof entry !== 'string' || !entry)) fail(`${name} must be a string array`);
  return [...value];
};
const sha256 = (buffer) => crypto.createHash('sha256').update(buffer).digest('hex');

const build = spec.world_build;
if (!build) fail('world_build is required');

const width = finite(spec.spatial_geometry?.overall_width_m, 'spatial_geometry.overall_width_m');
const depth = finite(spec.spatial_geometry?.overall_depth_m, 'spatial_geometry.overall_depth_m');
const height = finite(build.ceiling_height_m, 'world_build.ceiling_height_m');
const spawn = vec3(build.spawn?.position_m, 'world_build.spawn.position_m');
const spawnBuffer = finite(build.spawn?.buffer_radius_m, 'world_build.spawn.buffer_radius_m');
const socialCenter = vec3(build.social_core?.center_m, 'world_build.social_core.center_m');
const retreatCenter = vec3(build.retreat?.center_m, 'world_build.retreat.center_m');
const anchor = vec3(build.activity_anchor?.position_m, 'world_build.activity_anchor.position_m');
const heroPosition = vec3(build.hero_view?.position_m, 'world_build.hero_view.position_m');
const heroTarget = vec3(build.hero_view?.target_m, 'world_build.hero_view.target_m');
const waypoints = (build.circulation?.waypoints_m ?? []).map((point, index) => vec3(point, `world_build.circulation.waypoints_m[${index}]`));
if (waypoints.length < 2) fail('at least two circulation waypoints are required');

const inside = ([x, y, z], margin = 0) =>
  Math.abs(x) <= width / 2 - margin + 1e-9 &&
  Math.abs(y) <= depth / 2 - margin + 1e-9 &&
  z >= -1e-9 && z <= height + 1e-9;
const assertInside = (value, name, margin = 0) => {
  if (!inside(value, margin)) fail(`${name} is outside room footprint/height`);
};

assertInside(spawn, 'spawn.position_m');
assertInside(socialCenter, 'social_core.center_m');
assertInside(retreatCenter, 'retreat.center_m');
assertInside(anchor, 'activity_anchor.position_m');
assertInside(heroPosition, 'hero_view.position_m');
assertInside(heroTarget, 'hero_view.target_m');
waypoints.forEach((point, index) => assertInside(point, `circulation.waypoints_m[${index}]`));

const clearance = finite(build.circulation?.minimum_clearance_m, 'world_build.circulation.minimum_clearance_m');
for (const [index, point] of waypoints.entries()) {
  const wallClearance = Math.min(width / 2 - Math.abs(point[0]), depth / 2 - Math.abs(point[1]));
  if (wallClearance < clearance / 2 - 1e-9) fail(`circulation waypoint ${index} violates wall clearance`);
}
const spawnWallClearance = Math.min(width / 2 - Math.abs(spawn[0]), depth / 2 - Math.abs(spawn[1]));
if (spawnWallClearance < spawnBuffer - 1e-9) fail('spawn buffer penetrates room wall');

const zones = spec.spatial_geometry?.zones ?? [];
const windowZone = zones.find((zone) =>
  Number.isFinite(zone?.low_window_width_m) &&
  Number.isFinite(zone?.low_window_height_m) &&
  Number.isFinite(zone?.low_window_sill_m));
let viewOpening = null;
if (windowZone) {
  const openingWidth = finite(windowZone.low_window_width_m, `${windowZone.zone_id}.low_window_width_m`);
  const openingHeight = finite(windowZone.low_window_height_m, `${windowZone.zone_id}.low_window_height_m`);
  const openingSill = finite(windowZone.low_window_sill_m, `${windowZone.zone_id}.low_window_sill_m`);
  if (openingWidth >= width) fail('view opening must be narrower than room');
  if (openingSill + openingHeight >= height) fail('view opening must fit below ceiling');
  viewOpening = {wall: 'back', width: openingWidth, height: openingHeight, sill: openingSill};
}

const social = spec.social_clusters?.primary_core;
if (!social) fail('social_clusters.primary_core is required');
const socialDiameter = finite(social.core_diameter_m, 'social_clusters.primary_core.core_diameter_m');
const maxFaceDistance = finite(social.max_face_distance_m, 'social_clusters.primary_core.max_face_distance_m');
const seatCount = Number(build.social_core?.seat_count);
if (!Number.isInteger(seatCount) || seatCount < 2 || seatCount > 12) fail('social seat_count must be an integer in [2,12]');
const seatRadius = finite(build.social_core?.seat_radius_m, 'world_build.social_core.seat_radius_m');

const proceduralAssets = build.procedural_assets ?? {};
const sourceFor = (assetId) => {
  if (!assetId || typeof assetId !== 'string') fail('asset id must be a non-empty string');
  const geometry = proceduralAssets[assetId];
  if (geometry) return {source_kind: 'procedural_blockout', geometry};
  return {source_kind: 'glb'};
};

const tableAssetId = build.social_core?.table_asset_id;
const seatAssetId = build.social_core?.seat_asset_id;
const retreatAssetId = build.retreat?.seat_asset_id;
const socialSeats = [];
for (let index = 0; index < seatCount; index += 1) {
  const angle = (Math.PI * 2 * index) / seatCount;
  const position = [socialCenter[0] + Math.cos(angle) * seatRadius, socialCenter[1] + Math.sin(angle) * seatRadius, 0];
  assertInside(position, `social seat ${index + 1}`);
  socialSeats.push({
    id: `social-seat-${index + 1}`,
    asset_id: seatAssetId,
    ...sourceFor(seatAssetId),
    position_m: position,
    face_target_m: socialCenter,
  });
}
for (let i = 0; i < socialSeats.length; i += 1) {
  for (let j = i + 1; j < socialSeats.length; j += 1) {
    const a = socialSeats[i].position_m;
    const b = socialSeats[j].position_m;
    if (Math.hypot(a[0] - b[0], a[1] - b[1]) > maxFaceDistance + 1e-9) fail(`social seat pair ${i + 1}/${j + 1} exceeds max face distance`);
  }
}

const retreatSeatCount = Number(build.retreat?.seat_count);
if (!Number.isInteger(retreatSeatCount) || retreatSeatCount < 1 || retreatSeatCount > 4) fail('retreat seat_count must be in [1,4]');
const retreatReach = Math.min(...waypoints.map((point) => Math.hypot(point[0] - retreatCenter[0], point[1] - retreatCenter[1])));
if (retreatReach > clearance + 1e-9) fail('retreat center is not reachable from circulation path');

const anchorToSocial = Math.hypot(anchor[0] - socialCenter[0], anchor[1] - socialCenter[1]);
const socialRadius = socialDiameter / 2;
const anchorDistance = Math.min(...waypoints.map((point) => Math.hypot(point[0] - anchor[0], point[1] - anchor[1])));
const approachDistance = anchorToSocial <= socialRadius + 1e-9 ? Math.max(0, anchorDistance - socialRadius) : anchorDistance;
if (approachDistance > finite(build.activity_anchor?.approach_clearance_m, 'world_build.activity_anchor.approach_clearance_m') + 1e-9) {
  fail('activity anchor is not reachable from circulation path');
}

const blockoutAnchors = spec.blockout?.anchors ?? [];
const blockoutInstances = blockoutAnchors.map((entry, index) => {
  const id = entry?.id;
  if (!id || typeof id !== 'string') fail(`blockout.anchors[${index}].id is required`);
  const assetId = entry.asset_id;
  if (!assetId || typeof assetId !== 'string') fail(`blockout.anchors[${index}].asset_id is required`);
  const position = vec3(entry.position_m, `blockout.anchors[${index}].position_m`);
  assertInside(position, `blockout anchor ${id}`);
  return {
    id,
    instance_id: `Blockout_${id}`,
    asset_id: assetId,
    source_kind: 'glb',
    position_m: position,
    footprint_m: vec2(entry.footprint_m, `blockout.anchors[${index}].footprint_m`),
    height_m: finite(entry.height_m, `blockout.anchors[${index}].height_m`),
    yaw_deg: finite(entry.yaw_deg ?? 0, `blockout.anchors[${index}].yaw_deg`),
    role: entry.role ?? null,
  };
});
const blockoutById = new Map(blockoutInstances.map((entry) => [entry.id, entry]));

const compositionAxisIds = spec.blockout?.composition_axis ?? ['entrance', 'plaza'];
if (!Array.isArray(compositionAxisIds) || compositionAxisIds.length < 2) fail('blockout.composition_axis must contain at least two nodes');
const compositionAxis = compositionAxisIds.map((id, index) => {
  if (typeof id !== 'string' || !id) fail(`blockout.composition_axis[${index}] must be a non-empty string`);
  if (id === 'entrance') return {id, source: 'world_build.spawn', position_m: spawn};
  if (id === 'plaza') return {id, source: 'world_build.social_core', position_m: socialCenter};
  const instance = blockoutById.get(id);
  if (!instance) fail(`blockout.composition_axis references unresolved node ${id}`);
  return {id, source: `blockout.anchors.${id}`, position_m: instance.position_m};
});

const glbAssetIds = new Set();
const collectGlb = (record) => { if (record?.source_kind === 'glb') glbAssetIds.add(record.asset_id); };
const tableRecord = {asset_id: tableAssetId, ...sourceFor(tableAssetId), position_m: [socialCenter[0], socialCenter[1], 0]};
collectGlb(tableRecord);
socialSeats.forEach(collectGlb);
collectGlb({asset_id: retreatAssetId, ...sourceFor(retreatAssetId)});
blockoutInstances.forEach(collectGlb);

let visualLayers = null;
if (spec.blockout) {
  const practicalLights = strings(spec.lighting_design?.practical_lights ?? [], 'lighting_design.practical_lights');
  if (practicalLights.length < 1) fail('lighting_design.practical_lights must not be empty for blockout visual layers');
  const palette = spec.atmosphere?.palette ?? {};
  const requiredPaletteKeys = ['wood_brown', 'forest_green', 'warm_cream', 'accent_terracotta', 'lamp_gold'];
  for (const key of requiredPaletteKeys) {
    if (typeof palette[key] !== 'string' || !/^#[0-9A-Fa-f]{6}$/.test(palette[key])) fail(`atmosphere.palette.${key} must be #RRGGBB`);
  }
  const atmosphericDepth = spec.atmosphere?.atmospheric_depth ?? {};
  const atmosphericDensity = finite(atmosphericDepth.density ?? 0.018, 'atmosphere.atmospheric_depth.density');
  if (atmosphericDensity <= 0 || atmosphericDensity > 0.2) fail('atmosphere.atmospheric_depth.density must be in (0,0.2]');
  const atmosphericColorKey = atmosphericDepth.color_palette_key ?? 'warm_cream';
  if (!requiredPaletteKeys.includes(atmosphericColorKey)) fail(`atmosphere.atmospheric_depth.color_palette_key must reference ${requiredPaletteKeys.join(', ')}`);
  const materialPalette = {
    primary: strings(spec.material_palette?.primary ?? [], 'material_palette.primary'),
    secondary: strings(spec.material_palette?.secondary ?? [], 'material_palette.secondary'),
    avoid: strings(spec.material_palette?.avoid ?? [], 'material_palette.avoid'),
  };
  const lifeTraceIds = strings(spec.life_traces ?? [], 'life_traces');
  if (new Set(lifeTraceIds).size < 3) fail('life_traces must contain at least three distinct IDs');

  const requireAnchor = (id) => {
    const found = blockoutById.get(id);
    if (!found) fail(`visual layer requires missing blockout anchor ${id}`);
    return found;
  };
  const bind = (anchorId, offset) => {
    const base = requireAnchor(anchorId).position_m;
    return base.map((value, index) => value + offset[index]);
  };

  const practicalBindings = [
    {token: 'lodge_window', position_m: bind('lodge', [0, -0.12, 0.25]), energy_w: 22, radius_m: 0.32},
    {token: 'two_cottage_windows', position_m: bind('cottage_a', [0, -0.08, 0.18]), energy_w: 12, radius_m: 0.22},
    {token: 'two_cottage_windows', position_m: bind('cottage_b', [0, -0.08, 0.17]), energy_w: 12, radius_m: 0.22},
    {token: 'market_lantern', position_m: bind('market_stall', [0, -0.10, 0.22]), energy_w: 16, radius_m: 0.26},
    {token: 'bridge_lantern', position_m: bind('bridge', [0.18, 0, 0.16]), energy_w: 10, radius_m: 0.20},
  ];
  const resolvedTokens = new Set(practicalBindings.map((entry) => entry.token));
  for (const token of practicalLights) if (!resolvedTokens.has(token)) fail(`unresolved practical light token ${token}`);
  for (const token of resolvedTokens) if (!practicalLights.includes(token)) fail(`practical light binding is not canonical: ${token}`);

  const lifeTraceBindings = [
    {id: lifeTraceIds[0], kind: 'book', position_m: bind('reading_bench', [-0.05, 0.00, 0.18]), palette_key: 'warm_cream', classification: 'paper'},
    {id: lifeTraceIds[1], kind: 'mug', position_m: bind('reading_bench', [0.10, 0.01, 0.18]), palette_key: 'warm_cream', classification: 'matte_ceramic'},
    {id: lifeTraceIds[2], kind: 'firewood', position_m: bind('lodge', [-0.25, 0.13, 0.04]), palette_key: 'wood_brown', classification: 'wood'},
  ];
  const vegetationSources = {
    grass: 'woodland-grass-clump-01', fern: 'woodland-fern-01', moss: 'woodland-moss-patch-01',
    mushroom: 'woodland-mushroom-cluster-01', root: 'woodland-exposed-root-01',
  };
  const tableRadius = finite(tableRecord.geometry?.diameter_m, 'table diameter') / 2;
  const tabletopZ = finite(spec.blockout.tabletop_top_z_m, 'blockout.tabletop_top_z_m');
  const plazaRadius = Math.min(...requireAnchor('bridge').footprint_m) / 2;
  const vegetationConstraints = {
    tabletop_min_m: [socialCenter[0] - tableRadius, socialCenter[1] - tableRadius, tabletopZ],
    tabletop_max_m: [socialCenter[0] + tableRadius, socialCenter[1] + tableRadius, tabletopZ + 0.18],
    plaza_center_m: socialCenter, plaza_radius_m: plazaRadius,
  };
  const vegetationBudget = {max_instances: 64, max_triangles: 12000, max_materials: 12};
  const sourceSizes = new Map(Object.values(vegetationSources).map((id) => {
    const file = path.join('config/world-items', `${id}.json`);
    const source = JSON.parse(fs.readFileSync(file, 'utf8'));
    if (source.id !== id) fail(`vegetation source identity mismatch: ${id}`);
    return [id, vec3(source.dimensions_m, `${id}.dimensions_m`)];
  }));
  const naturalLayers = [];
  const clearFootprint = (position, radius, obstacle) => {
    const yaw = -obstacle.yaw_deg * Math.PI / 180;
    const dx = position[0] - obstacle.position_m[0], dy = position[1] - obstacle.position_m[1];
    const x = dx * Math.cos(yaw) - dy * Math.sin(yaw), y = dx * Math.sin(yaw) + dy * Math.cos(yaw);
    return Math.hypot(Math.max(0, Math.abs(x) - obstacle.footprint_m[0] / 2),
      Math.max(0, Math.abs(y) - obstacle.footprint_m[1] / 2)) >= radius + 0.012;
  };
  // Dense tree undergrowth, medium banks/buildings, empty central gathering space.
  // All offsets follow canonical footprints; no second placement spec or random scatter.
  const groups = [
    ['great_tree', 'moss', 3, 0.55], ['great_tree', 'root', 3, 0.5],
    ['great_tree', 'fern', 3, 0.6], ['great_tree', 'mushroom', 3, 0.7],
    ['stream', 'moss', 3, 0.55], ['stream', 'fern', 3, 0.55], ['stream', 'grass', 4, 0.75],
    ['bridge', 'moss', 2, 0.5], ['bridge', 'grass', 3, 0.7],
    ['lodge', 'grass', 4, 0.75], ['cottage_a', 'grass', 3, 0.65], ['cottage_b', 'grass', 3, 0.65],
  ];
  for (const [groupIndex, [anchorId, kind, count, scale]] of groups.entries()) {
    const base = requireAnchor(anchorId), assetId = vegetationSources[kind];
    const size = sourceSizes.get(assetId);
    // Origin can be asymmetric; use a conservative full-width radius.
    const radius = Math.hypot(size[0], size[1]) * scale * 0.65;
    const treeGround = anchorId === 'great_tree' && ['moss', 'root'].includes(kind);
    let placed = 0;
    for (let candidate = 0; candidate < 48 && placed < count; candidate += 1) {
      const angle = (candidate * 137.508 + groupIndex * 29) * Math.PI / 180;
      const rx = treeGround ? base.footprint_m[0] * 0.32 : base.footprint_m[0] / 2 + radius + 0.045;
      const ry = treeGround ? base.footprint_m[1] * 0.32 : base.footprint_m[1] / 2 + radius + 0.045;
      const yaw = base.yaw_deg * Math.PI / 180;
      const perimeter = treeGround ? 1 : Math.max(Math.abs(Math.cos(angle)), Math.abs(Math.sin(angle)));
      const dx = Math.cos(angle) * rx / perimeter, dy = Math.sin(angle) * ry / perimeter;
      const position = [base.position_m[0] + dx * Math.cos(yaw) - dy * Math.sin(yaw),
        base.position_m[1] + dx * Math.sin(yaw) + dy * Math.cos(yaw), tabletopZ + 0.001];
      if (Math.hypot(position[0] - socialCenter[0], position[1] - socialCenter[1]) + radius > tableRadius - 0.025) continue;
      if (Math.hypot(position[0] - socialCenter[0], position[1] - socialCenter[1]) < plazaRadius + radius + 0.015) continue;
      if (blockoutInstances.some(obstacle => !(treeGround && obstacle.id === anchorId) && !clearFootprint(position, radius, obstacle))) continue;
      if (naturalLayers.some(other => other.kind === kind && Math.hypot(other.position_m[0] - position[0], other.position_m[1] - position[1]) < radius * 1.3)) continue;
      const record = {instance_id: `Vegetation_${anchorId}_${kind}_${placed + 1}`, kind,
        asset_id: assetId, source_kind: 'glb', anchor_id: anchorId, position_m: position,
        scale, yaw_deg: (candidate * 137.508 + base.yaw_deg) % 360};
      naturalLayers.push(record);
      collectGlb(record);
      placed += 1;
    }
    if (placed < count) fail(`insufficient clear vegetation placements: ${anchorId}/${kind} ${placed}/${count}`);
  }


  visualLayers = {
    practical_lights: practicalLights,
    practical_light_bindings: practicalBindings,
    palette: Object.fromEntries(requiredPaletteKeys.map((key) => [key, palette[key]])),
    material_palette: materialPalette,
    life_trace_ids: lifeTraceIds,
    life_trace_bindings: lifeTraceBindings,
    natural_layer_kinds: naturalLayers.map((entry) => entry.kind),
    natural_layers: naturalLayers,
    vegetation_constraints: vegetationConstraints,
    vegetation_budget: vegetationBudget,
    atmospheric_depth: {
      enabled: true,
      mechanism: 'world_volume_haze',
      density: atmosphericDensity,
      color_palette_key: atmosphericColorKey,
      camera_clip_start_m: finite(spec.runtime_budget?.camera_near_clip_m, 'runtime_budget.camera_near_clip_m'),
      camera_clip_end_m: finite(spec.runtime_budget?.background_max_distance_m, 'runtime_budget.background_max_distance_m'),
    },
  };
}

const raw = fs.readFileSync(specPath);
const plan = {
  schema_version: 4,
  source_spec: path.relative(process.cwd(), specPath).replaceAll('\\', '/'),
  source_spec_sha256: sha256(raw),
  world_id: path.basename(specPath, path.extname(specPath)),
  theme_name: spec.meta?.theme_name ?? path.basename(specPath),
  platform_target: spec.meta?.platform_target ?? null,
  footprint: {width, depth, height},
  ...(viewOpening ? {view_opening: viewOpening} : {}),
  spawn: {position_m: spawn, facing_deg: finite(build.spawn?.facing_deg, 'world_build.spawn.facing_deg'), buffer_radius_m: spawnBuffer},
  social_core: {
    center_m: socialCenter,
    diameter: socialDiameter,
    max_face_distance: maxFaceDistance,
    table_height_m: finite(social.table_height_m, 'social_clusters.primary_core.table_height_m'),
    table: tableRecord,
    seats: socialSeats,
  },
  retreat: {zone_id: build.retreat?.zone_id, center_m: retreatCenter, seat_count: retreatSeatCount, asset_id: retreatAssetId, seat_asset_id: retreatAssetId, ...sourceFor(retreatAssetId)},
  activity_anchor: {kind: build.activity_anchor?.kind, position_m: anchor, approach_clearance_m: finite(build.activity_anchor?.approach_clearance_m, 'world_build.activity_anchor.approach_clearance_m')},
  hero_view: {position_m: heroPosition, target_m: heroTarget},
  circulation_contract: {waypoints_m: waypoints, minimum_clearance: clearance},
  composition_axis: compositionAxis,
  blockout_instances: blockoutInstances,
  asset_ids: [...glbAssetIds].sort(),
  ...(visualLayers ? {visual_layers: visualLayers} : {}),
  runtime: {
    realtime_lights: Number(spec.runtime_budget?.realtime_light_count ?? 0),
    max_size_pc_mb: Number(spec.runtime_budget?.max_size_pc_mb ?? 0),
    max_size_quest_mb: Number(spec.runtime_budget?.max_size_quest_mb ?? 0),
  },
};
process.stdout.write(`${JSON.stringify(plan, null, 2)}\n`);
