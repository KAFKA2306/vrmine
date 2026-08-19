import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const exhibition = JSON.parse(await readFile(new URL('../config/gaussian-exhibition.json', import.meta.url), 'utf8'));
const sources = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));
const playlist = JSON.parse(await readFile(new URL('../config/gaussian-video-playlist.json', import.meta.url), 'utf8'));

assert.equal(exhibition.schema_version, 2, 'unsupported gaussian exhibition schema');
assert.equal(exhibition.final_expected_exhibits, 20, 'the #72 final product still requires exactly 20 exhibits');
assert.equal(exhibition.canonical_platform, 'windows');
assert.equal(exhibition.source_registry, 'config/gaussian-splats.json');
assert.equal(exhibition.renderer, sources.renderers.unity_vrchat, 'scene renderer must match the canonical source registry');
assert.equal(exhibition.target_extent_m, 1, 'exhibits target an approximately 1 m normalized extent');
assert.equal(exhibition.scene_path, 'Assets/KafkaMade/VRMine/Scenes/GaussianSplatExhibition.unity');
assert.ok(!Object.hasOwn(exhibition, 'exhibits'), 'scene config must not duplicate a fixed-count exhibit list');
assert.ok(!Object.hasOwn(exhibition, 'floor'), 'floor dimensions must derive from the registered source count/layout');
assert.ok(!Object.hasOwn(exhibition, 'spawn'), 'spawn must derive from the generated floor/layout');

for (const key of ['center_spacing_m', 'aisle_width_m', 'pad_size_m', 'wall_height_m']) {
  assert.ok(Number.isFinite(exhibition.layout?.[key]) && exhibition.layout[key] > 0, `${key} must be positive`);
}
assert.ok(Number.isFinite(exhibition.layout?.margin_m) && exhibition.layout.margin_m >= 0, 'margin_m must be non-negative');
assert.ok(Number.isFinite(exhibition.reference_camera?.field_of_view) && exhibition.reference_camera.field_of_view > 0, 'reference camera FOV must be positive');
assert.equal(exhibition.video_player?.prefab_path, playlist.player_prefab_path, 'scene and playlist must use the same canonical SDK player prefab');
assert.equal(exhibition.video_player?.playlist_manifest, 'config/gaussian-video-playlist.json');

assert.ok(Array.isArray(sources.environments) && sources.environments.length >= 1, 'local pipeline requires at least one registered source');
const ids = new Set();
for (const [index, source] of sources.environments.entries()) {
  assert.ok(typeof source.id === 'string' && source.id.length > 0, `source ${index + 1} id is missing`);
  assert.ok(!ids.has(source.id), `duplicate source id ${source.id}`);
  ids.add(source.id);
  assert.ok(Number.isInteger(source.source?.size_bytes) && source.source.size_bytes > 0, `${source.id}: size_bytes missing`);
  assert.match(source.source?.sha256 ?? '', /^[0-9a-f]{64}$/, `${source.id}: sha256 missing or invalid`);
}

assert.equal(playlist.expected_entries, exhibition.final_expected_exhibits, 'final playlist count must follow the final product count');

console.log(`Validated count-independent Gaussian exhibition contract: registered=${sources.environments.length}, final_required=${exhibition.final_expected_exhibits}, renderer=1`);
