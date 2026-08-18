import { DEFAULT_QUESTIONS } from '../games/answer-impostor/engine.mjs';

const params = new URLSearchParams(location.search);
const eventSlug = params.get('event');

function emit(endpoint, name, detail = {}) {
  if (!endpoint) return;
  const payload = JSON.stringify({ event: name, event_slug: eventSlug, at: new Date().toISOString(), ...detail });
  if (navigator.sendBeacon) {
    navigator.sendBeacon(endpoint, new Blob([payload], { type: 'application/json' }));
    return;
  }
  fetch(endpoint, { method: 'POST', headers: { 'content-type': 'application/json' }, body: payload, keepalive: true }).catch(() => {});
}

if (eventSlug && /^[a-z0-9-]+$/.test(eventSlug)) {
  try {
    const configUrl = new URL(`./${eventSlug}/config.json`, import.meta.url);
    const config = await fetch(configUrl).then((response) => {
      if (!response.ok) throw new Error(`event config HTTP ${response.status}`);
      return response.json();
    });
    const packId = config.game?.question_pack;
    if (packId && /^[a-z0-9-]+$/.test(packId)) {
      const packUrl = new URL(`./question-packs/${packId}.json`, import.meta.url);
      const pack = await fetch(packUrl).then((response) => {
        if (!response.ok) throw new Error(`question pack HTTP ${response.status}`);
        return response.json();
      });
      if (!Array.isArray(pack.questions) || pack.questions.length < 3) throw new Error('question pack is invalid');
      DEFAULT_QUESTIONS.splice(0, DEFAULT_QUESTIONS.length, ...pack.questions.map(String));
    }

    const gameTitle = document.querySelector('.game-hero h1');
    if (gameTitle && config.event?.name) gameTitle.insertAdjacentHTML('afterend', `<p class="help">${String(config.event.name).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')}</p>`);

    const endpoint = config.analytics?.endpoint || null;
    let started = sessionStorage.getItem(`vrmine.event.${eventSlug}.started`) === '1';
    let completed = sessionStorage.getItem(`vrmine.event.${eventSlug}.completed`) === '1';
    window.addEventListener('vrmine:state-saved', ({ detail }) => {
      if (detail?.gameId !== 'answer-impostor') return;
      const raw = localStorage.getItem('vrmine.games.answer-impostor.state');
      if (!raw) return;
      try {
        const state = JSON.parse(raw);
        if (!started && state.status && state.status !== 'setup') {
          started = true;
          sessionStorage.setItem(`vrmine.event.${eventSlug}.started`, '1');
          emit(endpoint, 'start_game', { game_id: 'answer-impostor' });
        }
        if (!completed && state.status === 'finished') {
          completed = true;
          sessionStorage.setItem(`vrmine.event.${eventSlug}.completed`, '1');
          emit(endpoint, 'complete_game', { game_id: 'answer-impostor' });
        }
      } catch {}
    });
  } catch (error) {
    console.error('Event context failed:', error);
  }
}

await import('../games/answer-impostor/game.js');
