export const DEFAULT_QUESTIONS = [
  '好きな寿司ネタは？', '飼いたい動物は？', '旅行するならどこ？', '好きな乗り物は？',
  '一番落ち着く場所は？', '休日にしたいことは？', '好きな季節は？',
  '無人島に持っていくものは？', 'もらってうれしいプレゼントは？',
  'つい買ってしまうものは？', '朝に最初にすることは？', '理想の部屋に置きたいものは？'
];

export function normalizePlayerName(value) {
  return String(value ?? '').trim().replace(/\s+/g, ' ').slice(0, 24);
}
export function normalizeAnswer(value) {
  return String(value ?? '').trim().replace(/\s+/g, ' ').slice(0, 80);
}
export function validatePlayers(names) {
  const normalized = names.map(normalizePlayerName).filter(Boolean);
  if (normalized.length < 4 || normalized.length > 8) return { ok: false, error: 'プレイヤーは4〜8人にしてください。' };
  const folded = normalized.map((name) => name.toLocaleLowerCase('ja-JP'));
  if (new Set(folded).size !== normalized.length) return { ok: false, error: '同じ名前は使用できません。' };
  return { ok: true, players: normalized.map((name, index) => ({ id: `p${index + 1}`, name, score: 0 })) };
}
export function createGame({ names, totalRounds = 5, seed = Date.now() }) {
  const checked = validatePlayers(names);
  if (!checked.ok) throw new Error(checked.error);
  const rounds = Number(totalRounds);
  if (!Number.isInteger(rounds) || rounds < 3 || rounds > 8) throw new Error('ラウンド数は3〜8にしてください。');
  return { version: 1, seed: Number(seed) >>> 0, totalRounds: rounds, roundNumber: 0, players: checked.players, history: [], currentRound: null, status: 'ready' };
}
export function mulberry32(seed) {
  let value = seed >>> 0;
  return function random() {
    value += 0x6D2B79F5;
    let t = value;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
export function shuffled(items, random = Math.random) {
  const result = [...items];
  for (let i = result.length - 1; i > 0; i -= 1) {
    const j = Math.floor(random() * (i + 1));
    [result[i], result[j]] = [result[j], result[i]];
  }
  return result;
}
export function selectQuestionCandidates(questions, count, random = Math.random) {
  const source = questions.map((q) => String(q).trim()).filter(Boolean);
  return shuffled([...new Set(source)], random).slice(0, Math.min(count, source.length));
}
export function startRound(game, questions = DEFAULT_QUESTIONS) {
  if (!game || !Array.isArray(game.players)) throw new Error('ゲーム状態が不正です。');
  if (game.roundNumber >= game.totalRounds) throw new Error('規定ラウンドは終了しています。');
  const nextRound = game.roundNumber + 1;
  const random = mulberry32((game.seed + nextRound * 2654435761) >>> 0);
  const playerOrder = shuffled(game.players.map((p) => p.id), random);
  const impostorId = playerOrder[0];
  const targetId = playerOrder.find((id) => id !== impostorId);
  return {
    ...game,
    roundNumber: nextRound,
    status: 'role-reveal',
    currentRound: {
      number: nextRound, impostorId, targetId, roleOrder: playerOrder, roleRevealIndex: 0,
      questionCandidates: selectQuestionCandidates(questions, 3, random), questionVotes: {}, selectedQuestion: null,
      answers: {}, answerOrder: [], votes: {}, result: null
    }
  };
}
export function chooseQuestion(questionCandidates, questionVotes, random = Math.random) {
  if (!questionCandidates.length) throw new Error('質問候補がありません。');
  const counts = Object.fromEntries(questionCandidates.map((question) => [question, 0]));
  Object.values(questionVotes).forEach((question) => { if (Object.hasOwn(counts, question)) counts[question] += 1; });
  const max = Math.max(...Object.values(counts));
  const winners = questionCandidates.filter((question) => counts[question] === max);
  return winners[Math.floor(random() * winners.length)];
}
export function buildAnswerOrder(playerIds, seed) {
  return shuffled(playerIds, mulberry32(seed >>> 0));
}
export function scoreRound({ players, impostorId, targetId, answerOrder, votes }) {
  const voteCounts = Object.fromEntries(answerOrder.map((id) => [id, 0]));
  for (const [voterId, answerOwnerId] of Object.entries(votes)) {
    if (!players.some((p) => p.id === voterId)) continue;
    if (Object.hasOwn(voteCounts, answerOwnerId)) voteCounts[answerOwnerId] += 1;
  }
  const maxVotes = Math.max(0, ...Object.values(voteCounts));
  const impostorVotes = voteCounts[impostorId] ?? 0;
  const leaders = Object.entries(voteCounts).filter(([, count]) => count === maxVotes).map(([id]) => id);
  const impostorIsLeader = leaders.includes(impostorId);
  let impostorOutcome = 'success';
  let impostorPoints = 3;
  if (impostorIsLeader && leaders.length === 1) { impostorOutcome = 'failed'; impostorPoints = 0; }
  else if (impostorIsLeader) { impostorOutcome = 'partial'; impostorPoints = 1; }
  const points = Object.fromEntries(players.map((p) => [p.id, 0]));
  const correctVoters = [];
  for (const participant of players) {
    if (participant.id === impostorId) continue;
    if (votes[participant.id] === impostorId) { points[participant.id] += 2; correctVoters.push(participant.id); }
  }
  points[impostorId] += impostorPoints;
  const targetBonus = (voteCounts[targetId] ?? 0) > impostorVotes;
  if (targetBonus) points[targetId] += 1;
  return { voteCounts, leaders, correctVoters, impostorOutcome, impostorPoints, targetBonus, points };
}
export function applyRoundResult(game) {
  const round = game.currentRound;
  if (!round) throw new Error('進行中のラウンドがありません。');
  const result = scoreRound({ players: game.players, impostorId: round.impostorId, targetId: round.targetId, answerOrder: round.answerOrder, votes: round.votes });
  const players = game.players.map((participant) => ({ ...participant, score: participant.score + (result.points[participant.id] ?? 0) }));
  const completedRound = { ...round, result };
  return { ...game, players, currentRound: completedRound, history: [...game.history, completedRound], status: game.roundNumber >= game.totalRounds ? 'finished' : 'round-result' };
}
export function ranking(players) {
  return [...players].sort((a, b) => b.score - a.score || a.name.localeCompare(b.name, 'ja'));
}
