import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const specPath = path.join(root, 'config', 'perspective-cage.json');

function fail(message) {
  console.error(`PerspectiveCage verification FAIL: ${message}`);
  process.exit(1);
}

function nonEmptyString(value) {
  return typeof value === 'string' && value.trim().length > 0;
}

function readRequired(relativePath) {
  const fullPath = path.join(root, relativePath);
  if (!fs.existsSync(fullPath)) fail(`required file is missing: ${relativePath}`);
  return fs.readFileSync(fullPath, 'utf8');
}

function requireTokens(source, relativePath, tokens) {
  for (const token of tokens) if (!source.includes(token)) fail(`${relativePath} is missing contract token: ${token}`);
}

let spec;
try {
  spec = JSON.parse(fs.readFileSync(specPath, 'utf8'));
} catch (error) {
  fail(`cannot read valid JSON at ${specPath}: ${error.message}`);
}

if (spec.schema_version !== 1) fail('schema_version must be 1');
if (!spec.world || spec.world.id !== 'perspective-cage') fail('world.id must be perspective-cage');
if (spec.world.primary_target !== 'vrchat_pc') fail('primary_target must be vrchat_pc');
if (!nonEmptyString(spec.world.canonical_scene)) fail('canonical_scene is required');
if (spec.world.canonical_scene !== 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Scenes/PerspectiveCage.unity') fail('canonical_scene changed unexpectedly');
if (spec.world.players?.min !== 1 || spec.world.players?.max !== 4) fail('players must be exactly 1..4');
if (spec.world.target_playtime_minutes?.min !== 15 || spec.world.target_playtime_minutes?.max !== 30) fail('target playtime must be exactly 15..30 minutes');

const puzzles = spec.puzzles;
if (!Array.isArray(puzzles) || puzzles.length !== 5) fail('puzzles must contain exactly 5 entries');
const expectedIds = ['p01', 'p02', 'p03', 'p04', 'p05'];
const ids = puzzles.map((puzzle) => puzzle.id);
if (new Set(ids).size !== ids.length) fail('duplicate puzzle id');
if (JSON.stringify(ids) !== JSON.stringify(expectedIds)) fail(`puzzle ids/order must be ${expectedIds.join(', ')}`);
const rooms = puzzles.map((puzzle) => puzzle.room);
if (new Set(rooms).size !== rooms.length) fail('duplicate room number');
if (JSON.stringify(rooms) !== JSON.stringify([1, 2, 3, 4, 5])) fail('rooms must be 1..5 in order');

const requiredStringFields = ['title_ja', 'goal', 'interaction_type', 'wrong_input_behavior', 'completion_effect', 'reset_state', 'multiplayer_scope', 'accessibility_fallback'];
const allowedInteractionTypes = new Set(['choice', 'sequence', 'mapping']);
for (const puzzle of puzzles) {
  for (const field of requiredStringFields) if (!nonEmptyString(puzzle[field])) fail(`${puzzle.id}.${field} is required`);
  if (!allowedInteractionTypes.has(puzzle.interaction_type)) fail(`${puzzle.id}.interaction_type is unsupported: ${puzzle.interaction_type}`);
  if (puzzle.multiplayer_scope !== 'public_instance') fail(`${puzzle.id}.multiplayer_scope must be public_instance`);
  if (!Array.isArray(puzzle.observable_clues) || puzzle.observable_clues.length < 2) fail(`${puzzle.id}.observable_clues must contain at least 2 clues`);
  if (puzzle.observable_clues.some((clue) => !nonEmptyString(clue))) fail(`${puzzle.id}.observable_clues contains an empty clue`);
  if (!Array.isArray(puzzle.hints) || puzzle.hints.length !== 3) fail(`${puzzle.id}.hints must contain exactly 3 levels`);
  if (puzzle.hints.some((hint) => !nonEmptyString(hint))) fail(`${puzzle.id}.hints contains an empty hint`);
  if (!puzzle.solution || typeof puzzle.solution !== 'object') fail(`${puzzle.id}.solution is required`);
  if (!nonEmptyString(puzzle.solution.output)) fail(`${puzzle.id}.solution.output is required`);
}

const resultOrder = spec.intro_rule?.result_order;
if (!Array.isArray(resultOrder) || resultOrder.length !== 4) fail('intro_rule.result_order must contain 4 puzzle ids');
if (new Set(resultOrder).size !== 4) fail('intro_rule.result_order must not contain duplicates');
for (const id of resultOrder) if (!['p01', 'p02', 'p03', 'p04'].includes(id)) fail(`intro_rule.result_order contains invalid id ${id}`);
const finalPuzzle = puzzles[4];
if (JSON.stringify(finalPuzzle.solution.references) !== JSON.stringify(resultOrder)) fail('p05.solution.references must exactly match intro_rule.result_order');
const outputById = Object.fromEntries(puzzles.slice(0, 4).map((puzzle) => [puzzle.id, puzzle.solution.output]));
const expectedFinalSequence = resultOrder.map((id) => outputById[id]);
if (JSON.stringify(finalPuzzle.solution.sequence) !== JSON.stringify(expectedFinalSequence)) fail(`p05.solution.sequence must be derived from room outputs: ${expectedFinalSequence.join(' -> ')}`);
if (finalPuzzle.solution.output !== 'clear') fail('p05.solution.output must be clear');

const files = {
  controller: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Runtime/PerspectiveCageController.cs',
  controllerMeta: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Runtime/PerspectiveCageController.cs.meta',
  interactable: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Runtime/PerspectiveCageInteractable.cs',
  interactableMeta: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Runtime/PerspectiveCageInteractable.cs.meta',
  builder: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Editor/PerspectiveCageBuilder.cs',
  builderMeta: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Editor/PerspectiveCageBuilder.cs.meta',
  verification: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Editor/PerspectiveCageVerification.cs',
  verificationMeta: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Editor/PerspectiveCageVerification.cs.meta',
  runner: 'scripts/run-perspective-cage-unity.mjs',
};
const source = Object.fromEntries(Object.entries(files).map(([key, relative]) => [key, readRequired(relative)]));

requireTokens(source.controller, files.controller, [
  '[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]',
  '[UdonSynced] public int completionMask',
  '[UdonSynced] public int p02Step',
  '[UdonSynced] public int p03PlacedMask',
  '[UdonSynced] public int p05Step',
  '[UdonSynced] public int hintPacked',
  '[UdonSynced] public int resetGeneration',
  '[UdonSynced] public bool cleared',
  'public override void OnDeserialization()',
  'Networking.SetOwner',
  'RequestSerialization()',
  'public void ResetWorld()',
  'selectedMarker = -1',
  'SendCustomEventDelayedSeconds',
  'ExpectedP02',
  'ExpectedP05',
  'VerifyDeterministicLogic',
]);
requireTokens(source.interactable, files.interactable, [
  '[UdonBehaviourSyncMode(BehaviourSyncMode.None)]',
  'public override void Interact()',
  'controller.HandleInteraction(puzzleIndex, action, value)',
]);
requireTokens(source.builder, files.builder, [
  'public const string ScenePath = "Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Scenes/PerspectiveCage.unity"',
  'LoadSpec()',
  'UdonSharpProgramAsset.CompileAllCsPrograms(true)',
  'EditorSceneManager.NewScene',
  'BuildP01(',
  'BuildP02(',
  'BuildP03(',
  'BuildP04(',
  'BuildP05(',
  'BuildClearArea(',
  'EditorSceneManager.SaveScene(scene, ScenePath)',
  'RegisterBuildScene()',
  'new GameObject("VRCSceneDescriptor")',
  'new GameObject("ReferenceCamera")',
]);
requireTokens(source.verification, files.verification, [
  'PerspectiveCageBuilder.Build();',
  'InteractionCount',
  'UdonPrograms',
  'MissingScripts',
  'BuildSettings',
  'Perspective Cage verification PASS',
]);
if ((source.verification.match(/PerspectiveCageBuilder\.Build\(\);/g) ?? []).length < 4) fail('verification must exercise builder rerun in both interactive and batch paths');
requireTokens(source.runner, files.runner, [
  "projectVersion !== '2022.3.22f1'",
  "'PerspectiveCageVerification.BuildAndVerifyBatch'",
  "Perspective Cage verification PASS",
  'PerspectiveCage.unity',
]);

const metaSources = [source.controllerMeta, source.interactableMeta, source.builderMeta, source.verificationMeta];
const guids = metaSources.map((meta, index) => {
  const match = meta.match(/^guid:\s*([0-9a-f]{32})$/m);
  if (!match) fail(`invalid Unity meta GUID: ${Object.values(files)[index * 2 + 1]}`);
  return match[1];
});
if (new Set(guids).size !== guids.length) fail('Perspective Cage Unity meta GUIDs must be unique');
if (/GaussianSplat|VRChatGaussianSplatting/.test(source.builder + source.controller)) fail('Perspective Cage core must not depend on Gaussian Splatting');

console.log(JSON.stringify({
  status: 'PASS',
  world: spec.world.id,
  puzzles: puzzles.length,
  hints: puzzles.reduce((sum, puzzle) => sum + puzzle.hints.length, 0),
  finalSequence: expectedFinalSequence,
  primaryTarget: spec.world.primary_target,
  implementationFiles: Object.keys(files).length,
  unityRuntimeEvidence: 'requires exact Unity 2022.3.22f1 execution',
}, null, 2));
