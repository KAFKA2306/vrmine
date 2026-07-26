import { clearState, escapeHtml, loadState, registerServiceWorker, saveState, showToast } from '../../assets/platform.js';

const GAME_ID = 'abyss-invasion';
const root = document.querySelector('[data-root]');
const title = document.querySelector('[data-title]');
const status = document.querySelector('[data-status]');
let state = loadState(GAME_ID);

function newState(names) {
  return {
    version: 1,
    round: 1,
    turnIndex: 0,
    finished: false,
    players: names.map((name, index) => ({ id: `p${index + 1}`, name: name.trim(), territories: 1, largestArea: 1 })),
    log: []
  };
}
function persist() { if (state) saveState(GAME_ID, state); }
function current() { return state.players[state.turnIndex]; }
function setup() {
  title.textContent = 'プレイヤー設定';
  status.textContent = '未開始';
  root.innerHTML = `
    <form data-setup><div class="form-grid">${[1,2,3,4].map((n) => `<div class="field"><label for="player-${n}">プレイヤー${n}</label><input id="player-${n}" name="player" maxlength="24" required value="教団${n}"></div>`).join('')}</div>
    <p class="help">全員1区域から開始する標準設定です。</p>
    <div class="inline-actions"><button class="btn btn-primary" type="submit">進行を開始</button></div></form>`;
  root.querySelector('[data-setup]').addEventListener('submit', (event) => {
    event.preventDefault();
    const names = new FormData(event.currentTarget).getAll('player').map(String);
    if (new Set(names.map((name) => name.trim().toLowerCase())).size !== 4) return showToast('異なる4つの名前を入力してください。');
    state = newState(names);
    persist();
    render();
  });
}
function actionLabel(action) {
  return { invade: '隣接侵蝕', hide: '遠隔潜伏', clash: '教団抗争', ritual: '儀式', pass: 'パス' }[action] ?? action;
}
function render() {
  if (!state) return setup();
  if (state.finished) return renderResult();
  const player = current();
  title.textContent = `第${state.round}ラウンド · ${escapeHtml(player.name)}の手番`;
  status.textContent = `${state.turnIndex + 1} / 4人目`;
  root.innerHTML = `
    <div class="score-list">${state.players.map((p, index) => `<div class="score-row"><span class="score-person"><span class="rank-number">${index + 1}</span>${escapeHtml(p.name)}</span><span><strong>${p.territories}区域</strong> · 最大連続${p.largestArea}</span></div>`).join('')}</div>
    <form data-turn-form style="margin-top:20px">
      <div class="form-grid">
        <div class="field"><label for="action">行動</label><select id="action" name="action"><option value="invade">隣接侵蝕</option><option value="hide">遠隔潜伏</option><option value="clash">教団抗争</option><option value="ritual">儀式</option><option value="pass">パス</option></select></div>
        <div class="field"><label for="territory-delta">支配区域の増減</label><select id="territory-delta" name="delta"><option value="0">変更なし</option><option value="1">+1区域</option><option value="2">+2区域</option><option value="-1">−1区域</option></select></div>
        <div class="field"><label for="largest">最大連続領域群</label><input id="largest" name="largest" type="number" min="1" max="40" value="${player.largestArea}"></div>
        <div class="field"><label for="note">メモ</label><input id="note" name="note" maxlength="80" placeholder="能力名・対象区域など"></div>
      </div>
      <div class="inline-actions"><button class="btn" type="button" data-roll>抗争ダイスを振る</button><button class="btn btn-primary" type="submit">手番を確定</button></div>
      <p class="help" data-dice-result>抗争は攻撃側・防御側が1D6を振り、同値なら防御側勝利。</p>
    </form>
    <h3 style="margin-top:24px">直近の記録</h3>
    <div class="log-list">${state.log.slice(-6).reverse().map((entry) => `<div class="log-entry"><strong>R${entry.round} ${escapeHtml(entry.player)}</strong><span>${escapeHtml(actionLabel(entry.action))}${entry.delta ? ` · ${entry.delta > 0 ? '+' : ''}${entry.delta}区域` : ''}${entry.note ? ` · ${escapeHtml(entry.note)}` : ''}</span></div>`).join('') || '<p class="help">まだ記録はありません。</p>'}</div>`;
  root.querySelector('[data-roll]').addEventListener('click', () => {
    const attack = Math.floor(Math.random() * 6) + 1;
    const defense = Math.floor(Math.random() * 6) + 1;
    const winner = attack > defense ? '攻撃側勝利' : '防御側勝利';
    root.querySelector('[data-dice-result]').innerHTML = `<strong>攻撃 ${attack} — 防御 ${defense}：${winner}</strong>`;
  });
  root.querySelector('[data-turn-form]').addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const delta = Number(data.get('delta'));
    player.territories = Math.max(0, Math.min(40, player.territories + delta));
    player.largestArea = Math.max(1, Math.min(player.territories || 1, Number(data.get('largest')) || 1));
    state.log.push({ round: state.round, player: player.name, action: data.get('action'), delta, note: String(data.get('note') ?? '').trim() });
    state.turnIndex += 1;
    if (state.turnIndex >= 4) { state.turnIndex = 0; state.round += 1; }
    if (state.round > 7) state.finished = true;
    persist();
    render();
  });
}
function renderResult() {
  title.textContent = '最終結果';
  status.textContent = '7ラウンド終了';
  const ordered = [...state.players].sort((a, b) => b.territories - a.territories || b.largestArea - a.largestArea || a.name.localeCompare(b.name, 'ja'));
  root.innerHTML = `<div class="result-banner"><p class="eyebrow">Dominant cult</p><h3>${escapeHtml(ordered[0].name)}</h3><p>${ordered[0].territories}区域、最大連続領域群${ordered[0].largestArea}で首位です。</p></div><div class="score-list" style="margin-top:14px">${ordered.map((p, index) => `<div class="score-row"><span class="score-person"><span class="rank-number">${index + 1}</span>${escapeHtml(p.name)}</span><strong>${p.territories}区域 · 最大${p.largestArea}</strong></div>`).join('')}</div><div class="inline-actions"><button class="btn btn-primary" type="button" data-rematch>同じメンバーで再戦</button></div>`;
  root.querySelector('[data-rematch]').addEventListener('click', () => { state = newState(state.players.map((p) => p.name)); persist(); render(); });
}

document.querySelector('[data-reset]').addEventListener('click', () => {
  if (!window.confirm('深淵侵蝕の進行記録を削除しますか？')) return;
  clearState(GAME_ID); state = null; setup();
});
registerServiceWorker();
render();
