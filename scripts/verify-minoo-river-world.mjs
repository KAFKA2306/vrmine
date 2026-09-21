import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const config = JSON.parse(await readFile('config/gaussian-worlds/minoo-river-osaka.json', 'utf8'));
const registry = JSON.parse(await readFile('config/gaussian-worlds/minoo-river-osaka-sources.json', 'utf8'));
const taskfile = await readFile('Taskfile.yml', 'utf8');
const builder = await readFile('Assets/KafkaMade/VRMine/Editor/MinooRiverWorldBuilder.cs', 'utf8');
const pipeline = await readFile('Assets/KafkaMade/VRMine/Editor/MinooRiverWorldPipeline.cs', 'utf8');
const importer = await readFile('Assets/KafkaMade/VRMine/Editor/GaussianSplatBatchImporter.cs', 'utf8');
const verification = await readFile('Assets/KafkaMade/VRMine/Editor/MinooRiverWorldVerification.cs', 'utf8');
const materializer = await readFile('scripts/stage-minoo-source.mjs', 'utf8');

assert.equal(config.schema_version, 1);
assert.equal(config.world_id, 'minoo-river-osaka');
assert.equal(config.world_mode, 'single-world');
assert.equal(config.scene_path, 'Assets/KafkaMade/VRMine/Scenes/MinooRiverOsaka.unity');
assert.equal(config.canonical_platform, 'windows');
assert.equal(config.renderer, registry.renderers.unity_vrchat);
assert.equal(config.presentation_scale_multiplier, 5);
assert.equal(config.show_title_sign, false);
assert.equal(registry.environments.length, 1);

const source = registry.environments[0];
assert.equal(source.id, 'minoo-river-osaka');
assert.equal(source.display_index, 1);
assert.equal(source.source.size_bytes, 249424370);
assert.match(source.source.sha256, /^[0-9a-f]{64}$/);
assert.equal(source.source.artifact_id, 'autophotogrammetry/minoo-river-osaka/splat');
assert.equal(source.source.artifact_manifest, 'config/gaussian-worlds/minoo-river-osaka-artifacts.yaml');
assert.equal(source.orientation_evidence.scope, 'coordinate_basis_only');
assert.equal(source.orientation_evidence.physical_up_status, 'review_required');
assert.equal(config.import_overrides.length, 1);
assert.equal(config.import_overrides[0].id, source.id);
assert.equal(config.import_overrides[0].crop.enabled, true);

assert.match(importer, /ImportConfigured/);
assert.match(builder, /VRCSceneDescriptor/);
assert.match(builder, /EnsureSceneRendererExists/);
assert.match(pipeline, /MinooRiverWorldVerification\.Verify/);
assert.match(verification, /minoo-river-u2-evidence\.json/);
assert.match(materializer, /VRMINE_MINOO_PLY/);
assert.match(taskfile, /minoo:release-candidate:/);

console.log('Minoo River standalone world contract PASS: config, provenance, local staging and Unity verification path are present.');
