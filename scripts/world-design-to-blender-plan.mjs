import fs from 'node:fs';
import crypto from 'node:crypto';

const specPath = process.argv[2] ?? 'config/world-design/generated/cyber-alley-tea-nook.json';
const fail = (m) => { throw new Error(`World Design consumer FAIL: ${m}`); };
const raw = fs.readFileSync(specPath, 'utf8');
const spec = JSON.parse(raw);

const finite = (value, path) => {
  if (!Number.isFinite(value)) fail(`${path} must be a finite number`);
  return value;
};
const vec3 = (value, path) => {
  if (!Array.isArray(value) || value.length !== 3) fail(`${path} must be a 3-vector`);
  return value.map((v, i) => finite(v, `${path}[${i}]`));
};
const positiveInt = (value, path) => {
  if (!Number.isInteger(value) || value < 1) fail(`${path} must be a positive integer`);
  return value;
};
const requiredString = (value, path) => {
  if (typeof value !== 'string' || value.length === 0) fail(`${path} is required`);
  return value;
};

const zones = spec.spatial_geometry?.zones;
if (!Array.isArray(zones) || zones.length < 1) fail('spatial_geometry.zones must be a non-empty array');
const build = spec.world_build;
if (!build || typeof build !== 'object') fail('world_build is required for physical materialization');

const roomWidth = finite(spec.spatial_geometry?.overall_width_m, 'spatial_geometry.overall_width_m');
const roomDepth = finite(spec.spatial_geometry?.overall_depth_m, 'spatial_geometry.overall_depth_m');
const roomHeight = finite(build.ceiling_height_m, 'world_build.ceiling_height_m');
const halfW = roomWidth / 2;
const halfD = roomDepth / 2;
const assertInside = (p, path) => {
  if (Math.abs(p[0]) > halfW || Math.abs(p[1]) > halfD || p[2] < 0 || p[2] > roomHeight) fail(`${path} lies outside room bounds`);
};

const spawn = vec3(build.spawn?.position_m, 'world_build.spawn.position_m');
const socialCenter = vec3(build.social_core?.center_m, 'world_build.social_core.center_m');
const retreatCenter = vec3(build.retreat?.center_m, 'world_build.retreat.center_m');
const anchor = vec3(build.activity_anchor?.position_m, 'world_build.activity_anchor.position_m');
const heroPosition = vec3(build.hero_view?.position_m, 'world_build.hero_view.position_m');
const heroTarget = vec3(build.hero_view?.target_m, 'world_build.hero_view.target_m');
for (const [p, path] of [[spawn, 'spawn'], [socialCenter, 'social_core'], [retreatCenter, 'retreat'], [anchor, 'activity_anchor'], [heroPosition, 'hero_view.position'], [heroTarget, 'hero_view.target']]) assertInside(p, path);

const seatCount = positiveInt(build.social_core?.seat_count, 'world_build.social_core.seat_count');
const seatRadius = finite(build.social_core?.seat_radius_m, 'world_build.social_core.seat_radius_m');
if (seatRadius <= 0) fail('world_build.social_core.seat_radius_m must be > 0');
const seats = Array.from({length: seatCount}, (_, i) => {
  const a = (2 * Math.PI * i) / seatCount;
  const position = [socialCenter[0] + Math.cos(a) * seatRadius, socialCenter[1] + Math.sin(a) * seatRadius, 0];
  assertInside(position, `social_seats[${i}]`);
  return {id: `social-seat-${i + 1}`, asset_id: requiredString(build.social_core.seat_asset_id, 'world_build.social_core.seat_asset_id'), position_m: position, facing_target_m: socialCenter};
});

const waypoints = build.circulation?.waypoints_m;
if (!Array.isArray(waypoints) || waypoints.length < 2) fail('world_build.circulation.waypoints_m must have at least two points');
const circulation = waypoints.map((v, i) => {
  const p = vec3(v, `world_build.circulation.waypoints_m[${i}]`);
  assertInside(p, `circulation[${i}]`);
  return p;
});
const clearance = finite(build.circulation?.minimum_clearance_m, 'world_build.circulation.minimum_clearance_m');
if (clearance <= 0) fail('world_build.circulation.minimum_clearance_m must be > 0');
for (const [i, p] of circulation.entries()) {
  if (halfW - Math.abs(p[0]) < clearance / 2 || halfD - Math.abs(p[1]) < clearance / 2) fail(`circulation[${i}] violates half-clearance from room wall`);
}
const anchorDistance = Math.min(...circulation.map((p) => Math.hypot(p[0] - anchor[0], p[1] - anchor[1])));
if (anchorDistance > finite(build.activity_anchor?.approach_clearance_m, 'world_build.activity_anchor.approach_clearance_m')) fail('circulation does not reach the activity anchor approach zone');
const retreatDistance = Math.min(...circulation.map((p) => Math.hypot(p[0] - retreatCenter[0], p[1] - retreatCenter[1])));
if (retreatDistance > clearance) fail('circulation does not reach the retreat zone');

const windowZone = zones.find((z) => Number.isFinite(z.low_window_width_m) && Number.isFinite(z.low_window_height_m) && Number.isFinite(z.low_window_sill_m));
if (!windowZone) fail('a physical view opening with width/height/sill is required');

const roomPlan = {
  schema_version: 3,
  units: 'm',
  source_spec: specPath,
  source_spec_sha256: crypto.createHash('sha256').update(raw).digest('hex'),
  footprint: {width: roomWidth, depth: roomDepth, height: roomHeight},
  view_opening: {
    zone_id: requiredString(windowZone.zone_id, 'view opening zone_id'),
    wall: 'positive_y',
    width: finite(windowZone.low_window_width_m, `${windowZone.zone_id}.low_window_width_m`),
    height: finite(windowZone.low_window_height_m, `${windowZone.zone_id}.low_window_height_m`),
    sill: finite(windowZone.low_window_sill_m, `${windowZone.zone_id}.low_window_sill_m`)
  },
  circulation_contract: {
    clearance: finite(spec.spatial_geometry?.corridor_clearance_m, 'spatial_geometry.corridor_clearance_m'),
    minimum_clearance: clearance,
    door_height: finite(spec.spatial_geometry?.door_height_m, 'spatial_geometry.door_height_m'),
    door_width: finite(spec.spatial_geometry?.door_width_m, 'spatial_geometry.door_width_m'),
    waypoints_m: circulation
  },
  zones: zones.map((zone, index) => {
    if (!zone.zone_id) fail(`spatial_geometry.zones[${index}].zone_id is required`);
    const plan = {id: zone.zone_id};
    for (const key of ['width_m','depth_m','height_m']) if (zone[key] !== undefined) plan[key.replace('_m','')] = finite(zone[key], `spatial_geometry.zones[${index}].${key}`);
    if (zone.scale_compression !== undefined) plan.scale = finite(zone.scale_compression, `spatial_geometry.zones[${index}].scale_compression`);
    if (!('width' in plan) && !('scale' in plan)) fail(`zone ${zone.zone_id} needs dimensions or scale_compression`);
    return plan;
  }),
  spawn: {position_m: spawn, facing_deg: finite(build.spawn?.facing_deg, 'world_build.spawn.facing_deg'), buffer_radius_m: finite(build.spawn?.buffer_radius_m, 'world_build.spawn.buffer_radius_m')},
  social_core: {
    center_m: socialCenter,
    diameter: finite(spec.social_clusters?.primary_core?.core_diameter_m, 'social_clusters.primary_core.core_diameter_m'),
    max_face_distance: finite(spec.social_clusters?.primary_core?.max_face_distance_m, 'social_clusters.primary_core.max_face_distance_m'),
    table: {asset_id: requiredString(build.social_core.table_asset_id, 'world_build.social_core.table_asset_id'), position_m: [socialCenter[0], socialCenter[1], 0]},
    seats
  },
  retreat: {zone_id: requiredString(build.retreat?.zone_id, 'world_build.retreat.zone_id'), center_m: retreatCenter, seat_count: positiveInt(build.retreat?.seat_count, 'world_build.retreat.seat_count'), seat_asset_id: requiredString(build.retreat?.seat_asset_id, 'world_build.retreat.seat_asset_id')},
  activity_anchor: {kind: requiredString(build.activity_anchor?.kind, 'world_build.activity_anchor.kind'), position_m: anchor, approach_clearance_m: finite(build.activity_anchor?.approach_clearance_m, 'world_build.activity_anchor.approach_clearance_m')},
  hero_view: {position_m: heroPosition, target_m: heroTarget},
  runtime: {realtime_lights: finite(spec.runtime_budget?.realtime_light_count, 'runtime_budget.realtime_light_count'), near_clip: finite(spec.runtime_budget?.camera_near_clip_m, 'runtime_budget.camera_near_clip_m')}
};

process.stdout.write(JSON.stringify(roomPlan, null, 2) + '\n');
