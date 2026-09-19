import fs from 'node:fs';

const path = 'config/world-media-audio.json';
const fail = (message) => { console.error(`FAIL: ${message}`); process.exitCode = 1; };
const assert = (condition, message) => { if (!condition) fail(message); };
const c = JSON.parse(fs.readFileSync(path, 'utf8'));

assert(c.authority === 'world_media_audio', 'one canonical media/audio authority is required');
assert(c.player?.abstraction && c.player?.production_provider, 'source/player abstraction and production provider are required');
assert(c.player?.single_playback_authority === true, 'playback authority must be singular');
assert(Array.isArray(c.player?.source_kinds) && c.player.source_kinds.includes('stream'), 'stream source must use the common abstraction');
assert(c.player?.external_streaming?.production_required === false, 'WebRTC must remain optional');
assert(c.player?.external_streaming?.state_without_runtime_evidence === 'UNVERIFIED', 'unmeasured streaming must remain UNVERIFIED');

const presets = Object.values(c.spatial_audio?.presets ?? {});
assert(c.spatial_audio?.component === 'VRC_SpatialAudioSource', 'spatial audio must use VRC_SpatialAudioSource');
assert(presets.length >= 2, 'at least two reusable spatial presets are required');
for (const p of presets) assert(Number.isFinite(p.near_m) && Number.isFinite(p.far_m) && p.near_m < p.far_m, 'each spatial preset needs finite near/far distances with near < far');

const events = c.audiolink?.rebind_events ?? [];
assert(c.audiolink?.binding === 'runtime_discovery', 'AudioLink must use runtime discovery');
assert(events.includes('OnEnable') && events.includes('OnPlayerRestored'), 'AudioLink must rebind after lifecycle/player restoration');
assert(c.audiolink?.silent_fallback === false, 'AudioLink may not silently fall back');

const cases = c.editor_test?.required_cases ?? [];
for (const required of ['source_switch', 'spatial_preset', 'audiolink_rebind', 'missing_optional_provider']) assert(cases.includes(required), `editor test contract missing ${required}`);

for (const platform of ['pc', 'android']) {
  const p = c.platforms?.[platform];
  assert(p && Number.isFinite(p.latency_target_ms) && p.latency_target_ms > 0, `${platform} latency constraint is required`);
}
assert(c.platforms.pc.latency_target_ms !== c.platforms.android.latency_target_ms, 'PC and Android latency constraints must be distinct');
assert(c.evidence?.missing_measurement === 'UNVERIFIED', 'missing measurements must remain UNVERIFIED');

if (!process.exitCode) console.log('PASS: world media/audio contract');
