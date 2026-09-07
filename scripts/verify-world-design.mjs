import fs from 'node:fs';

const readJson = (p) => JSON.parse(fs.readFileSync(p, 'utf8'));
const fail = (m) => { throw new Error(`World Design FAIL: ${m}`); };

const dataset = readJson('config/world-design/source-records.json');
const schema = readJson('config/world-design/world-design-spec.schema.json');
const rulesDoc = readJson('config/world-design/engineering-rules.json');
const summary = readJson('config/world-design/summary.json');
const spec = readJson('config/world-design/generated/cyber-alley-tea-nook.json');

if (dataset.schema_version !== 1) fail('dataset schema_version must be 1');
if (!Array.isArray(dataset.records) || dataset.records.length < 20) fail('at least 20 real source records are required');

const requiredCategories = ['source','use','spatial','experience','light_sound','material_runtime','production','evidence'];
const ids = new Set();
for (const record of dataset.records) {
  if (!record.id || ids.has(record.id)) fail(`invalid/duplicate record id: ${record.id}`);
  ids.add(record.id);
  for (const key of requiredCategories) if (record[key] === undefined) fail(`${record.id} missing ${key}`);
  if (!['VRChat','BOOTH'].includes(record.source.source_type)) fail(`${record.id} invalid source_type`);
  for (const key of ['canonical_url','title','creator','captured_at']) if (!record.source[key]) fail(`${record.id} missing source.${key}`);
  if (!Array.isArray(record.source.evidence_urls) || record.source.evidence_urls.length < 1) fail(`${record.id} missing evidence URLs`);
  if (!Array.isArray(record.evidence) || record.evidence.length < 1) fail(`${record.id} missing evidence entries`);
  for (const e of record.evidence) {
    if (!Array.isArray(e.fields) || e.fields.length < 1 || !e.url) fail(`${record.id} has incomplete evidence`);
    if (!['HIGH','MEDIUM','LOW','UNVERIFIED'].includes(e.confidence)) fail(`${record.id} invalid evidence confidence`);
    if (!e.observation_type) fail(`${record.id} evidence missing observation_type`);
  }
  const forbiddenUnknownStrings = [];
  const walk = (v, p='') => {
    if (typeof v === 'string' && /^(UNKNOWN|UNVERIFIED)$/i.test(v)) forbiddenUnknownStrings.push(p);
    if (Array.isArray(v)) v.forEach((x,i)=>walk(x,`${p}[${i}]`));
    else if (v && typeof v === 'object') Object.entries(v).forEach(([k,x])=>walk(x,p?`${p}.${k}`:k));
  };
  walk(record);
  if (forbiddenUnknownStrings.length) fail(`${record.id} must use null for unknown values: ${forbiddenUnknownStrings.join(', ')}`);
}

if (schema.title !== 'WorldDesignSpec' || schema.version !== '1.1.0') fail('WorldDesignSpec schema must be v1.1.0');
for (const key of schema.required ?? []) if (spec[key] === undefined) fail(`generated spec missing ${key}`);

const allowedIntents = new Set(schema.properties.meta.properties.primary_intents.items.enum);
if (!Array.isArray(spec.meta.primary_intents) || spec.meta.primary_intents.some(x => !allowedIntents.has(x))) fail('invalid primary_intents');
if (spec.meta.capacity_target < 1 || spec.meta.capacity_target > 40) fail('capacity_target out of range');
if (!['PC_ONLY','CROSS_PLATFORM_QUEST'].includes(spec.meta.platform_target)) fail('invalid platform_target');

const rules = new Map(rulesDoc.rules.map(r => [r.id, r]));
const rule = (id) => {
  if (!rules.has(id)) fail(`missing engineering rule ${id}`);
  return rules.get(id);
};

if (spec.spatial_geometry.corridor_clearance_m < rule('CORRIDOR-MIN').min) fail('DRC-01 corridor clearance');
if (spec.spatial_geometry.door_height_m < rule('DOOR-MIN').height_min) fail('DRC-01 door height');
if (spec.spatial_geometry.door_width_m < rule('DOOR-MIN').width_min) fail('DRC-01 door width');
if (spec.retreat_protocol.visual_shield_angle_deg < rule('RETREAT-VISUAL').min) fail('DRC-02 visual shield');
if (spec.retreat_protocol.acoustic_attenuation_db > rule('RETREAT-AUDIO').max) fail('DRC-02 acoustic shield');
for (const required of ['local_dimming','auto_on_low_mirror']) {
  if (!spec.retreat_protocol.affordance_components.includes(required)) fail(`DRC-02 retreat missing ${required}`);
}
if (spec.optical_lighting.seating_direct_sunlight_ratio_percent !== 0) fail('DRC-03 seating direct sunlight must be zero');

const tpu = spec.optical_lighting.tpu_allocation;
for (const [field,id] of [['avatar_contact_surfaces','TPU-CONTACT'],['interior_walls','TPU-WALL'],['exterior_background','TPU-BACKGROUND']]) {
  const r = rule(id);
  if (tpu[field] < r.min || tpu[field] > r.max) fail(`DRC-04 ${field}`);
}
if (spec.runtime_budget.camera_near_clip_m === rule('NEARCLIP').value &&
    spec.runtime_budget.background_max_distance_m > rule('BACKGROUND-MAX-WITH-NEARCLIP').max) fail('DRC-05 background distance');
if (spec.media_governance.video_player_mode === rule('MEDIA-GOVERNANCE').forbidden) fail('DRC-06 media dominance');

const provenanceTargets = [
  'meta.primary_intents',
  'meta.platform_target',
  'spatial_geometry.furniture_scale_factor',
  'spatial_geometry.zones.tatami_lounge.height_m',
  'social_clusters.primary_core.core_diameter_m',
  'spatial_geometry.corridor_clearance_m',
  'spatial_geometry.door_height_m',
  'spatial_geometry.door_width_m',
  'retreat_protocol.visual_shield_angle_deg',
  'retreat_protocol.acoustic_attenuation_db',
  'optical_lighting.tpu_allocation',
  'runtime_budget.camera_near_clip_m',
  'media_governance.video_player_mode'
];
for (const target of provenanceTargets) {
  const refs = spec.provenance.decisions?.[target];
  if (!Array.isArray(refs) || refs.length < 1) fail(`missing provenance for ${target}`);
  for (const ref of refs) if (!ids.has(ref) && !rules.has(ref)) fail(`unknown provenance ref ${ref}`);
}
for (const id of spec.provenance.market_record_ids ?? []) if (!ids.has(id)) fail(`unknown market record ${id}`);

const sourceCounts = dataset.records.reduce((a,r)=>(a[r.source.source_type]=(a[r.source.source_type]??0)+1,a),{});
const questValues = dataset.records.map(r=>r.material_runtime.quest_support);
const quest = {
  known_true: questValues.filter(v=>v===true).length,
  known_false: questValues.filter(v=>v===false).length,
  unknown: questValues.filter(v=>v===null).length
};
const downloadObserved = dataset.records.filter(r=>Number.isFinite(r.material_runtime.download_size_mb)).length;
const priceObserved = dataset.records.filter(r=>Number.isFinite(r.production.price_jpy)).length;
if (summary.record_count !== dataset.records.length) fail('summary record_count drift');
if (JSON.stringify(summary.source_type_counts) !== JSON.stringify(sourceCounts)) fail('summary source_type_counts drift');
if (JSON.stringify(summary.quest_support) !== JSON.stringify(quest)) fail('summary quest_support drift');
if (summary.download_size_mb.observed_count !== downloadObserved) fail('summary download observed_count drift');
if (summary.price_jpy.observed_count !== priceObserved) fail('summary price observed_count drift');
if (summary.dimension_distribution_status !== 'UNVERIFIED_IN_PUBLIC_SOURCES') fail('unobserved market dimensions must stay unverified');

console.log(JSON.stringify({
  status:'PASS',
  records:dataset.records.length,
  source_type_counts:sourceCounts,
  quest_support:quest,
  download_size_observed:downloadObserved,
  price_observed:priceObserved,
  generated_spec:spec.meta.theme_name,
  drc_rules:6,
  provenance_targets:provenanceTargets.length
}, null, 2));
