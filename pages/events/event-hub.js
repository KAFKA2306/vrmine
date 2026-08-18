const root = document.querySelector('[data-event-root]');
const eventSlug = document.body.dataset.eventSlug;

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function emit(endpoint, name, detail = {}) {
  if (!endpoint) return;
  const payload = JSON.stringify({ event: name, event_slug: eventSlug, at: new Date().toISOString(), ...detail });
  if (navigator.sendBeacon) {
    navigator.sendBeacon(endpoint, new Blob([payload], { type: 'application/json' }));
    return;
  }
  fetch(endpoint, { method: 'POST', headers: { 'content-type': 'application/json' }, body: payload, keepalive: true }).catch(() => {});
}

try {
  if (!eventSlug || !/^[a-z0-9-]+$/.test(eventSlug)) throw new Error('event slug is invalid');
  const config = await fetch('./config.json').then((response) => {
    if (!response.ok) throw new Error(`event config HTTP ${response.status}`);
    return response.json();
  });
  const event = config.event || {};
  const game = config.game || {};
  const endpoint = config.analytics?.endpoint || null;
  document.title = `${event.name || 'Event'} | VRMine`;
  root.innerHTML = `
    <section class="hero">
      <p class="eyebrow">Branded party game hub</p>
      <h1>${escapeHtml(event.name)}</h1>
      <p class="lead">${escapeHtml(event.organizer?.name)} のイベント用ブラウザHub。インストール不要で、その場でゲームを開始できます。</p>
      <div class="hero-actions">
        <a class="btn btn-primary" data-start href="../../games/answer-impostor/?event=${encodeURIComponent(eventSlug)}">Answer Impostorを開始</a>
        <a class="btn" href="../../3dgs/">3DGS showcaseを見る</a>
      </div>
    </section>
    <section class="section">
      <div class="feature-grid">
        <article class="feature-card"><h3>主催</h3><p><a href="${escapeHtml(event.organizer?.url)}">${escapeHtml(event.organizer?.name)}</a></p></article>
        <article class="feature-card"><h3>質問パック</h3><p><code>${escapeHtml(game.question_pack)}</code></p></article>
        <article class="feature-card"><h3>参加方法</h3><p>このURLを開き、同じ端末を4〜8人で回して遊びます。</p></article>
      </div>
      <p style="margin-top:1.5rem"><a class="btn" data-cta href="${escapeHtml(event.cta?.url)}">${escapeHtml(event.cta?.label)}</a></p>
    </section>`;
  emit(endpoint, 'view_hub');
  root.querySelector('[data-cta]')?.addEventListener('click', () => emit(endpoint, 'cta_click'));
} catch (error) {
  root.innerHTML = `<section class="hero"><h1>Event configuration error</h1><p class="lead">${escapeHtml(error.message)}</p></section>`;
}
