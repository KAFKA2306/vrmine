import assert from 'node:assert/strict';
import fs from 'node:fs';

const specPath = 'config/stich-meister-rules.json';
const runtimePath = 'Assets/KafkaMade/VRMine/Runtime/Game/GameController.cs';
const boardViewPath = 'Assets/KafkaMade/VRMine/Runtime/UI/BoardView.cs';
const pagePath = 'pages/games/stich-meister/index.html';

const spec = JSON.parse(fs.readFileSync(specPath, 'utf8'));
const runtime = fs.readFileSync(runtimePath, 'utf8');
const boardView = fs.readFileSync(boardViewPath, 'utf8');
const page = fs.readFileSync(pagePath, 'utf8');

assert.equal(spec.canonical_identity, 'Stich-Meister');
assert.equal(spec.authority_scope.intended_rules, 'unresolved unless an author-owned source is added later');
assert.equal(spec.source_audit.status, 'no-authoritative-rule-1-60-source-found');
assert.deepEqual(spec.session_observed.supported_player_counts, [3, 4, 5]);
assert.deepEqual(spec.session_observed.cards_per_player, {3: 16, 4: 15, 5: 12});
assert.deepEqual(spec.session_observed.category_ranges, {Trump: [1, 21], Basic: [22, 40], Scoring: [41, 60]});
assert.equal(spec.session_observed.rounds_per_game, 'player_count');
assert.equal(spec.rule_defaults.conflict_priority, 'global-lower-id');

const ruleIds = Object.keys(spec.rules).map(Number).sort((a, b) => a - b);
assert.equal(ruleIds.length, 60, 'Rule authority must contain exactly 60 entries');
assert.deepEqual(ruleIds, Array.from({length: 60}, (_, index) => index + 1), 'Rule IDs must be contiguous 1..60');

for (const id of ruleIds) {
  const rule = spec.rules[String(id)];
  const expectedCategory = id <= 21 ? 'Trump' : id <= 40 ? 'Basic' : 'Scoring';
  assert.equal(rule.category, expectedCategory, `Rule ${id} category mismatch`);
  assert.ok(rule.phase, `Rule ${id} phase missing`);
  assert.ok(rule.runtime, `Rule ${id} observed runtime description missing`);
  assert.ok(['implementation-observed', 'unimplemented-observed'].includes(rule.implementation_status), `Rule ${id} implementation status invalid`);
}

assert.equal(spec.rule_defaults.display_name, null, 'Unverified display names must stay unresolved');
assert.equal(spec.rule_defaults.intended_effect, null, 'Observed runtime must not become intended rule text');
assert.equal(spec.rule_defaults.intended_spec_status, 'unresolved');
assert.equal(spec.rules['26'].implementation_status, 'unimplemented-observed');

const runtimeContracts = [
  'if (board.playerCount == 3) board.selectedRules[count++] = DrawRule();',
  'if (board.playerCount == 5 && seat == (board.dealerSeat + 1) % board.playerCount) continue;',
  'if (board.selectedRules[j] < board.selectedRules[i])',
  'if (rule <= 21 && rule != 0 && board.trumpRule == 0) board.trumpRule = rule;',
  'else if (rule <= 40 && rule != 0 && board.basicRule == 0) board.basicRule = rule;',
  'else if (rule != 0 && board.scoringRule == 0) board.scoringRule = rule;',
  'if (rule >= 1 && rule <= 14) return Rank(card) == rule + 1;',
  'if (rule == 21 && board.trickIndex >= 1 && board.trickCardCount > 0)',
  'if (HasRule(22)) board.prepareStep = 1;',
  'else if (HasRule(25)) board.prepareStep = 4;',
  'byte reference = HasRule(27) ? board.trickCards[board.trickCardCount - 1] : board.trickCards[0];',
  'return HasRule(40) ? remaining == board.playerCount * 3 : remaining == 0;',
  'if (rule < 46 || rule > 60) return;',
  'else if (rule == 60)',
  'if (board.roundIndex >= board.playerCount)',
  'board.selectedRuleBySeat[0] = 14;',
  'board.selectedRuleBySeat[1] = 5;',
  'board.selectedRuleBySeat[2] = 60;',
  'board.selectedRuleBySeat[3] = 41;',
  'if (board.trumpRule != 5 || board.scoringRule != 41) failures++;'
];
for (const contract of runtimeContracts) assert.ok(runtime.includes(contract), `Runtime contract drift: ${contract}`);

assert.ok(!runtime.includes('HasRule(26)'), 'Rule 26 gained runtime behavior; update canonical authority before merge');
assert.ok(!runtime.includes('rule == 26'), 'Rule 26 gained runtime behavior; update canonical authority before merge');

assert.ok(boardView.includes('int localPlayerId = Networking.LocalPlayer == null ? 0 : Networking.LocalPlayer.playerId;'), 'Private hand view must resolve the actual local player identity');
assert.ok(boardView.includes('state.occupiedPlayerIds[seat] == localPlayerId'), 'Private hand view must derive local-seat visibility from canonical occupiedPlayerIds');
assert.ok(!boardView.includes('seat == controller.localPlayerSeat'), 'Private hand visibility must not trust the mutable/default localPlayerSeat hint');
assert.ok(boardView.includes('cv.isFaceDown = !isLocal;'), 'Opponent and unseated hand presentation must remain face-down');

const actionSurface = runtime.slice(runtime.indexOf('public void SelectRule'), runtime.indexOf('public void TryPlayCard'));
assert.ok(actionSurface.includes('Networking.LocalPlayer'), 'Player actions must resolve the actual local player identity');
assert.ok(actionSurface.includes('board.occupiedPlayerIds'), 'Player actions must derive acting seat from canonical occupiedPlayerIds');
assert.ok(!actionSurface.includes('localPlayerSeat'), 'Player actions must not trust mutable/default localPlayerSeat as authority');

const sessionSurface = runtime.slice(runtime.indexOf('public void JoinGame'), runtime.indexOf('public void Render'));
assert.ok(sessionSurface.includes('public void LeaveGame()'), 'Session path must expose an explicit leave action');
assert.ok(sessionSurface.includes('ResolveLocalSeat()'), 'Leave must resolve the actual occupied seat instead of trusting localPlayerSeat');
assert.ok(sessionSurface.includes('board.occupiedPlayerIds[seat] = 0;'), 'Leave must release the canonical occupied seat');
assert.ok(sessionSurface.includes('AssignSeat(playerId, seat)'), 'Rejoin must reuse the canonical seat assignment path');
assert.ok(runtime.includes('if (!ReleaseSeat(playerId)) failures++;'), '3P/4P/5P fixture must exercise seat release');
assert.ok(runtime.includes('if (!AssignSeat(playerId, 0)) failures++;'), '3P/4P/5P fixture must exercise rejoin through canonical assignment');

assert.ok(page.includes('data-stich-rule-authority'), 'Public Stich-Meister page must expose rule authority status');
assert.ok(page.includes('60枚の意図仕様は未解決'), 'Public page must not imply resolved Rule 1–60 semantics');
assert.ok(page.includes('https://github.com/KAFKA2306/vrmine/blob/main/config/stich-meister-rules.json'), 'Public page must link to the canonical rule authority');

console.log('Stich-Meister rule authority: PASS (60 rules; lower-id conflict priority guarded; private hand, action seat, and leave/rejoin session authority guarded; intended semantics unresolved)');
