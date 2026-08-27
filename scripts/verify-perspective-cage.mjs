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

function readRequired(relativePath) {
  const fullPath = path.join(root, relativePath);
  if (!fs.existsSync(fullPath)) fail(`required file is missing: ${relativePath}`);
  return fs.readFileSync(fullPath, 'utf8');
}

function requireTokens(source, relativePath, tokens) {
  for (const token of tokens) if (!source.includes(token)) fail(`${relativePath} is missing contract token: ${token}`);
}

function nonEmpty(value) {
  return typeof value === 'string' && value.trim().length > 0;
}

let spec;
try {
  spec = JSON.parse(fs.readFileSync(specPath, 'utf8'));
} catch (error) {
  fail(`cannot read valid JSON at ${specPath}: ${error.message}`);
}

if (spec.schema_version !== 1) fail('schema_version must be 1');
if (spec.world?.id !== 'perspective-cage') fail('world.id must be perspective-cage');
if (spec.world?.primary_target !== 'vrchat_pc') fail('primary_target must be vrchat_pc');
if (spec.world?.canonical_scene !== 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Scenes/PerspectiveCage.unity') fail('canonical_scene changed unexpectedly');
if (spec.world?.players?.min !== 1 || spec.world?.players?.max !== 4) fail('players must be exactly 1..4');
if (spec.world?.target_playtime_minutes?.min !== 15 || spec.world?.target_playtime_minutes?.max !== 30) fail('target playtime must be exactly 15..30 minutes');
for (const field of ['title_ja', 'title_en', 'summary_ja', 'summary_en']) if (!nonEmpty(spec.world?.[field])) fail(`world.${field} is required`);
for (const field of ['description_ja', 'description_en', 'quick_start_ja', 'quick_start_en']) if (!nonEmpty(spec.intro_rule?.[field])) fail(`intro_rule.${field} is required`);

const puzzles = spec.puzzles;
if (!Array.isArray(puzzles) || puzzles.length !== 5) fail('puzzles must contain exactly 5 entries');
const expectedIds = ['p01', 'p02', 'p03', 'p04', 'p05'];
if (JSON.stringify(puzzles.map((p) => p.id)) !== JSON.stringify(expectedIds)) fail(`puzzle ids/order must be ${expectedIds.join(', ')}`);
if (JSON.stringify(puzzles.map((p) => p.room)) !== JSON.stringify([1, 2, 3, 4, 5])) fail('rooms must be 1..5 in order');

const requiredStrings = [
  'title_ja', 'title_en', 'goal', 'goal_en', 'interaction_type', 'wrong_input_behavior',
  'wrong_feedback_ja', 'wrong_feedback_en', 'completion_effect', 'success_feedback_ja', 'success_feedback_en',
  'reset_state', 'multiplayer_scope', 'accessibility_fallback',
];
for (const puzzle of puzzles) {
  for (const field of requiredStrings) if (!nonEmpty(puzzle[field])) fail(`${puzzle.id}.${field} is required`);
  if (!['choice', 'sequence', 'mapping'].includes(puzzle.interaction_type)) fail(`${puzzle.id}.interaction_type is unsupported`);
  if (puzzle.multiplayer_scope !== 'public_instance') fail(`${puzzle.id}.multiplayer_scope must be public_instance`);
  if (!Array.isArray(puzzle.observable_clues) || puzzle.observable_clues.length < 2 || puzzle.observable_clues.some((v) => !nonEmpty(v))) fail(`${puzzle.id}.observable_clues is invalid`);
  if (!Array.isArray(puzzle.hints) || puzzle.hints.length !== 3 || puzzle.hints.some((v) => !nonEmpty(v))) fail(`${puzzle.id}.hints must contain exactly 3 non-empty levels`);
  if (!Array.isArray(puzzle.hints_en) || puzzle.hints_en.length !== 3 || puzzle.hints_en.some((v) => !nonEmpty(v))) fail(`${puzzle.id}.hints_en must contain exactly 3 non-empty levels`);
  if (!puzzle.solution || !nonEmpty(puzzle.solution.output)) fail(`${puzzle.id}.solution is incomplete`);
}

const p02Sequence = puzzles[1].solution.sequence;
if (!Array.isArray(p02Sequence) || p02Sequence.length !== 4 || new Set(p02Sequence).size !== 4) fail('p02.solution.sequence must contain four unique objects');
const mapping = puzzles[2].solution.mapping;
const p03Sockets = mapping && [mapping.marker_triangle, mapping.marker_circle, mapping.marker_square, mapping.marker_diamond];
if (!p03Sockets || p03Sockets.some((v) => !nonEmpty(v)) || new Set(p03Sockets).size !== 4) fail('p03.solution.mapping must map four markers bijectively');
const allowedSockets = new Set(['socket_west', 'socket_north', 'socket_east', 'socket_south']);
if (p03Sockets.some((v) => !allowedSockets.has(v))) fail('p03.solution.mapping contains an unknown socket');

const resultOrder = spec.intro_rule?.result_order;
if (!Array.isArray(resultOrder) || resultOrder.length !== 4 || new Set(resultOrder).size !== 4) fail('intro_rule.result_order must contain four unique ids');
for (const id of resultOrder) if (!['p01', 'p02', 'p03', 'p04'].includes(id)) fail(`intro_rule.result_order contains invalid id ${id}`);
if (JSON.stringify(puzzles[4].solution.references) !== JSON.stringify(resultOrder)) fail('p05.solution.references must exactly match intro_rule.result_order');
const outputById = Object.fromEntries(puzzles.slice(0, 4).map((p) => [p.id, p.solution.output]));
const expectedFinalSequence = resultOrder.map((id) => outputById[id]);
if (JSON.stringify(puzzles[4].solution.sequence) !== JSON.stringify(expectedFinalSequence)) fail(`p05.solution.sequence must be derived from prior outputs: ${expectedFinalSequence.join(' -> ')}`);
if (puzzles[4].solution.output !== 'clear') fail('p05.solution.output must be clear');

const vpm = JSON.parse(readRequired('Packages/vpm-manifest.json'));
if (vpm.dependencies?.['com.vrchat.worlds']?.version !== '3.9.0') fail('parameterized owner events require repository-pinned VRChat Worlds SDK 3.9.0');

const files = {
  controller: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Runtime/PerspectiveCageController.cs',
  controllerMeta: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Runtime/PerspectiveCageController.cs.meta',
  interactable: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Runtime/PerspectiveCageInteractable.cs',
  interactableMeta: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Runtime/PerspectiveCageInteractable.cs.meta',
  builder: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Editor/PerspectiveCageBuilder.cs',
  builderMeta: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Editor/PerspectiveCageBuilder.cs.meta',
  experience: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Editor/PerspectiveCageExperienceBuilder.cs',
  experienceMeta: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Editor/PerspectiveCageExperienceBuilder.cs.meta',
  verification: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Editor/PerspectiveCageVerification.cs',
  verificationMeta: 'Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Editor/PerspectiveCageVerification.cs.meta',
  runner: 'scripts/run-perspective-cage-unity.mjs',
};
const source = Object.fromEntries(Object.entries(files).map(([key, file]) => [key, readRequired(file)]));

requireTokens(source.controller, files.controller, [
  '[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]',
  'using VRC.SDK3.UdonNetworkCalling;',
  'public int p01Solution;',
  'public int[] p02Solution = new int[4];',
  'public int[] p03SocketByMarker = new int[4];',
  'public int p04Solution;',
  'public int[] p05Solution = new int[4];',
  '[UdonSynced] public int completionMask',
  '[UdonSynced] public int resetGeneration',
  'public override void OnDeserialization()',
  'public override void OnOwnershipTransferred(VRCPlayerApi player)',
  'SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(ApplyInteraction), puzzleIndex, action, value, marker)',
  '[NetworkCallable(maxEventsPerSecond: 20)]',
  'if (!Networking.IsOwner(gameObject)) return;',
  'if (action != ActionSocket) return;',
  'else if (action != ActionInput) return;',
  'value != p02Solution[p02Step]',
  'value != p03SocketByMarker[marker]',
  'value != p05Solution[p05Step]',
  'int socket = p03SocketByMarker[marker];',
  'RequestSerialization()',
  'SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ShowWrongNetwork), puzzleIndex)',
]);
if (source.controller.includes('Networking.SetOwner')) fail('per-interaction SetOwner is forbidden; current owner is the single mutation authority');
if (/ExpectedP0[235]/.test(source.controller)) fail('runtime must not duplicate canonical solution sequences in code');

requireTokens(source.interactable, files.interactable, [
  '[UdonBehaviourSyncMode(BehaviourSyncMode.None)]',
  'controller.SubmitInteraction(puzzleIndex, action, value)',
]);

requireTokens(source.builder, files.builder, [
  'public const string ScenePath = "Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Scenes/PerspectiveCage.unity"',
  'JsonUtility.FromJson<WorldSpec>',
  'ConfigureSolutions(controller, spec);',
  'controller.p01Solution = SymbolIndex(spec.puzzles[0].solution.choice);',
  'controller.p02Solution = MapSequence(spec.puzzles[1].solution.sequence, ObjectIndex);',
  'controller.p03SocketByMarker = new[]',
  'controller.p04Solution = SymbolIndex(spec.puzzles[3].solution.choice);',
  'controller.p05Solution = MapSequence(spec.puzzles[4].solution.sequence, SymbolIndex);',
  'MarkerForSocket(controller.p03SocketByMarker, socket)',
  'MATCH SHAPE + NOTCH DIRECTION',
  'spec.solution.output.ToUpperInvariant()',
  'UdonSharpProgramAsset.CompileAllCsPrograms(true)',
  'EditorSceneManager.NewScene',
  'EditorSceneManager.SaveScene(scene, ScenePath)',
  'RegisterBuildScene()',
  'new GameObject("VRCSceneDescriptor")',
  'Anchor("ReferenceCamera",',
]);

requireTokens(source.experience, files.experience, [
  'public static class PerspectiveCageExperienceBuilder',
  'JsonUtility.FromJson<ExperienceSpec>',
  'PerspectiveCageExperience',
  'QuickStart',
  'P01ViewpointGuide',
  'hints_en',
  'wrong_feedback_en',
  'success_feedback_en',
  'EditorSceneManager.SaveScene(scene, PerspectiveCageBuilder.ScenePath)',
]);

requireTokens(source.verification, files.verification, [
  'PerspectiveCageBuilder.Build();',
  'PerspectiveCageExperienceBuilder.Apply();',
  'InteractionCount',
  'DeterministicPuzzleLogic',
  'UdonPrograms',
  'ExperienceRoot',
  'QuickStart',
  'BilingualHints',
  'P01ViewpointGuide',
  'MissingScripts',
  'BuildSettings',
  'Perspective Cage verification PASS',
  'Library/VRMine/PerspectiveCageVerification.txt',
]);
if ((source.verification.match(/PerspectiveCageBuilder\.Build\(\);/g) ?? []).length < 4) fail('verification must exercise builder rerun in both interactive and batch paths');
if ((source.verification.match(/PerspectiveCageExperienceBuilder\.Apply\(\);/g) ?? []).length < 4) fail('verification must exercise experience presentation after every canonical scene build');

requireTokens(source.runner, files.runner, [
  "projectVersion !== '2022.3.22f1'",
  "'PerspectiveCageVerification.BuildAndVerifyBatch'",
  'Perspective Cage verification PASS',
  'PerspectiveCage.unity',
  "path.join(evidenceDir, 'PerspectiveCageVerification.txt')",
]);

const metas = [source.controllerMeta, source.interactableMeta, source.builderMeta, source.experienceMeta, source.verificationMeta];
const guids = metas.map((meta, index) => {
  const match = meta.match(/^guid:\s*([0-9a-f]{32})$/m);
  if (!match) fail(`invalid Unity meta GUID at index ${index}`);
  return match[1];
});
if (new Set(guids).size !== guids.length) fail('Perspective Cage Unity source meta GUIDs must be unique');
if (/GaussianSplat|VRChatGaussianSplatting/.test(source.builder + source.experience + source.controller)) fail('Perspective Cage core must not depend on Gaussian Splatting');

console.log(JSON.stringify({
  status: 'PASS',
  world: spec.world.id,
  puzzles: puzzles.length,
  hintsJa: puzzles.reduce((sum, p) => sum + p.hints.length, 0),
  hintsEn: puzzles.reduce((sum, p) => sum + p.hints_en.length, 0),
  finalSequence: expectedFinalSequence,
  vrchatWorldsSdk: vpm.dependencies['com.vrchat.worlds'].version,
  solutionAuthority: 'config/perspective-cage.json -> builder serialized fields -> runtime',
  presentationAuthority: 'config/perspective-cage.json -> experience builder -> canonical scene',
  networkingAuthority: 'single-current-owner',
  implementationFiles: Object.keys(files).length,
  unityRuntimeEvidence: 'requires exact Unity 2022.3.22f1 execution',
}, null, 2));
