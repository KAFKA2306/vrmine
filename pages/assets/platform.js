const PREFIX = 'vrmine.games.';

export function storageKey(gameId) {
  return `${PREFIX}${gameId}.state`;
}

export function saveState(gameId, state) {
  localStorage.setItem(storageKey(gameId), JSON.stringify(state));
  window.dispatchEvent(new CustomEvent('vrmine:state-saved', { detail: { gameId } }));
}

export function loadState(gameId) {
  try {
    const raw = localStorage.getItem(storageKey(gameId));
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function clearState(gameId) {
  localStorage.removeItem(storageKey(gameId));
}

export function downloadJson(filename, data) {
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

export function readJsonFile(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      try { resolve(JSON.parse(String(reader.result))); }
      catch { reject(new Error('JSONファイルを読み込めませんでした。')); }
    };
    reader.onerror = () => reject(new Error('ファイルを読み込めませんでした。'));
    reader.readAsText(file);
  });
}

export function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

export function showToast(message, timeout = 2600) {
  let toast = document.querySelector('[data-toast]');
  if (!toast) {
    toast = document.createElement('div');
    toast.dataset.toast = '';
    toast.className = 'toast';
    toast.setAttribute('role', 'status');
    document.body.append(toast);
  }
  toast.textContent = message;
  toast.classList.remove('hidden');
  window.clearTimeout(showToast.timer);
  showToast.timer = window.setTimeout(() => toast.classList.add('hidden'), timeout);
}

export function registerServiceWorker() {
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
      navigator.serviceWorker.register(new URL('../service-worker.js', import.meta.url)).catch(() => {});
    });
  }
}
