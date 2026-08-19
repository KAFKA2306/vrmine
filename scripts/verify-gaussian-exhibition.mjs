import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const exhibition = JSON.parse(await readFile(new URL('../config/gaussian-exhibition.json', import.meta.url), 'utf8'));
const sources = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));
const playlist = JSON.parse(await readFile(new URL('../config/gaussian-video-playlist.json', import.meta.url), 'utf8'));
const importerSource = await readFile(new URL('../Assets/KafkaMade/VRMine/Editor/GaussianSplatBatchImporter.cs', import.meta.url), 'utf8');
const importOverridesSource = await readFile(new URL('../Assets/KafkaMade/VRMine/Editor/GaussianImportOverrides.cs', import.meta.url), 'utf8');
const pipelineSource = await readFile(new URL('../Assets/KafkaMade/VRMine/Editor/GaussianExhibitionPipeline.cs', import.meta.url), 'utf8');
const materializerSource = await readFile(new URL('./materialize-gaussian-sources.mjs', import.meta.url), 'utf8');

assert.equal(exhibition.schema_version, 3, 'unsupported gaussian exhibition schema');
assert.equal(exhibition.final_expected_exhibits, 20, 'the #72 final product still requires exactly 20 exhibits');
assert.equal(exhibition.canonical_platform, 'windows');
assert.equal(exhibition.source_registry, 'config/gaussian-splats.json');
assert.equal(exhibition.renderer, sources.renderers.unity_vrchat, 'scene renderer must match the canonical source registry');
assert.equal(exhibition.target_extent_m, 1, 'exhibits target an approximately 1 m normalized extent');
assert.equal(exhibition.scene_path, 'Assets/KafkaMade/VRMine/Scenes/GaussianSplatExhibition.unity');
assert.ok(Array.isArray(exhibition.import_overrides), 'import_overrides must be an array');
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
  assert.match(source.source?.download_url ?? '', /^https:\/\//, `${source.id}: direct download_url missing`);
}

const finiteVector = (value) => value && ['x', 'y', 'z'].every((key) => Number.isFinite(value[key]));
const overrideIds = new Set();
for (const override of exhibition.import_overrides) {
  assert.ok(typeof override?.id === 'string' && override.id.length > 0, 'import override id is missing');
  assert.ok(ids.has(override.id), `import override references unknown id ${override.id}`);
  assert.ok(!overrideIds.has(override.id), `duplicate import override id ${override.id}`);
  overrideIds.add(override.id);
  if (override.crop?.enabled) {
    assert.ok(finiteVector(override.crop.center), `${override.id}: crop center must be finite`);
    assert.ok(finiteVector(override.crop.size), `${override.id}: crop size must be finite`);
    for (const key of ['x', 'y', 'z']) assert.ok(override.crop.size[key] > 0, `${override.id}: crop size ${key} must be positive`);
    assert.ok(Number.isFinite(override.crop.padding) && override.crop.padding >= 0, `${override.id}: crop padding must be non-negative`);
  }
  if (override.alignment?.enabled) {
    assert.ok(['horizon', 'wall'].includes(override.alignment.mode), `${override.id}: alignment mode must be horizon or wall`);
    const q = override.alignment.rotation;
    assert.ok(q && ['x', 'y', 'z', 'w'].every((key) => Number.isFinite(q[key])), `${override.id}: alignment rotation must be finite`);
    assert.ok(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w > 1e-12, `${override.id}: alignment rotation must be non-zero`);
    if (override.alignment.pivot) assert.ok(finiteVector(override.alignment.pivot), `${override.id}: alignment pivot must be finite`);
  }
}

assert.equal(playlist.expected_entries, exhibition.final_expected_exhibits, 'final playlist count must follow the final product count');

assert.doesNotMatch(importerSource, /const\s+float\s+TargetExtentMeters/, 'target extent must have one authority in gaussian-exhibition.json');
assert.match(importerSource, /exhibition\.target_extent_m/, 'Unity importer must read target_extent_m from gaussian-exhibition.json');
assert.match(importerSource, /GaussianImportOverrides\.Validate\(exhibition\.import_overrides, registeredIds\)/, 'Unity importer must validate import overrides before import');
assert.match(importerSource, /GaussianImportOverrides\.Apply\(/, 'Unity importer must apply per-exhibit import overrides');
assert.match(importerSource, /existingPrefab != null && ProvenanceMatches\(provenancePath, expectedProvenance\)/, 'prefab reuse must require matching import provenance');
for (const field of ['source_repository', 'source_commit', 'source_path', 'source_size_bytes', 'source_sha256', 'renderer', 'import_method', 'chunk_size', 'import_options_json', 'import_override_json']) {
  assert.match(importerSource, new RegExp(`public\\s+[^;]+\\s+${field};`), `import provenance must include ${field}`);
}
assert.match(importerSource, /JsonUtility\.ToJson\(options\)/, 'cache provenance must cover the complete upstream ImportOptions value');
for (const field of ['cropToBounds', 'cropBounds', 'cropPadding', 'applyHorizonAlignment', 'horizonRotation', 'horizonPivot']) {
  assert.match(importOverridesSource, new RegExp(`"${field}"`), `import override implementation must use upstream ${field}`);
}
assert.doesNotMatch(importOverridesSource, /prun|k-NN|semantic/i, 'VRMine import overrides must not implement custom Gaussian pruning');

const prepareCall = pipelineSource.indexOf('PrepareLocal();');
const markerDelete = pipelineSource.indexOf('File.Delete(PrepareOnOpenMarker);');
assert.ok(prepareCall >= 0 && markerDelete >= 0, 'auto-on-open pipeline must prepare the local scene and consume its marker');
assert.ok(prepareCall < markerDelete, 'auto-on-open marker must be deleted only after successful scene preparation');
assert.match(pipelineSource, /preparation marker was preserved/i, 'failure path must state that the retry marker is preserved');

const temporaryStat = materializerSource.indexOf('await stat(temporary)');
const temporaryHash = materializerSource.indexOf('await sha256(temporary)');
const promotion = materializerSource.indexOf('await rename(temporary, destination)');
assert.ok(temporaryStat >= 0 && temporaryHash >= 0 && promotion >= 0, 'PLY materializer must verify a partial file and promote it with rename');
assert.ok(temporaryStat < promotion && temporaryHash < promotion, 'PLY size/hash verification must happen before destination promotion');
assert.ok(!materializerSource.includes('await rm(destination, { force: true });'), 'PLY materializer must not delete the destination before verified promotion');
assert.doesNotMatch(materializerSource, /spawnSync|gitShowToTemporary|git\s+\[/, 'PLY materializer must not depend on a source repository checkout');
assert.match(materializerSource, /environment\.source\.download_url/, 'PLY materializer must use each source direct download URL');

console.log(`Validated count-independent Gaussian exhibition contract: registered=${sources.environments.length}, import_overrides=${exhibition.import_overrides.length}, final_required=${exhibition.final_expected_exhibits}, renderer=1`);
