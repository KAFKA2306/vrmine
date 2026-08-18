import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import {
  applyRoundResult,
  buildAnswerOrder,
  chooseQuestion,
  createGame,
  normalizeAnswer,
  scoreRound,
  startRound,
  validatePlayers
} from './engine.mjs';

test('player validation accepts 4-8 unique names', () => {
  assert.equal(validatePlayers(['A', 'B', 'C']).ok, false);
  assert.equal(validatePlayers(['A', 'B', 'C', 'A']).ok, false);
  assert.equal(validatePlayers(['A', 'B', 'C', 'D']).ok, true);
});

test('answer normalization trims and limits length', () => {
  assert.equal(normalizeAnswer('  salmon   roe  '), 'salmon roe');
  assert.equal(normalizeAnswer('x'.repeat(100)).length, 80);
});

test('round role assignment is deterministic and target differs', () => {
  const game = createGame({ names: ['A', 'B', 'C', 'D'], seed: 123, totalRounds: 5 });
  const first = startRound(game);
  const second = startRound(game);
  assert.equal(first.currentRound.impostorId, second.currentRound.impostorId);
  assert.notEqual(first.currentRound.impostorId, first.currentRound.targetId);
  assert.equal(first.currentRound.questionCandidates.length, 3);
});

test('question tie is resolved using provided random source', () => {
  const selected = chooseQuestion(['A', 'B', 'C'], { p1: 'A', p2: 'B' }, () => 0.99);
  assert.equal(selected, 'B');
});

test('impostor succeeds when another answer has more votes', () => {
  const players = ['p1', 'p2', 'p3', 'p4'].map((id) => ({ id, name: id, score: 0 }));
  const result = scoreRound({
    players,
    impostorId: 'p1',
    targetId: 'p2',
    answerOrder: ['p1', 'p2', 'p3', 'p4'],
    votes: { p1: 'p3', p2: 'p1', p3: 'p2', p4: 'p2' }
  });
  assert.equal(result.impostorOutcome, 'success');
  assert.equal(result.points.p1, 3);
  assert.equal(result.points.p2, 3);
});

test('impostor fails as sole vote leader', () => {
  const players = ['p1', 'p2', 'p3', 'p4'].map((id) => ({ id, name: id, score: 0 }));
  const result = scoreRound({
    players,
    impostorId: 'p1',
    targetId: 'p2',
    answerOrder: ['p1', 'p2', 'p3', 'p4'],
    votes: { p1: 'p2', p2: 'p1', p3: 'p1', p4: 'p1' }
  });
  assert.equal(result.impostorOutcome, 'failed');
  assert.equal(result.points.p1, 0);
});

test('impostor gets partial score on tied lead', () => {
  const players = ['p1', 'p2', 'p3', 'p4'].map((id) => ({ id, name: id, score: 0 }));
  const result = scoreRound({
    players,
    impostorId: 'p1',
    targetId: 'p2',
    answerOrder: ['p1', 'p2', 'p3', 'p4'],
    votes: { p1: 'p2', p2: 'p1', p3: 'p1', p4: 'p2' }
  });
  assert.equal(result.impostorOutcome, 'partial');
  assert.equal(result.points.p1, 1);
});

test('applying round updates cumulative scores', () => {
  let game = createGame({ names: ['A', 'B', 'C', 'D'], seed: 9, totalRounds: 3 });
  game = startRound(game);
  const ids = game.players.map((p) => p.id);
  game.currentRound.answerOrder = buildAnswerOrder(ids, 77);
  const imp = game.currentRound.impostorId;
  for (const player of game.players) {
    game.currentRound.votes[player.id] = player.id === imp ? ids.find((id) => id !== imp) : imp;
  }
  const scored = applyRoundResult(game);
  assert.equal(scored.history.length, 1);
  assert.equal(scored.status, 'round-result');
  assert.ok(scored.players.some((p) => p.score > 0));
});

test('event PoC uses one config-selected question pack without external telemetry by default', async () => {
  const config = JSON.parse(await readFile(new URL('../../events/demo/config.json', import.meta.url), 'utf8'));
  const pack = JSON.parse(await readFile(new URL('../../events/question-packs/demo.json', import.meta.url), 'utf8'));
  assert.equal(config.event.slug, 'demo');
  assert.equal(config.game.id, 'answer-impostor');
  assert.equal(config.game.question_pack, pack.id);
  assert.equal(config.analytics.endpoint, null);
  assert.ok(pack.questions.length >= 3);
  assert.equal(new Set(pack.questions).size, pack.questions.length);
});

test('event PoC exposes the four conversion event names and bootstraps the existing game', async () => {
  const hub = await readFile(new URL('../../events/event-hub.js', import.meta.url), 'utf8');
  const gameContext = await readFile(new URL('../../events/event-game.js', import.meta.url), 'utf8');
  const gameHtml = await readFile(new URL('./index.html', import.meta.url), 'utf8');
  for (const eventName of ['view_hub', 'start_game', 'complete_game', 'cta_click']) {
    assert.ok(`${hub}\n${gameContext}`.includes(eventName));
  }
  assert.ok(gameContext.includes("await import('../games/answer-impostor/game.js')"));
  assert.ok(gameHtml.includes('../../events/event-game.js'));
});
