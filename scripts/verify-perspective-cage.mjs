import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const specPath = path.join(root, 'config', 'perspective-cage.json');

function fail(message) {
  console.error(`PerspectiveCage spec FAIL: ${message}`);
  process.exit(1);
}

function nonEmptyString(value) {
  return typeof value === 'string' && value.trim().length > 0;
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
if (spec.world.players?.min !== 1 || spec.world.players?.max !== 4) fail('players must be exactly 1..4');
if (spec.world.target_playtime_minutes?.min !== 15 || spec.world.target_playtime_minutes?.max !== 30) {
  fail('target playtime must be exactly 15..30 minutes');
}

const puzzles = spec.puzzles;
if (!Array.isArray(puzzles) || puzzles.length !== 5) fail('puzzles must contain exactly 5 entries');

const expectedIds = ['p01', 'p02', 'p03', 'p04', 'p05'];
const ids = puzzles.map((puzzle) => puzzle.id);
if (new Set(ids).size !== ids.length) fail('duplicate puzzle id');
if (JSON.stringify(ids) !== JSON.stringify(expectedIds)) {
  fail(`puzzle ids/order must be ${expectedIds.join(', ')}`);
}

const rooms = puzzles.map((puzzle) => puzzle.room);
if (new Set(rooms).size !== rooms.length) fail('duplicate room number');
if (JSON.stringify(rooms) !== JSON.stringify([1, 2, 3, 4, 5])) fail('rooms must be 1..5 in order');

const requiredStringFields = [
  'title_ja',
  'goal',
  'interaction_type',
  'wrong_input_behavior',
  'completion_effect',
  'reset_state',
  'multiplayer_scope',
  'accessibility_fallback'
];
const allowedInteractionTypes = new Set(['choice', 'sequence', 'mapping']);

for (const puzzle of puzzles) {
  for (const field of requiredStringFields) {
    if (!nonEmptyString(puzzle[field])) fail(`${puzzle.id}.${field} is required`);
  }
  if (!allowedInteractionTypes.has(puzzle.interaction_type)) {
    fail(`${puzzle.id}.interaction_type is unsupported: ${puzzle.interaction_type}`);
  }
  if (puzzle.multiplayer_scope !== 'public_instance') {
    fail(`${puzzle.id}.multiplayer_scope must be public_instance`);
  }
  if (!Array.isArray(puzzle.observable_clues) || puzzle.observable_clues.length < 2) {
    fail(`${puzzle.id}.observable_clues must contain at least 2 clues`);
  }
  if (puzzle.observable_clues.some((clue) => !nonEmptyString(clue))) {
    fail(`${puzzle.id}.observable_clues contains an empty clue`);
  }
  if (!Array.isArray(puzzle.hints) || puzzle.hints.length !== 3) {
    fail(`${puzzle.id}.hints must contain exactly 3 levels`);
  }
  if (puzzle.hints.some((hint) => !nonEmptyString(hint))) {
    fail(`${puzzle.id}.hints contains an empty hint`);
  }
  if (!puzzle.solution || typeof puzzle.solution !== 'object') fail(`${puzzle.id}.solution is required`);
  if (!nonEmptyString(puzzle.solution.output)) fail(`${puzzle.id}.solution.output is required`);
}

const resultOrder = spec.intro_rule?.result_order;
if (!Array.isArray(resultOrder) || resultOrder.length !== 4) fail('intro_rule.result_order must contain 4 puzzle ids');
if (new Set(resultOrder).size !== 4) fail('intro_rule.result_order must not contain duplicates');
for (const id of resultOrder) {
  if (!['p01', 'p02', 'p03', 'p04'].includes(id)) fail(`intro_rule.result_order contains invalid id ${id}`);
}

const finalPuzzle = puzzles[4];
const finalReferences = finalPuzzle.solution.references;
if (JSON.stringify(finalReferences) !== JSON.stringify(resultOrder)) {
  fail('p05.solution.references must exactly match intro_rule.result_order');
}

const outputById = Object.fromEntries(puzzles.slice(0, 4).map((puzzle) => [puzzle.id, puzzle.solution.output]));
const expectedFinalSequence = resultOrder.map((id) => outputById[id]);
if (JSON.stringify(finalPuzzle.solution.sequence) !== JSON.stringify(expectedFinalSequence)) {
  fail(`p05.solution.sequence must be derived from referenced room outputs: ${expectedFinalSequence.join(' -> ')}`);
}
if (finalPuzzle.solution.output !== 'clear') fail('p05.solution.output must be clear');

console.log(JSON.stringify({
  status: 'PASS',
  world: spec.world.id,
  puzzles: puzzles.length,
  hints: puzzles.reduce((sum, puzzle) => sum + puzzle.hints.length, 0),
  finalSequence: expectedFinalSequence,
  primaryTarget: spec.world.primary_target
}, null, 2));
