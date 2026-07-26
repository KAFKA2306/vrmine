import { clearState, escapeHtml, loadState, registerServiceWorker, saveState, showToast } from '../../assets/platform.js';

const GAME_ID = 'abyss-invasion';
const root = document.querySelector('[data-root]');
const title = document.querySelector('[data-title]');
const status = document.querySelector('[data-status]');
let state = loadState(GAME_ID);
let networkSocket = null;
let networkSession = null;

function newState(names) {
  return {
    version: 1,
    mode: 'local',
    round: 1,
    turnIndex: 0,
    finished: false,
    players: names.map((name, index) => ({ id: `p${index + 1}`, name: name.trim(), territories: 1, largestArea: 1 })),
    log: []
  };
}

function persist() {
  if (state) saveState(GAME_ID, state);
}

function current() {
  return state.players[state.turnIndex];
}

function closeNetwork() {
  if (networkSocket) networkSocket.close();
  networkSocket = null;
  networkSession = null;
}

function sendNetwork(message) {
  networkSocket.send(JSON.stringify(message));
}

function setup() {
  title.textContent = 'プレイヤー設定';
  status.textContent = '未開始';
  root.innerHTML = `
    <form data-setup><h3>この端末で進行</h3><div class="form-grid">${[1, 2, 3, 4].map((n) => `<div class="field"><label for="player-${n}">プレイヤー${n}</label><input id="player-${n}" name="player" maxlength="24" required value="教団${n}"></div>`).join('')}</div>
    <p class="help">全員1区域から開始するローカル進行です。</p>
    <div class="inline-actions"><button class="btn btn-primary" type="submit">ローカル進行を開始</button></div></form>
    <form data-network-form style="margin-top:28px"><h3>IPルームに接続</h3><div class="form-grid">
      <div class="field"><label for="server">WebSocketサーバー</label><input id="server" name="server" type="url" required value="ws://127.0.0.1:8787/abyss"></div>
      <div class="field"><label for="room">ルーム名</label><input id="room" name="room" maxlength="32" required value="abyss"></div>
      <div class="field"><label for="network-player">自分の名前</label><input id="network-player" name="name" maxlength="24" required value="教団1"></div>
    </div><p class="help">最初の接続者がホストとしてゲームを開始します。PagesはHTTPSのため、公開URLからはwss://サーバーを指定してください。</p>
    <div class="inline-actions"><button class="btn" type="submit">IPルームへ接続</button></div></form>`;
  root.querySelector('[data-setup]').addEventListener('submit', (event) => {
    event.preventDefault();
    closeNetwork();
    const names = new FormData(event.currentTarget).getAll('player').map(String);
    if (new Set(names.map((name) => name.trim().toLowerCase())).size !== 4) return showToast('異なる4つの名前を入力してください。');
    state = newState(names);
    persist();
    render();
  });
  root.querySelector('[data-network-form]').addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    connectNetwork(String(data.get('server')), String(data.get('room')).trim(), String(data.get('name')).trim());
  });
}

function connectNetwork(server, room, name) {
  closeNetwork();
  networkSession = { clientId: crypto.randomUUID(), room, name, server, playerIndex: -1 };
  title.textContent = `${room}へ接続中`;
  status.textContent = '接続中';
  networkSocket = new WebSocket(server);
  networkSocket.addEventListener('open', () => {
    status.textContent = '参加待ち';
    sendNetwork({ type: 'join', clientId: networkSession.clientId, room, name });
  });
  networkSocket.addEventListener('message', (event) => receiveNetwork(JSON.parse(event.data)));
  networkSocket.addEventListener('close', () => {
    if (networkSession) status.textContent = '切断';
    networkSocket = null;
    if (state?.mode === 'network') render();
  });
  networkSocket.addEventListener('error', () => showToast('IPルームへ接続できませんでした。'));
  renderWaiting();
}

function receiveNetwork(message) {
  if (message.type === 'ready') {
    networkSession.playerIndex = message.playerIndex;
    if (message.host) {
      state = newState([networkSession.name, '教団2', '教団3', '教団4']);
      state.mode = 'network';
      state.room = networkSession.room;
      state.server = networkSession.server;
      state.playerName = networkSession.name;
      persist();
      sendNetwork({ type: 'start', state });
    } else {
      renderWaiting();
    }
    return;
  }
  if (message.type === 'state') {
    state = message.state;
    state.mode = 'network';
    state.room = networkSession.room;
    state.server = networkSession.server;
    state.playerName = networkSession.name;
    networkSession.playerIndex = message.playerIndex;
    persist();
    render();
    return;
  }
  if (message.type === 'waiting') {
    renderWaiting();
    return;
  }
  if (message.type === 'error') {
    status.textContent = '参加不可';
    showToast(message.message);
  }
}

function renderWaiting() {
  title.textContent = `${networkSession.room} · IPルーム`;
  status.textContent = '参加待ち';
  root.innerHTML = `<div class="callout"><h3>ホストの開始を待っています</h3><p>サーバー：${escapeHtml(networkSession.server ?? '接続済み')}<br>ルーム：${escapeHtml(networkSession.room)}<br>プレイヤー：${escapeHtml(networkSession.name)}</p></div>`;
}

function renderNetworkOffline() {
  title.textContent = 'IPルームを再接続';
  status.textContent = '未接続';
  root.innerHTML = `<div class="callout"><h3>保存済みのネットワーク進行があります</h3><p>ルーム「${escapeHtml(state.room)}」の進行はこの端末に保存されています。</p><div class="inline-actions"><button class="btn btn-primary" type="button" data-reconnect>再接続設定を開く</button></div></div>`;
  root.querySelector('[data-reconnect]').addEventListener('click', () => {
    const savedRoom = state.room;
    const savedServer = state.server ?? 'ws://127.0.0.1:8787/abyss';
    const savedName = state.playerName ?? '教団1';
    setup();
    root.querySelector('[name=server]').value = savedServer;
    root.querySelector('[name=room]').value = savedRoom;
    root.querySelector('[name=name]').value = savedName;
  });
}

function actionLabel(action) {
  return { invade: '隣接侵蝕', hide: '遠隔潜伏', clash: '教団抗争', ritual: '儀式', pass: 'パス' }[action] ?? action;
}

function render() {
  if (!state) return setup();
  if (state.mode === 'network' && !networkSocket) return renderNetworkOffline();
  if (state.finished) return renderResult();
  const player = current();
  const networkTurn = state.mode === 'network';
  const isMyTurn = !networkTurn || networkSession?.playerIndex === state.turnIndex;
  title.textContent = `第${state.round}ラウンド · ${escapeHtml(player.name)}の手番`;
  status.textContent = networkTurn ? `${state.turnIndex + 1} / 4人目 · ${isMyTurn ? 'あなたの手番' : '他プレイヤーの手番'}` : `${state.turnIndex + 1} / 4人目`;
  root.innerHTML = `
    <div class="score-list">${state.players.map((p, index) => `<div class="score-row"><span class="score-person"><span class="rank-number">${index + 1}</span>${escapeHtml(p.name)}</span><span><strong>${p.territories}区域</strong> · 最大連続${p.largestArea}</span></div>`).join('')}</div>
    <form data-turn-form style="margin-top:20px">
      <div class="form-grid">
        <div class="field"><label for="action">行動</label><select id="action" name="action" ${isMyTurn ? '' : 'disabled'}><option value="invade">隣接侵蝕</option><option value="hide">遠隔潜伏</option><option value="clash">教団抗争</option><option value="ritual">儀式</option><option value="pass">パス</option></select></div>
        <div class="field"><label for="territory-delta">支配区域の増減</label><select id="territory-delta" name="delta" ${isMyTurn ? '' : 'disabled'}><option value="0">変更なし</option><option value="1">+1区域</option><option value="2">+2区域</option><option value="-1">−1区域</option></select></div>
        <div class="field"><label for="largest">最大連続領域群</label><input id="largest" name="largest" type="number" min="1" max="40" value="${player.largestArea}" ${isMyTurn ? '' : 'disabled'}></div>
        <div class="field"><label for="note">メモ</label><input id="note" name="note" maxlength="80" placeholder="能力名・対象区域など" ${isMyTurn ? '' : 'disabled'}></div>
      </div>
      <div class="inline-actions"><button class="btn" type="button" data-roll ${isMyTurn ? '' : 'disabled'}>抗争ダイスを振る</button><button class="btn btn-primary" type="submit" ${isMyTurn ? '' : 'disabled'}>手番を確定</button></div>
      <p class="help" data-dice-result>抗争は攻撃側・防御側が1D6を振り、同値なら防御側勝利。${networkTurn ? ` 現在は${escapeHtml(player.name)}の端末が操作します。` : ''}</p>
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
    if (!isMyTurn) return;
    const data = new FormData(event.currentTarget);
    const delta = Number(data.get('delta'));
    player.territories = Math.max(0, Math.min(40, player.territories + delta));
    player.largestArea = Math.max(1, Math.min(player.territories || 1, Number(data.get('largest')) || 1));
    state.log.push({ round: state.round, player: player.name, action: data.get('action'), delta, note: String(data.get('note') ?? '').trim() });
    state.turnIndex += 1;
    if (state.turnIndex >= 4) { state.turnIndex = 0; state.round += 1; }
    if (state.round > 7) state.finished = true;
    persist();
    if (networkTurn) sendNetwork({ type: 'state', state });
    render();
  });
}

function renderResult() {
  title.textContent = '最終結果';
  status.textContent = '7ラウンド終了';
  const ordered = [...state.players].sort((a, b) => b.territories - a.territories || b.largestArea - a.largestArea || a.name.localeCompare(b.name, 'ja'));
  root.innerHTML = `<div class="result-banner"><p class="eyebrow">Dominant cult</p><h3>${escapeHtml(ordered[0].name)}</h3><p>${ordered[0].territories}区域、最大連続領域群${ordered[0].largestArea}で首位です。</p></div><div class="score-list" style="margin-top:14px">${ordered.map((p, index) => `<div class="score-row"><span class="score-person"><span class="rank-number">${index + 1}</span>${escapeHtml(p.name)}</span><strong>${p.territories}区域 · 最大${p.largestArea}</strong></div>`).join('')}</div><div class="inline-actions"><button class="btn btn-primary" type="button" data-rematch>同じメンバーで再戦</button></div>`;
  root.querySelector('[data-rematch]').addEventListener('click', () => {
    state = newState(state.players.map((p) => p.name));
    if (networkSession) {
      const server = networkSession.server;
      state.mode = 'network';
      state.room = networkSession.room;
      state.server = server;
      state.playerName = networkSession.name;
    }
    persist();
    if (networkSession) sendNetwork({ type: 'state', state });
    render();
  });
}

document.querySelector('[data-reset]').addEventListener('click', () => {
  if (!window.confirm('深淵侵蝕の進行記録を削除しますか？')) return;
  closeNetwork();
  clearState(GAME_ID);
  state = null;
  setup();
});

registerServiceWorker();
render();
