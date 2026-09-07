import fs from 'node:fs';

const specPath = process.argv[2] ?? 'config/world-design/generated/cyber-alley-tea-nook.json';
const fail = (m) => { throw new Error(`World Design consumer FAIL: ${m}`); };
const spec = JSON.parse(fs.readFileSync(specPath, 'utf8'));

const finite = (value, path) => {
  if (!Number.isFinite(value)) fail(`${path} must be a finite number`);
  return value;
};
const zones = spec.spatial_geometry?.zones;
if (!Array.isArray(zones) || zones.length < 1) fail('spatial_geometry.zones must be a non-empty array');

const roomPlan = {
  schema_version: 1,
  units: 'm',
  source_spec: specPath,
  footprint: {
    width: finite(spec.spatial_geometry?.overall_width_m, 'spatial_geometry.overall_width_m'),
    depth: finite(spec.spatial_geometry?.overall_depth_m, 'spatial_geometry.overall_depth_m')
  },
  circulation: {
    clearance: finite(spec.spatial_geometry?.corridor_clearance_m, 'spatial_geometry.corridor_clearance_m'),
    door_height: finite(spec.spatial_geometry?.door_height_m, 'spatial_geometry.door_height_m'),
    door_width: finite(spec.spatial_geometry?.door_width_m, 'spatial_geometry.door_width_m')
  },
  zones: zones.map((zone, index) => {
    if (!zone.zone_id) fail(`spatial_geometry.zones[${index}].zone_id is required`);
    const plan = { id: zone.zone_id };
    for (const key of ['width_m','depth_m','height_m']) {
      if (zone[key] !== undefined) plan[key.replace('_m','')] = finite(zone[key], `spatial_geometry.zones[${index}].${key}`);
    }
    if (zone.scale_compression !== undefined) plan.scale = finite(zone.scale_compression, `spatial_geometry.zones[${index}].scale_compression`);
    if (!('width' in plan) && !('scale' in plan)) fail(`zone ${zone.zone_id} needs dimensions or scale_compression`);
    return plan;
  }),
  social_core: {
    diameter: finite(spec.social_clusters?.primary_core?.core_diameter_m, 'social_clusters.primary_core.core_diameter_m'),
    max_face_distance: finite(spec.social_clusters?.primary_core?.max_face_distance_m, 'social_clusters.primary_core.max_face_distance_m')
  },
  runtime: {
    realtime_lights: finite(spec.runtime_budget?.realtime_light_count, 'runtime_budget.realtime_light_count'),
    near_clip: finite(spec.runtime_budget?.camera_near_clip_m, 'runtime_budget.camera_near_clip_m')
  }
};

process.stdout.write(JSON.stringify(roomPlan, null, 2) + '\n');
