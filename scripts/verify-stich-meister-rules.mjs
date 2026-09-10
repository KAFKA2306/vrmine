import assert from 'node:assert/strict';
import fs from 'node:fs';

const specPath = 'config/stich-meister-rules.json';
const runtimePath = 'Assets/KafkaMade/VRMine/Runtime/Game/GameController.cs';
const boardViewPath = 'Assets/KafkaMade/VRMine/Runtime/UI/BoardView.cs';
const showcaseViewPath = 'Assets/KafkaMade/VRMine/Runtime/UI/BoardGameShowcaseView.cs';
const cardViewPath = 'Assets/KafkaMade/VRMine/Runtime/UI/CardView.cs';
const actionPath = 'Assets/KafkaMade/VRMine/Runtime/UI/BoardGameAction.cs';
const pagePath = 'pages/games/stich-meister/index.html';

const spec = JSON.parse(fs.readFileSync(specPath, 'utf8'));
const runtime = fs.readFileSync(runtimePath, 'utf8');
const boardView = fs.readFileSync(boardViewPath, 'utf8');
const showcaseView = fs.readFileSync(showcaseViewPath, 'utf8');
const cardView = fs.readFileSync(cardViewPath, 'utf8');
const action = fs.readFileSync(actionPath, 'utf8');
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
  'if (board.trumpRule != 5 || board.scoringRule != 41) failures++'
];
for (const contract of runtimeContracts) assert.ok(runtime.includes(contract), `Runtime contract drift: ${contract}`);

assert.ok(!runtime.includes('HasRule(26)'), 'Rule 26 gained runtime behavior; update canonical authority before merge');
assert.ok(!runtime.includes('rule == 26'), 'Rule 26 gained runtime behavior; update canonical authority before merge');

assert.ok(boardView.includes('int localPlayerId = Networking.LocalPlayer == null ? 0 : Networking.LocalPlayer.playerId;'), 'Private hand view must resolve the actual local player identity');
assert.ok(boardView.includes('state.occupiedPlayerIds[seat] == localPlayerId'), 'Private hand view must derive local-seat visibility from canonical occupiedPlayerIds');
assert.ok(!boardView.includes('seat == controller.localPlayerSeat'), 'Private hand visibility must not trust the mutable/default localPlayerSeat hint');
assert.ok(boardView.includes('cv.isFaceDown = !isLocal;'), 'Opponent and unseated hand presentation must remain face-down');

assert.ok(showcaseView.includes('int localPlayerId = Networking.LocalPlayer == null ? 0 : Networking.LocalPlayer.playerId;'), 'Showcase view must resolve the actual local player identity');
assert.ok(showcaseView.includes('seat >= 0 && seat < state.playerCount && state.occupiedPlayerIds[seat] == localPlayerId'), 'Showcase view must validate the seat hint against canonical occupiedPlayerIds before indexing private state');
assert.ok(showcaseView.includes('if (!hasLocalSeat)'), 'Unseated showcase presentation must take an explicit safe path');
assert.ok(showcaseView.includes('trickCards[i].text = "";'), 'Unseated showcase presentation must clear private hand labels');
assert.ok(showcaseView.includes('ruleCards[i].text = "";'), 'Unseated showcase presentation must clear private rule labels');
assert.ok(!showcaseView.includes('int offset = trickGame.localPlayerSeat * NetConst.MaxHandSize;'), 'Showcase view must not index private state directly from mutable/default localPlayerSeat');

const actionSurface = runtime.slice(runtime.indexOf('public void SelectRule'), runtime.indexOf('public void TryPlayCard'));
assert.ok(actionSurface.includes('Networking.LocalPlayer'), 'Player actions must resolve the actual local player identity');
assert.ok(actionSurface.includes('board.occupiedPlayerIds'), 'Player actions must derive acting seat from canonical occupiedPlayerIds');
assert.ok(!actionSurface.includes('localPlayerSeat'), 'Player actions must not trust mutable/default localPlayerSeat as authority');

const tryPlaySurface = runtime.slice(runtime.indexOf('public void TryPlayCard'), runtime.indexOf('public void OnDeclare'));
const wrongTurnGuard = tryPlaySurface.indexOf('if (playerSeat != board.currentPlayerSeat || handIndex < 0 || handIndex >= NetConst.MaxHandSize) return;');
const cardGuard = tryPlaySurface.indexOf('if (card == 0 || !LegalCard(playerSeat, card)) return;');
const acceptedMutation = tryPlaySurface.indexOf('int slot = board.trickCardCount;');
assert.ok(wrongTurnGuard >= 0 && cardGuard > wrongTurnGuard && acceptedMutation > cardGuard, 'TryPlayCard must reject wrong-turn/index and empty/illegal cards before the accepted mutation boundary');
const rejectedActionSurface = tryPlaySurface.slice(0, acceptedMutation);
for (const mutation of [
  'board.trickCards[',
  'board.trickSeats[',
  'board.trickCardCount++',
  'board.playerHands[offset + handIndex] = 0;',
  'board.currentPlayerSeat =',
  'turnIndex++',
  'ResolveTrick();',
  'Sync();'
]) assert.ok(!rejectedActionSurface.includes(mutation), `Rejected TryPlayCard input must be a canonical state no-op before acceptance: ${mutation}`);
const legalCardSurface = runtime.slice(runtime.indexOf('bool LegalCard'), runtime.indexOf('void ResolveTrick'));
assert.ok(!/board\.[A-Za-z0-9_]+(?:\[[^\]]+\])?\s*(?:=(?!=)|\+=|-=|\+\+|--)/.test(legalCardSurface), 'LegalCard must remain a pure predicate over canonical state');
assert.ok(!legalCardSurface.includes('Sync();') && !legalCardSurface.includes('OwnState();'), 'LegalCard must not acquire ownership or synchronize state');

const verifyRulesSurface = runtime.slice(runtime.indexOf('public int VerifyRules()'), runtime.indexOf('uint PlayCardStateHash()'));
assert.ok(verifyRulesSurface.includes('uint rejectedState = PlayCardStateHash();'), 'Illegal play-card fixture must capture canonical state before rejected actions');
for (const actionCall of ['TryPlayCard(0, 0);', 'TryPlayCard(1, -1);', 'TryPlayCard(1, 2);', 'TryPlayCard(1, 0);'])
  assert.ok(verifyRulesSurface.includes(actionCall), `Illegal play-card fixture missing rejected action: ${actionCall}`);
assert.equal((verifyRulesSurface.match(/if \(PlayCardStateHash\(\) != rejectedState\) failures\+\+;/g) || []).length, 4, 'Each illegal play-card class must prove the canonical state snapshot is unchanged');
const snapshotSurface = runtime.slice(runtime.indexOf('uint PlayCardStateHash()'), runtime.indexOf('bool AllRulesSelected()'));
for (const stateSurface of ['board.playerHands', 'board.trickCards', 'board.trickSeats', 'board.selectedRules', 'board.cardOwners', 'board.cardTricks', 'board.takenTricks', 'board.scores', 'board.currentPlayerSeat', 'board.trickCardCount', 'board.trickIndex', 'board.roundIndex', 'board.prepareStep', 'board.syncState', 'turnIndex'])
  assert.ok(snapshotSurface.includes(stateSurface), `Illegal-action snapshot must cover play-card canonical state: ${stateSurface}`);

assert.ok(cardView.includes('int lastInteractFrame = -1;'), 'Card interaction must retain one local duplicate-event frame marker');
assert.ok(cardView.includes('int frame = Time.frameCount;'), 'Card interaction duplicate detection must use the exact Unity frame instead of an arbitrary time threshold');
assert.ok(cardView.includes('if (lastInteractFrame == frame) return;'), 'Same-frame duplicate card interactions must be rejected');
assert.ok(cardView.includes('lastInteractFrame = frame;'), 'Accepted card interaction must record its frame before entering canonical action handling');
assert.equal((cardView.match(/controller\.OnCardClicked\(cardIndex\);/g) || []).length, 1, 'Card interaction must have one canonical OnCardClicked path');

const sessionSurface = runtime.slice(runtime.indexOf('public void JoinGame'), runtime.indexOf('public void Render'));
assert.ok(sessionSurface.includes('public void LeaveGame()'), 'Session path must expose an explicit leave action');
assert.ok(sessionSurface.includes('ResolveLocalSeat()'), 'Leave must resolve the actual occupied seat instead of trusting localPlayerSeat');
assert.ok(sessionSurface.includes('board.occupiedPlayerIds[seat] = 0;'), 'Leave must release the canonical occupied seat');
assert.ok(sessionSurface.includes('AssignSeat(playerId, seat)'), 'Rejoin must reuse the canonical seat assignment path');
assert.ok(runtime.includes('if (!ReleaseSeat(playerId)) failures++;'), '3P/4P/5P fixture must exercise seat release');
assert.ok(runtime.includes('if (!AssignSeat(playerId, 0)) failures++;'), '3P/4P/5P fixture must exercise rejoin through canonical assignment');
assert.ok(runtime.includes('bool CanChangeSession()'), 'Session membership changes must share one canonical phase guard');
assert.equal((sessionSurface.match(/if \(!CanChangeSession\(\)\) return;/g) || []).length, 2, 'Join and leave must both reject membership mutation during an active match');
assert.ok(runtime.includes('board.phase == BoardState.PhaseSetup || board.phase == BoardState.PhaseComplete'), 'Session membership changes must be limited to setup or completed matches');

const startSurface = runtime.slice(runtime.indexOf('void Start()'), runtime.indexOf('public void SelectRule'));
assert.ok(!startSurface.includes('board.phase == BoardState.PhaseSetup) SetupGame();'), 'PhaseSetup alone must not auto-start an unoccupied session');
assert.ok(runtime.includes('bool HasCompleteSession()'), 'First-match start must have one canonical occupancy guard');
assert.ok(runtime.includes('board.occupiedPlayerIds[seat] <= 0'), 'Start occupancy must be derived from canonical occupiedPlayerIds');
assert.ok(runtime.includes('if (!HasCompleteSession()) failures++;'), '3P/4P/5P fixture must prove complete occupancy can satisfy the start guard');
assert.ok(runtime.includes('if (HasCompleteSession()) failures++;'), '3P/4P/5P fixture must prove incomplete occupancy is rejected');

const resetSurface = action.slice(action.indexOf('void HandleTrickReset()'), action.indexOf('void DisarmReset()'));
assert.ok(action.includes('else HandleTrickReset();'), 'Player-facing reset must use one explicit confirmation path');
assert.ok(resetSurface.includes('trickGame.board.phase != BoardState.PhaseComplete'), 'Reset confirmation must remain limited to completed matches');
assert.ok(resetSurface.includes('SetActionLabel("CONFIRM RESET")'), 'First reset interaction must visibly arm confirmation instead of restarting immediately');
assert.ok(resetSurface.includes('if (now - resetArmedAt < ResetConfirmMinDelay) return;'), 'Rapid duplicate input must not satisfy reset confirmation');
assert.ok(action.includes('const float ResetConfirmTimeout = 5f;'), 'Reset confirmation must expire instead of remaining armed indefinitely');
assert.equal((resetSurface.match(/trickGame\.SetupGame\(\);/g) || []).length, 1, 'Canonical second-match initialization must occur only after confirmation');
assert.ok(!action.includes('else if (trickGame.board.phase == BoardState.PhaseComplete) trickGame.SetupGame();'), 'Single-interaction reset must not return');
assert.ok(!action.includes('else trickGame.SetupGame();'), 'Reset action must not restart an active match unconditionally');

assert.ok(page.includes('data-stich-rule-authority'), 'Public Stich-Meister page must expose rule authority status');
assert.ok(page.includes('60枚の意図仕様は未解決'), 'Public page must not imply resolved Rule 1–60 semantics');
assert.ok(page.includes('https://github.com/KAFKA2306/vrmine/blob/main/config/stich-meister-rules.json'), 'Public page must link to the canonical rule authority');

console.log('Stich-Meister rule authority: PASS (60 rules; lower-id conflict priority guarded; illegal play-card rejection has source boundary and state-snapshot regression; private hand, unseated showcase, action seat, card duplicate input, leave/rejoin, active-match session lock, first-match occupancy, and confirmed second-match reset boundary guarded; intended semantics unresolved)');
