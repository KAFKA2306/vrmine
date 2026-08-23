import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const config = JSON.parse(await readFile(new URL('../config/gaussian-exhibition.json', import.meta.url), 'utf8'));
const pipelineSource = await readFile(new URL('../Assets/KafkaMade/VRMine/Editor/GaussianExhibitionPipeline.cs', import.meta.url), 'utf8');
const presentationSource = await readFile(new URL('../Assets/KafkaMade/VRMine/Editor/GaussianExhibitionPresentation.cs', import.meta.url), 'utf8');

assert.equal(config.target_extent_m, 1, 'reusable imported prefabs must remain normalized to approximately 1 m');
assert.equal(config.presentation_scale_multiplier, 2, 'canonical exhibition presentation multiplier must be exactly 2x');
assert.equal(config.target_extent_m * config.presentation_scale_multiplier, 2, 'canonical presented extent must target approximately 2 m');

const buildCall = pipelineSource.indexOf('GaussianExhibitionBuilder.Build();');
const presentationCalls = [...pipelineSource.matchAll(/GaussianExhibitionPresentation\.Apply\(\);/g)].map((match) => match.index);
assert.ok(buildCall >= 0, 'canonical pipeline must build the scene from the registry');
assert.equal(presentationCalls.length, 1, 'the single canonical build path must apply presentation scaling exactly once');
assert.ok(presentationCalls[0] > buildCall, 'presentation scaling must run after registry-driven scene generation');
assert.doesNotMatch(pipelineSource, /BuildLocalPreview|BuildFinal/, 'count-specific preview/final builder paths must not return');

assert.match(presentationSource, /exhibit\.localScale\s*=\s*exhibit\.localScale\s*\*\s*config\.presentation_scale_multiplier/, 'presentation must scale each exhibit from its prefab-derived scene transform');
assert.match(presentationSource, /exhibit\.position\s*\+=\s*Vector3\.up\s*\*\s*-bounds\.min\.y/, 'scaled exhibits must be realigned to the floor from measured world bounds');
assert.match(presentationSource, /Mathf\.Max\(config\.layout\.pad_size_m, presentedExtent\)/, 'pad footprint must expand to at least the presented extent');
assert.match(presentationSource, /Mathf\.Max\(0f, \(presentedExtent - config\.layout\.pad_size_m\) \* 0\.5f\)/, 'row offset must preserve the configured clear aisle after scaling');
assert.match(presentationSource, /bounds\.max\.y \+ 0\.2f/, 'labels must derive vertical placement from scaled world bounds');
assert.match(presentationSource, /Mathf\.Abs\(extent - presentedExtent\) > 0\.01f/, 'presentation must fail when measured extent drifts from the configured target');
assert.match(presentationSource, /Mathf\.Abs\(bounds\.min\.y\) > 0\.01f/, 'presentation must fail when floor alignment drifts');

console.log(`Validated Gaussian presentation contract: imported_extent=${config.target_extent_m}m multiplier=${config.presentation_scale_multiplier} presented_extent=${config.target_extent_m * config.presentation_scale_multiplier}m`);
