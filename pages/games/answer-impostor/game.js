import {
  DEFAULT_QUESTIONS,
  applyRoundResult,
  buildAnswerOrder,
  chooseQuestion,
  createGame,
  mulberry32,
  normalizeAnswer,
  ranking,
  startRound
} from './engine.mjs';
import {
  clearState,
  downloadJson,
  escapeHtml,
  loadState,
  readJsonFile,
  registerServiceWorker,
  saveState,
  showToast
} from '../../assets/platform.js';

const GAME_ID = 'answer-impostor';
const root = document.querySelector('[data-game-root]');
const title = document.querySelector('[data-screen-title]');
const statusNode = document.querySelector('[data-status]');
const progress = [...document.querySelectorAll('[data-progress] .progress-step')];
let state = loadState(GAME_ID);
let privateVisible = false;
let timerHandle = null;
let timerSeconds = 60;

const phases = {
  setup: 0,
  ready: 0,
  'role-reveal': 1,
  'question-vote': 2,
  'answer-entry': 3,
  discussion: 4,
  voting: 5,
  'round-result': 6,
  finished: 6
};

function persist() {
  if (state) saveState(GAME_ID, state);
}

function player(id) {
  return state.players.find((entry) => entry.id === id);
}

function setChrome(screenTitle, statusText, phase = state?.status ?? 'setup') {
  title.textContent = screenTitle;
  statusNode.textContent = statusText;
  const active = phases[phase] ?? 0;
  progress.forEach((step, index) => step.classList.toggle('is-active', index < active));
}

function actions(html) {
  return `<div class="inline-actions">${html}</div>`;
}

function screenGuard(name, instruction, buttonLabel = '自分の画面を開く') {
  return `
    <div class="private-card">
      <div>
        <p class="eyebrow">Pass the device</p>
        <h3 class="secret-role">${escapeHtml(name)}さんへ</h3>
        <p class="help">${escapeHtml(instruction)}</p>
        ${actions(`<button class="btn btn-primary" type="button" data-show-private>${escapeHtml(buttonLabel)}</button>`)}
      </div>
    </div>`;
}

function setupTemplate() {
  const resume = state && state.status !== 'setup';
  if (resume) {
    const scoreRows = state.players.map((p) => `<div class="score-row"><span>${escapeHtml(p.name)}</span><strong>${p.score}点</strong></div>`).join('');
    return `
      <div class="result-banner"><h3>保存されたゲームがあります</h3><p>ラウンド ${state.roundNumber} / ${state.totalRounds}、現在の進行：${escapeHtml(state.status)}</p></div>
      <div class="score-list" style="margin-top:12px">${scoreRows}</div>
      ${actions('<button class="btn btn-primary" type="button" data-resume>続きから再開</button><button class="btn" type="button" data-new-game>新しいゲーム</button>')}`;
  }
  const names = ['プレイヤー1', 'プレイヤー2', 'プレイヤー3', 'プレイヤー4'];
  return `
    <form data-setup-form>
      <div class="form-grid">
        <div class="field"><label for="rounds">ラウンド数</label><select id="rounds" name="rounds"><option value="3">3ラウンド</option><option value="5" selected>5ラウンド</option><option value="8">8ラウンド</option></select></div>
        <div class="field"><label>プレイヤー人数</label><div class="inline-actions" style="margin-top:0"><button class="btn btn-small" type="button" data-remove-player>−</button><strong data-player-count>4人</strong><button class="btn btn-small" type="button" data-add-player>＋</button></div></div>
        <div class="field field-full"><label>プレイヤー名</label><div class="player-list" data-player-list>${names.map((name, index) => playerInput(index, name)).join('')}</div></div>
        <div class="field field-full"><label for="custom-questions">追加質問（任意・1行1問）</label><textarea id="custom-questions" name="customQuestions" placeholder="最近いちばん笑ったことは？&#10;一日だけ別の職業になるなら？"></textarea><span class="help">標準12問に追加されます。個人情報や答えにくい質問は避けてください。</span></div>
      </div>
      <p class="error hidden" data-form-error></p>
      ${actions('<button class="btn btn-primary" type="submit">ゲームを作成</button>')}
    </form>`;
}

function playerInput(index, value = '') {
  return `<div class="player-row"><span class="player-index">${index + 1}</span><input aria-label="プレイヤー${index + 1}の名前" name="player" maxlength="24" required value="${escapeHtml(value)}"></div>`;
}

function renderSetup(forceNew = false) {
  if (forceNew) state = null;
  setChrome('ゲーム設定', state ? '保存データあり' : '未開始', 'setup');
  root.innerHTML = setupTemplate();
  bindSetup();
}

function bindSetup() {
  root.querySelector('[data-resume]')?.addEventListener('click', render);
  root.querySelector('[data-new-game]')?.addEventListener('click', () => renderSetup(true));
  const list = root.querySelector('[data-player-list]');
  const count = root.querySelector('[data-player-count]');
  const refresh = () => {
    if (!list) return;
    [...list.children].forEach((row, index) => {
      row.querySelector('.player-index').textContent = String(index + 1);
      row.querySelector('input').setAttribute('aria-label', `プレイヤー${index + 1}の名前`);
    });
    if (count) count.textContent = `${list.children.length}人`;
  };
  root.querySelector('[data-add-player]')?.addEventListener('click', () => {
    if (list.children.length >= 8) return;
    list.insertAdjacentHTML('beforeend', playerInput(list.children.length, `プレイヤー${list.children.length + 1}`));
    refresh();
  });
  root.querySelector('[data-remove-player]')?.addEventListener('click', () => {
    if (list.children.length <= 4) return;
    list.lastElementChild.remove();
    refresh();
  });
  root.querySelector('[data-setup-form]')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const names = form.getAll('player');
    const custom = String(form.get('customQuestions') ?? '').split(/\r?\n/).map((q) => q.trim()).filter(Boolean).slice(0, 30);
    try {
      state = createGame({ names, totalRounds: Number(form.get('rounds')) });
      state.questions = [...DEFAULT_QUESTIONS, ...custom];
      persist();
      renderReady();
    } catch (error) {
      const node = root.querySelector('[data-form-error]');
      node.textContent = error.message;
      node.classList.remove('hidden');
    }
  });
}

function renderReady() {
  setChrome('ゲーム準備完了', `全${state.totalRounds}ラウンド`, 'ready');
  const rows = state.players.map((p, index) => `<div class="score-row"><span class="score-person"><span class="rank-number">${index + 1}</span>${escapeHtml(p.name)}</span><strong>0点</strong></div>`).join('');
  root.innerHTML = `<p>端末を全員が操作できる位置に置きます。役割確認以降は、表示された名前の人だけが画面を見てください。</p><div class="score-list">${rows}</div>${actions('<button class="btn btn-primary" type="button" data-start-round>第1ラウンドを開始</button>')}`;
  root.querySelector('[data-start-round]').addEventListener('click', () => {
    state = startRound(state, state.questions);
    privateVisible = false;
    persist();
    render();
  });
}

function renderRoleReveal() {
  const round = state.currentRound;
  const current = player(round.roleOrder[round.roleRevealIndex]);
  setChrome('秘密の役割', `ラウンド ${state.roundNumber} / ${state.totalRounds}`, 'role-reveal');
  if (!privateVisible) {
    root.innerHTML = screenGuard(current.name, 'ほかの人は画面を見ないでください。');
    root.querySelector('[data-show-private]').addEventListener('click', () => { privateVisible = true; renderRoleReveal(); });
    return;
  }
  const isImpostor = current.id === round.impostorId;
  const target = player(round.targetId);
  root.innerHTML = `
    <div class="private-card"><div>
      <p class="eyebrow">Your secret role</p>
      <div class="secret-role">${isImpostor ? '擬態者' : '通常プレイヤー'}</div>
      <p class="secret-target">${isImpostor ? `${escapeHtml(target.name)}さんになりきって回答` : '自分自身として回答'}</p>
      <p class="help">${isImpostor ? '質問への本当の自分の答えではなく、対象者が答えそうな内容を考えてください。' : '擬態者が誰かはまだ分かりません。役割を口に出さないでください。'}</p>
      ${actions('<button class="btn btn-primary" type="button" data-hide-next>確認して画面を隠す</button>')}
    </div></div>`;
  root.querySelector('[data-hide-next]').addEventListener('click', () => {
    privateVisible = false;
    round.roleRevealIndex += 1;
    if (round.roleRevealIndex >= round.roleOrder.length) {
      state.status = 'question-vote';
      round.questionVoteIndex = 0;
    }
    persist();
    render();
  });
}

function renderQuestionVote() {
  const round = state.currentRound;
  const voter = state.players[round.questionVoteIndex];
  setChrome('質問に投票', `${round.questionVoteIndex + 1} / ${state.players.length}人`, 'question-vote');
  if (!privateVisible) {
    root.innerHTML = screenGuard(voter.name, '好きな質問を1つ選びます。投票内容は全員完了まで非公開です。', '投票画面を開く');
    root.querySelector('[data-show-private]').addEventListener('click', () => { privateVisible = true; renderQuestionVote(); });
    return;
  }
  root.innerHTML = `
    <form data-question-form>
      <div class="choice-list">${round.questionCandidates.map((question, index) => `<label class="choice"><input type="radio" name="question" value="${escapeHtml(question)}" ${index === 0 ? 'required' : ''}><span class="choice-content"><span class="choice-title">${escapeHtml(question)}</span></span></label>`).join('')}</div>
      ${actions('<button class="btn btn-primary" type="submit">投票して画面を隠す</button>')}
    </form>`;
  root.querySelector('[data-question-form]').addEventListener('submit', (event) => {
    event.preventDefault();
    const selected = new FormData(event.currentTarget).get('question');
    if (!selected) return;
    round.questionVotes[voter.id] = selected;
    round.questionVoteIndex += 1;
    privateVisible = false;
    if (round.questionVoteIndex >= state.players.length) {
      const random = mulberry32((state.seed + state.roundNumber * 101) >>> 0);
      round.selectedQuestion = chooseQuestion(round.questionCandidates, round.questionVotes, random);
      round.answerEntryIndex = 0;
      state.status = 'answer-entry';
    }
    persist();
    render();
  });
}

function renderAnswerEntry() {
  const round = state.currentRound;
  const answerer = state.players[round.answerEntryIndex];
  setChrome('回答を入力', `${round.answerEntryIndex + 1} / ${state.players.length}人`, 'answer-entry');
  if (!privateVisible) {
    root.innerHTML = screenGuard(answerer.name, '自分の回答画面だけを見て入力します。', '回答画面を開く');
    root.querySelector('[data-show-private]').addEventListener('click', () => { privateVisible = true; renderAnswerEntry(); });
    return;
  }
  const isImpostor = answerer.id === round.impostorId;
  const target = player(round.targetId);
  root.innerHTML = `
    <form data-answer-form>
      <div class="result-banner"><p class="eyebrow">Question</p><h3>${escapeHtml(round.selectedQuestion)}</h3></div>
      <div class="field" style="margin-top:16px"><label for="answer">${isImpostor ? `${escapeHtml(target.name)}さんとして回答` : '自分自身として回答'}</label><textarea id="answer" name="answer" maxlength="80" required autocomplete="off" placeholder="80文字以内"></textarea><span class="help">${isImpostor ? '本人の好み・言葉選び・具体性を真似してください。' : '短くても構いません。自分らしい具体的な答えが推理材料になります。'}</span></div>
      <p class="error hidden" data-answer-error></p>
      ${actions('<button class="btn btn-primary" type="submit">回答を確定して隠す</button>')}
    </form>`;
  const input = root.querySelector('#answer');
  input.focus();
  root.querySelector('[data-answer-form]').addEventListener('submit', (event) => {
    event.preventDefault();
    const answer = normalizeAnswer(new FormData(event.currentTarget).get('answer'));
    if (!answer) {
      const node = root.querySelector('[data-answer-error]');
      node.textContent = '回答を入力してください。';
      node.classList.remove('hidden');
      return;
    }
    round.answers[answerer.id] = answer;
    round.answerEntryIndex += 1;
    privateVisible = false;
    if (round.answerEntryIndex >= state.players.length) {
      round.answerOrder = buildAnswerOrder(state.players.map((p) => p.id), (state.seed + state.roundNumber * 997) >>> 0);
      state.status = 'discussion';
      timerSeconds = 60;
    }
    persist();
    render();
  });
}

function anonymousAnswers({ includeVotes = false, reveal = false } = {}) {
  const round = state.currentRound;
  return round.answerOrder.map((ownerId, index) => {
    const voteCount = round.result?.voteCounts?.[ownerId] ?? 0;
    const classes = ['answer-card'];
    if (reveal && ownerId === round.impostorId) classes.push('is-impostor');
    return `<div class="${classes.join(' ')}"><span class="answer-letter">${String.fromCharCode(65 + index)}</span><div class="answer-body"><div class="answer-text">${escapeHtml(round.answers[ownerId])}</div>${includeVotes ? `<div class="answer-votes">${voteCount}票${reveal ? ` · ${escapeHtml(player(ownerId).name)}` : ''}</div>` : ''}</div></div>`;
  }).join('');
}

function renderDiscussion() {
  setChrome('回答を比較・議論', `ラウンド ${state.roundNumber} / ${state.totalRounds}`, 'discussion');
  root.innerHTML = `
    <div class="result-banner"><p class="eyebrow">Question</p><h3>${escapeHtml(state.currentRound.selectedQuestion)}</h3></div>
    <div class="answer-list" style="margin-top:14px">${anonymousAnswers()}</div>
    <div class="panel-heading" style="margin-top:18px"><div><strong>議論タイマー</strong><p class="help">必要なら延長できます。</p></div><div class="timer" data-timer>${formatTime(timerSeconds)}</div></div>
    ${actions('<button class="btn" type="button" data-timer-toggle>タイマー開始</button><button class="btn" type="button" data-timer-add>＋30秒</button><button class="btn btn-primary" type="button" data-to-vote>予測投票へ</button>')}`;
  const toggle = root.querySelector('[data-timer-toggle]');
  toggle.addEventListener('click', () => {
    if (timerHandle) { stopTimer(); toggle.textContent = 'タイマー再開'; }
    else { startTimer(); toggle.textContent = '一時停止'; }
  });
  root.querySelector('[data-timer-add]').addEventListener('click', () => { timerSeconds += 30; updateTimer(); });
  root.querySelector('[data-to-vote]').addEventListener('click', () => {
    stopTimer();
    state.status = 'voting';
    state.currentRound.voteIndex = 0;
    privateVisible = false;
    persist();
    render();
  });
}

function formatTime(seconds) {
  return `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
}
function updateTimer() {
  const node = root.querySelector('[data-timer]');
  if (node) node.textContent = formatTime(timerSeconds);
}
function startTimer() {
  if (timerHandle) return;
  timerHandle = window.setInterval(() => {
    timerSeconds = Math.max(0, timerSeconds - 1);
    updateTimer();
    if (timerSeconds === 0) { stopTimer(); showToast('議論時間が終了しました。'); }
  }, 1000);
}
function stopTimer() {
  window.clearInterval(timerHandle);
  timerHandle = null;
}

function renderVoting() {
  const round = state.currentRound;
  const voter = state.players[round.voteIndex];
  setChrome('擬態者の回答を予測', `${round.voteIndex + 1} / ${state.players.length}人`, 'voting');
  if (!privateVisible) {
    root.innerHTML = screenGuard(voter.name, '擬態者が書いたと思う回答を1つ選びます。', '投票画面を開く');
    root.querySelector('[data-show-private]').addEventListener('click', () => { privateVisible = true; renderVoting(); });
    return;
  }
  root.innerHTML = `
    <form data-vote-form>
      <div class="answer-list">${round.answerOrder.map((ownerId, index) => {
        const own = ownerId === voter.id;
        return `<label class="choice ${own ? 'hidden' : ''}"><input type="radio" name="answerOwner" value="${ownerId}" required><span class="answer-letter">${String.fromCharCode(65 + index)}</span><span class="choice-content"><span class="choice-title">${escapeHtml(round.answers[ownerId])}</span></span></label>`;
      }).join('')}</div>
      <p class="help">自分が書いた回答には投票できません。</p>
      ${actions('<button class="btn btn-primary" type="submit">投票して画面を隠す</button>')}
    </form>`;
  root.querySelector('[data-vote-form]').addEventListener('submit', (event) => {
    event.preventDefault();
    const selected = new FormData(event.currentTarget).get('answerOwner');
    if (!selected) return;
    round.votes[voter.id] = selected;
    round.voteIndex += 1;
    privateVisible = false;
    if (round.voteIndex >= state.players.length) state = applyRoundResult(state);
    persist();
    render();
  });
}

function renderRoundResult() {
  const round = state.currentRound;
  const result = round.result;
  const impostor = player(round.impostorId);
  const target = player(round.targetId);
  const outcomeText = result.impostorOutcome === 'success' ? '擬態成功' : result.impostorOutcome === 'partial' ? '部分成功' : '擬態失敗';
  setChrome('結果発表', `ラウンド ${state.roundNumber} 完了`, 'round-result');
  root.innerHTML = `
    <div class="result-banner ${result.impostorOutcome === 'failed' ? 'is-failed' : ''}"><p class="eyebrow">${escapeHtml(outcomeText)}</p><h3>擬態者は ${escapeHtml(impostor.name)} さん</h3><p>${escapeHtml(target.name)}さんになりきって回答しました。擬態者の獲得は${result.impostorPoints}点です。</p></div>
    <div class="answer-list" style="margin-top:14px">${anonymousAnswers({ includeVotes: true, reveal: true })}</div>
    <h3 style="margin-top:22px">今回の得点</h3>
    <div class="score-list">${ranking(state.players).map((p, index) => `<div class="score-row"><span class="score-person"><span class="rank-number">${index + 1}</span>${escapeHtml(p.name)}</span><span><span class="delta">+${result.points[p.id] ?? 0}</span> <span class="score-value">${p.score}点</span></span></div>`).join('')}</div>
    ${result.targetBonus ? `<p class="success" style="margin-top:12px">${escapeHtml(target.name)}さんは「本物の回答が偽物より疑われた」ボーナスを獲得しました。</p>` : ''}
    ${actions(state.roundNumber >= state.totalRounds ? '<button class="btn btn-primary" type="button" data-finish>最終結果を見る</button>' : `<button class="btn btn-primary" type="button" data-next-round>第${state.roundNumber + 1}ラウンドへ</button>`)}`;
  root.querySelector('[data-next-round]')?.addEventListener('click', () => {
    state = startRound({ ...state, status: 'ready', currentRound: null }, state.questions);
    privateVisible = false;
    persist();
    render();
  });
  root.querySelector('[data-finish]')?.addEventListener('click', () => { state.status = 'finished'; persist(); render(); });
}

function renderFinished() {
  const ordered = ranking(state.players);
  const top = ordered[0].score;
  const winners = ordered.filter((p) => p.score === top);
  setChrome('最終結果', `${state.totalRounds}ラウンド終了`, 'finished');
  root.innerHTML = `
    <div class="result-banner"><p class="eyebrow">Winner</p><h3>${winners.map((p) => escapeHtml(p.name)).join('・')}</h3><p>${top}点で${winners.length > 1 ? '同時優勝' : '優勝'}です。</p></div>
    <div class="score-list" style="margin-top:14px">${ordered.map((p, index) => `<div class="score-row"><span class="score-person"><span class="rank-number">${index + 1}</span>${escapeHtml(p.name)}</span><span class="score-value">${p.score}点</span></div>`).join('')}</div>
    ${actions('<button class="btn btn-primary" type="button" data-rematch>同じメンバーで再戦</button><button class="btn" type="button" data-new-game>メンバーを変更</button>')}`;
  root.querySelector('[data-rematch]').addEventListener('click', () => {
    const names = state.players.map((p) => p.name);
    const questions = state.questions;
    state = createGame({ names, totalRounds: state.totalRounds });
    state.questions = questions;
    persist();
    renderReady();
  });
  root.querySelector('[data-new-game]').addEventListener('click', () => { clearState(GAME_ID); state = null; renderSetup(true); });
}

function render() {
  stopTimer();
  if (!state) return renderSetup();
  switch (state.status) {
    case 'ready': return renderReady();
    case 'role-reveal': return renderRoleReveal();
    case 'question-vote': return renderQuestionVote();
    case 'answer-entry': return renderAnswerEntry();
    case 'discussion': return renderDiscussion();
    case 'voting': return renderVoting();
    case 'round-result': return renderRoundResult();
    case 'finished': return renderFinished();
    default: return renderSetup();
  }
}

document.querySelector('[data-reset]').addEventListener('click', () => {
  if (!window.confirm('保存中のゲームを削除して最初から始めますか？')) return;
  stopTimer();
  clearState(GAME_ID);
  state = null;
  privateVisible = false;
  renderSetup(true);
});

document.querySelector('[data-export]').addEventListener('click', () => {
  if (!state) return showToast('書き出すゲームデータがありません。');
  downloadJson(`answer-impostor-round-${state.roundNumber}.json`, state);
});

document.querySelector('[data-import]').addEventListener('change', async (event) => {
  const [file] = event.target.files;
  if (!file) return;
  try {
    const imported = await readJsonFile(file);
    if (imported.version !== 1 || !Array.isArray(imported.players) || !imported.status) throw new Error('Answer Impostorの保存データではありません。');
    state = imported;
    privateVisible = false;
    persist();
    showToast('保存データを読み込みました。');
    render();
  } catch (error) {
    showToast(error.message);
  } finally {
    event.target.value = '';
  }
});

registerServiceWorker();
render();
