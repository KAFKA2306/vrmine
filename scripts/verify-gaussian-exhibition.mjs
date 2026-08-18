import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const exhibition = JSON.parse(await readFile(new URL('../config/gaussian-exhibition.json', import.meta.url), 'utf8'));
const sources = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));

assert.equal(exhibition.schema_version, 1, 'unsupported gaussian exhibition schema');
assert.equal(exhibition.expected_exhibits, 20, 'the exhibition must define exactly 20 slots');
assert.equal(exhibition.canonical_platform, 'windows');
assert.equal(exhibition.source_registry, 'config/gaussian-splats.json');
assert.equal(exhibition.renderer, sources.renderers.unity_vrchat, 'scene renderer must match the canonical source registry');
assert.equal(exhibition.target_extent_m, 1, 'exhibits target an approximately 1 m normalized extent');
assert.ok(exhibition.scene_path.endsWith('.unity'), 'scene_path must point to a Unity scene');
assert.ok(Array.isArray(exhibition.exhibits), 'exhibits must be an array');
assert.equal(exhibition.exhibits.length, exhibition.expected_exhibits, 'exhibition slot count mismatch');

for (const vector of [
  exhibition.floor?.position,
  exhibition.floor?.scale,
  exhibition.spawn?.position,
  exhibition.reference_camera?.position,
  exhibition.reference_camera?.rotation_euler_degrees,
  exhibition.video_player?.position,
]) {
  assert.ok(Array.isArray(vector) && vector.length === 3, 'scene vectors must contain three values');
}
assert.ok(exhibition.floor.scale.every((value) => Number.isFinite(value) && value > 0), 'floor scale must be positive');
assert.ok(Number.isFinite(exhibition.reference_camera.field_of_view) && exhibition.reference_camera.field_of_view > 0);
assert.ok(['blocked_playlist', 'ready'].includes(exhibition.video_player.status), 'invalid video player status');
if (exhibition.video_player.status === 'blocked_playlist') {
  assert.equal(exhibition.video_player.prefab_path, null);
  assert.equal(exhibition.video_player.playlist_manifest, null);
} else {
  assert.ok(exhibition.video_player.prefab_path?.endsWith('.prefab'), 'ready video player needs a prefab');
  assert.ok(exhibition.video_player.playlist_manifest?.endsWith('.json'), 'ready video player needs a playlist manifest');
}

const sourceById = new Map(sources.environments.map((entry) => [entry.id, entry]));
assert.equal(sourceById.size, sources.environments.length, 'source ids must be unique before scene assignment');

const indexes = new Set();
const positions = new Set();
const assignedSources = new Set();
let registered = 0;
let blocked = 0;

for (const exhibit of exhibition.exhibits) {
  assert.ok(Number.isInteger(exhibit.display_index), 'display_index must be an integer');
  assert.ok(exhibit.display_index >= 1 && exhibit.display_index <= exhibition.expected_exhibits, 'display_index out of range');
  assert.ok(!indexes.has(exhibit.display_index), `duplicate display_index ${exhibit.display_index}`);
  indexes.add(exhibit.display_index);

  for (const key of ['position', 'rotation_euler_degrees', 'scale']) {
    assert.ok(Array.isArray(exhibit[key]) && exhibit[key].length === 3, `${key} must contain three values`);
    assert.ok(exhibit[key].every(Number.isFinite), `${key} must contain finite values`);
  }
  assert.ok(exhibit.scale.every((value) => value > 0), 'exhibit scale must be positive');
  const positionKey = exhibit.position.join(',');
  assert.ok(!positions.has(positionKey), `duplicate exhibit position ${positionKey}`);
  positions.add(positionKey);

  if (exhibit.source_id === null) {
    blocked++;
    assert.equal(exhibit.status, 'blocked_source');
    assert.equal(exhibit.prefab_path, null);
    continue;
  }

  registered++;
  assert.equal(exhibit.status, 'source_registered');
  assert.ok(sourceById.has(exhibit.source_id), `unknown source_id ${exhibit.source_id}`);
  assert.ok(!assignedSources.has(exhibit.source_id), `source assigned more than once: ${exhibit.source_id}`);
  assignedSources.add(exhibit.source_id);
  assert.equal(
    exhibit.prefab_path,
    `Assets/KafkaMade/VRMine/GaussianSplatting/Prefabs/${exhibit.source_id}.prefab`,
    `unexpected prefab path for ${exhibit.source_id}`,
  );
}

assert.equal(indexes.size, exhibition.expected_exhibits);
assert.equal(registered, sources.environments.length, 'every registered source must be assigned exactly once');
assert.equal(assignedSources.size, sources.environments.length, 'registered source assignment coverage mismatch');
assert.equal(blocked, exhibition.expected_exhibits - sources.environments.length, 'blocked slot count must match missing sources');
assert.ok(sources.environments.length <= exhibition.expected_exhibits, 'source registry exceeds exhibition capacity');

console.log(
  `Validated Gaussian exhibition: slots=${exhibition.expected_exhibits}, source_registered=${registered}, blocked_source=${blocked}`,
);
