import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const playlist = JSON.parse(await readFile(new URL('../config/gaussian-video-playlist.json', import.meta.url), 'utf8'));
const exhibition = JSON.parse(await readFile(new URL('../config/gaussian-exhibition.json', import.meta.url), 'utf8'));
const sources = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));

const expectedPlayer = 'Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VideoPlayers/UdonSyncPlayer (Unity).prefab';
const allowedEntryStatuses = new Set(['ready_allowlisted', 'ready_untrusted', 'blocked_playback_url', 'blocked_source']);

assert.equal(playlist.schema_version, 1, 'unsupported Gaussian video playlist schema');
assert.equal(playlist.expected_entries, 20, 'source-video playlist must have exactly 20 slots');
assert.equal(playlist.source_registry, 'config/gaussian-splats.json');
assert.equal(playlist.exhibition_manifest, 'config/gaussian-exhibition.json');
assert.equal(playlist.player_prefab_path, expectedPlayer, 'use the canonical SDK Unity sync-player prefab');
assert.equal(playlist.rate_limit_seconds, 5, 'VRChat URL loads must be throttled to at least the canonical 5 second interval');
assert.ok(['blocked_upstream', 'ready'].includes(playlist.status), 'invalid playlist status');
assert.equal(playlist.entries.length, playlist.expected_entries, 'playlist slot count mismatch');
assert.equal(exhibition.exhibits.length, playlist.expected_entries, 'playlist and exhibition must have the same slot count');

const sourceById = new Map(sources.environments.map((entry) => [entry.id, entry]));
const seenIndexes = new Set();
const seenSources = new Set();
let ready = 0;
let blockedUrl = 0;
let blockedSource = 0;

for (let i = 0; i < playlist.entries.length; i++) {
  const entry = playlist.entries[i];
  const exhibit = exhibition.exhibits[i];
  assert.equal(entry.display_index, i + 1, `playlist slot ${i + 1} must keep canonical display order`);
  assert.equal(exhibit.display_index, entry.display_index, `playlist/exhibition display index mismatch at ${i + 1}`);
  assert.ok(!seenIndexes.has(entry.display_index), `duplicate playlist display index ${entry.display_index}`);
  seenIndexes.add(entry.display_index);
  assert.ok(allowedEntryStatuses.has(entry.status), `invalid playlist entry status ${entry.status}`);

  if (entry.source_id === null) {
    blockedSource++;
    assert.equal(entry.status, 'blocked_source');
    assert.equal(entry.playback_url, null);
    assert.equal(entry.requires_untrusted_urls, null);
    assert.equal(exhibit.source_id, null, `blocked playlist slot ${entry.display_index} must match blocked exhibition slot`);
    continue;
  }

  assert.equal(entry.source_id, exhibit.source_id, `playlist source does not match exhibition slot ${entry.display_index}`);
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

assert.equal(seenIndexes.size, playlist.expected_entries);
assert.equal(seenSources.size, sources.environments.length, 'every currently registered 3DGS source must have one video slot');
assert.equal(blockedSource, playlist.expected_entries - sources.environments.length, 'missing source slots must remain explicit');

const fullyReady = ready === playlist.expected_entries;
assert.equal(playlist.status, fullyReady ? 'ready' : 'blocked_upstream', 'top-level playlist status must fail closed');
assert.equal(blockedUrl + blockedSource + ready, playlist.expected_entries);

console.log(`Validated Gaussian source-video playlist: slots=${playlist.expected_entries}, playable=${ready}, blocked_url=${blockedUrl}, blocked_source=${blockedSource}`);
