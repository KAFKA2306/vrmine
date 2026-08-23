import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const exhibition = JSON.parse(await readFile(new URL('../config/gaussian-exhibition.json', import.meta.url), 'utf8'));
const sources = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));

const expectedPlayer = 'Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VideoPlayers/UdonSyncPlayer (Unity).prefab';
assert.equal(exhibition.video_player?.prefab_path, expectedPlayer, 'exhibition must use the canonical SDK player prefab');
assert.ok(Array.isArray(sources.environments) && sources.environments.length >= 1, 'playback requires at least one registered source');

let ready = 0;
for (const [index, entry] of sources.environments.entries()) {
  assert.equal(entry.display_index, index + 1, `slot ${index + 1} must keep canonical display order`);
  assert.equal(entry.playback?.status, 'ready_untrusted', `${entry.id} playback must be ready_untrusted`);
  assert.equal(entry.playback?.requires_untrusted_urls, true, `${entry.id} Wikimedia playback requires untrusted URLs`);
  const url = new URL(entry.playback?.url);
  assert.equal(url.protocol, 'https:', `${entry.id} playback URL must use HTTPS`);
  assert.equal(url.hostname, 'upload.wikimedia.org', `${entry.id} playback URL must use the upstream Wikimedia media host`);
  ready++;
}

assert.equal(ready, sources.environments.length);
console.log(`Validated Gaussian source-video playback from canonical manifest: playable=${ready}, requires_untrusted_urls=${ready}`);
