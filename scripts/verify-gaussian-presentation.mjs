import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const config = JSON.parse(await readFile(new URL('../config/gaussian-exhibition.json', import.meta.url), 'utf8'));
const pipelineSource = await readFile(new URL('../Assets/KafkaMade/VRMine/Editor/GaussianExhibitionPipeline.cs', import.meta.url), 'utf8');
const presentationSource = await readFile(new URL('../Assets/KafkaMade/VRMine/Editor/GaussianExhibitionPresentation.cs', import.meta.url), 'utf8');

assert.equal(config.target_extent_m, 1, 'reusable imported prefabs must remain normalized to approximately 1 m');
assert.equal(config.presentation_scale_multiplier, 5, 'canonical exhibition presentation multiplier must be exactly 5x');
assert.equal(config.target_extent_m * config.presentation_scale_multiplier, 5, 'canonical presented extent must target approximately 5 m');

const localBuild = pipelineSource.indexOf('GaussianExhibitionBuilder.BuildLocalPreview();');
const finalBuild = pipelineSource.indexOf('GaussianExhibitionBuilder.BuildFinal();');
const presentationCalls = [...pipelineSource.matchAll(/GaussianExhibitionPresentation\.Apply\(\);/g)].map((match) => match.index);
assert.equal(presentationCalls.length, 2, 'both local and final build paths must apply presentation scaling exactly once');
assert.ok(localBuild >= 0 && presentationCalls[0] > localBuild, 'local presentation scaling must run after scene generation');
assert.ok(finalBuild >= 0 && presentationCalls[1] > finalBuild, 'final presentation scaling must run after scene generation');

assert.match(presentationSource, /exhibit\.localScale\s*=\s*exhibit\.localScale\s*\*\s*config\.presentation_scale_multiplier/, 'presentation must scale each exhibit from its prefab-derived scene transform');
assert.match(presentationSource, /PrefabUtility\.RecordPrefabInstancePropertyModifications\(exhibit\)/, 'presentation must persist prefab instance transform changes');
assert.match(presentationSource, /EditorSceneManager\.MarkSceneDirty\(root\.scene\)/, 'presentation must mark the generated scene dirty before saving');
assert.match(presentationSource, /exhibit\.position\s*\+=\s*Vector3\.up\s*\*\s*-bounds\.min\.y/, 'scaled exhibits must be realigned to the floor from measured world bounds');
assert.match(presentationSource, /Mathf\.Max\(config\.layout\.pad_size_m, presentedExtent\)/, 'pad footprint must expand to at least the presented extent');
assert.match(presentationSource, /Mathf\.Max\(0f, \(presentedExtent - config\.layout\.pad_size_m\) \* 0\.5f\)/, 'row offset must preserve the configured clear aisle after scaling');
assert.match(presentationSource, /bounds\.max\.y \+ 0\.2f/, 'labels must derive vertical placement from scaled world bounds');
assert.match(presentationSource, /Mathf\.Abs\(extent - presentedExtent\) > 0\.01f/, 'presentation must fail when measured extent drifts from the configured target');
assert.match(presentationSource, /Mathf\.Abs\(bounds\.min\.y\) > 0\.01f/, 'presentation must fail when floor alignment drifts');

console.log(`Validated Gaussian presentation contract: imported_extent=${config.target_extent_m}m multiplier=${config.presentation_scale_multiplier} presented_extent=${config.target_extent_m * config.presentation_scale_multiplier}m`);
