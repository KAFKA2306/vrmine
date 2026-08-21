import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const playlist = JSON.parse(await readFile(new URL('../config/gaussian-video-playlist.json', import.meta.url), 'utf8'));
const exhibition = JSON.parse(await readFile(new URL('../config/gaussian-exhibition.json', import.meta.url), 'utf8'));
const sources = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));
const expectedEntries = sources.environments.length;

const expectedPlayer = 'Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VideoPlayers/UdonSyncPlayer (Unity).prefab';
const allowedEntryStatuses = new Set(['ready_allowlisted', 'ready_untrusted', 'blocked_playback_url', 'blocked_source']);

assert.equal(playlist.schema_version, 2, 'unsupported Gaussian video playlist schema');
assert.equal(playlist.source_registry, 'config/gaussian-splats.json');
assert.equal(playlist.exhibition_manifest, 'config/gaussian-exhibition.json');
assert.equal(playlist.player_prefab_path, expectedPlayer, 'use the canonical SDK Unity sync-player prefab');
assert.equal(exhibition.video_player?.prefab_path, expectedPlayer, 'exhibition must use the same canonical SDK player prefab');
assert.equal(playlist.rate_limit_seconds, 5, 'VRChat URL loads must be throttled to the canonical 5 second interval');
assert.ok(['blocked_upstream', 'ready'].includes(playlist.status), 'invalid playlist status');
assert.ok(Array.isArray(playlist.entries), 'playlist entries must be an array');
assert.equal(playlist.entries.length, expectedEntries, 'playlist slot count must match the registered source count');

const sourceById = new Map(sources.environments.map((entry) => [entry.id, entry]));
assert.equal(sourceById.size, sources.environments.length, 'source ids must be unique');
const seenIndexes = new Set();
const seenSources = new Set();
let ready = 0;
let blockedUrl = 0;
let blockedSource = 0;

for (let i = 0; i < playlist.entries.length; i++) {
  const entry = playlist.entries[i];
  const registeredSource = i < sources.environments.length ? sources.environments[i] : null;
  assert.equal(entry.display_index, i + 1, `playlist slot ${i + 1} must keep canonical display order`);
  assert.ok(!seenIndexes.has(entry.display_index), `duplicate playlist display index ${entry.display_index}`);
  seenIndexes.add(entry.display_index);
  assert.ok(allowedEntryStatuses.has(entry.status), `invalid playlist entry status ${entry.status}`);

  if (registeredSource === null) {
    blockedSource++;
    assert.equal(entry.source_id, null, `unregistered final slot ${entry.display_index} must not invent a source id`);
    assert.equal(entry.status, 'blocked_source');
    assert.equal(entry.playback_url, null);
    assert.equal(entry.requires_untrusted_urls, null);
    continue;
  }

  assert.equal(entry.source_id, registeredSource.id, `playlist source order must follow the canonical source registry at slot ${entry.display_index}`);
  assert.ok(sourceById.has(entry.source_id), `unknown source id ${entry.source_id}`);
  assert.ok(!seenSources.has(entry.source_id), `source appears more than once in playlist: ${entry.source_id}`);
  seenSources.add(entry.source_id);

  if (entry.playback_url === null) {
    blockedUrl++;
    assert.equal(entry.status, 'blocked_playback_url');
    assert.equal(entry.requires_untrusted_urls, null, 'unknown playback URL must not guess URL trust state');
    continue;
  }

  ready++;
  const url = new URL(entry.playback_url);
  assert.equal(url.protocol, 'https:', `playback URL must use HTTPS: ${entry.source_id}`);
  assert.equal(typeof entry.requires_untrusted_urls, 'boolean', `ready URL must record trust requirement: ${entry.source_id}`);
  assert.equal(
    entry.status,
    entry.requires_untrusted_urls ? 'ready_untrusted' : 'ready_allowlisted',
    `playlist trust status mismatch: ${entry.source_id}`,
  );
  if (url.hostname === 'upload.wikimedia.org') {
    assert.equal(entry.requires_untrusted_urls, true, 'Wikimedia playback must not be mislabeled allowlisted');
  }
}

assert.equal(seenIndexes.size, expectedEntries);
assert.equal(seenSources.size, sources.environments.length, 'every currently registered 3DGS source must have one video slot');
assert.equal(blockedSource, 0, 'the scalable playlist must not contain unregistered capacity slots');

const fullyReady = ready === expectedEntries;
assert.equal(playlist.status, fullyReady ? 'ready' : 'blocked_upstream', 'top-level playlist status must fail closed');
assert.equal(blockedUrl + blockedSource + ready, expectedEntries);

console.log(`Validated Gaussian source-video playlist: entries=${expectedEntries}, registered_sources=${sources.environments.length}, playable=${ready}, blocked_url=${blockedUrl}, blocked_source=${blockedSource}`);
