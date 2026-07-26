import test from 'node:test';
import assert from 'node:assert/strict';
import { applyRoundResult, buildAnswerOrder, chooseQuestion, createGame, normalizeAnswer, scoreRound, startRound, validatePlayers } from './engine.mjs';

test('player validation accepts 4-8 unique names', () => {
  assert.equal(validatePlayers(['A', 'B', 'C']).ok, false);
  assert.equal(validatePlayers(['A', 'B', 'C', 'A']).ok, false);
  assert.equal(validatePlayers(['A', 'B', 'C', 'D']).ok, true);
});
test('answer normalization trims and limits length', () => {
  assert.equal(normalizeAnswer('  salmon   roe  '), 'salmon roe');
  assert.equal(normalizeAnswer('x'.repeat(100)).length, 80);
});
test('role assignment is deterministic and target differs', () => {
  const game = createGame({ names: ['A', 'B', 'C', 'D'], seed: 123, totalRounds: 5 });
  const first = startRound(game);
  const second = startRound(game);
  assert.equal(first.currentRound.impostorId, second.currentRound.impostorId);
  assert.notEqual(first.currentRound.impostorId, first.currentRound.targetId);
});
test('question ties use the supplied random source', () => {
  assert.equal(chooseQuestion(['A', 'B', 'C'], { p1: 'A', p2: 'B' }, () => 0.99), 'B');
});
test('scoring covers success, failure and tied lead', () => {
  const players = ['p1', 'p2', 'p3', 'p4'].map((id) => ({ id, name: id, score: 0 }));
  const success = scoreRound({ players, impostorId: 'p1', targetId: 'p2', answerOrder: ['p1','p2','p3','p4'], votes: { p1:'p3', p2:'p1', p3:'p2', p4:'p2' } });
  assert.equal(success.impostorOutcome, 'success');
  assert.equal(success.points.p1, 3);
  assert.equal(success.points.p2, 3);
  const failed = scoreRound({ players, impostorId: 'p1', targetId: 'p2', answerOrder: ['p1','p2','p3','p4'], votes: { p1:'p2', p2:'p1', p3:'p1', p4:'p1' } });
  assert.equal(failed.impostorOutcome, 'failed');
  const partial = scoreRound({ players, impostorId: 'p1', targetId: 'p2', answerOrder: ['p1','p2','p3','p4'], votes: { p1:'p2', p2:'p1', p3:'p1', p4:'p2' } });
  assert.equal(partial.impostorOutcome, 'partial');
  assert.equal(partial.points.p1, 1);
});
test('applying a result updates cumulative scores', () => {
  let game = createGame({ names: ['A', 'B', 'C', 'D'], seed: 9, totalRounds: 3 });
  game = startRound(game);
  const ids = game.players.map((p) => p.id);
  game.currentRound.answerOrder = buildAnswerOrder(ids, 77);
  const imp = game.currentRound.impostorId;
  for (const participant of game.players) game.currentRound.votes[participant.id] = participant.id === imp ? ids.find((id) => id !== imp) : imp;
  const scored = applyRoundResult(game);
  assert.equal(scored.history.length, 1);
  assert.ok(scored.players.some((p) => p.score > 0));
});
