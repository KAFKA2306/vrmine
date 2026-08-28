import { trackEvent } from '../../events/telemetry.js';

const params = new URLSearchParams(location.search);
const eventSlug = params.get('event');
const packId = params.get('pack');

async function fetchRequiredJson(url, label) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${label} HTTP ${response.status}`);
  const value = await response.json();
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new Error(`${label} must be a JSON object`);
  }
  return value;
}

if (eventSlug || packId) {
  const eventConfig = eventSlug
    ? await fetchRequiredJson(`../../events/${encodeURIComponent(eventSlug)}/config.json`, 'event config')
    : null;
  const resolvedPack = packId || eventConfig?.question_pack;
  if (!resolvedPack) throw new Error('question pack is required for event mode');

  const pack = await fetchRequiredJson(
    `../../events/question-packs/${encodeURIComponent(resolvedPack)}.json`,
    'question pack'
  );
  if (!Array.isArray(pack.questions) || pack.questions.length === 0) {
    throw new Error(`question pack ${resolvedPack} has no questions`);
  }

  const applyPack = () => {
    const textarea = document.querySelector('textarea[name="customQuestions"]');
    if (!textarea || textarea.dataset.eventPackApplied) return;
    textarea.value = pack.questions.join('\n');
    textarea.dataset.eventPackApplied = 'true';
    const help = textarea.parentElement?.querySelector('.help');
    if (help) help.textContent = `イベント質問パック「${pack.id ?? resolvedPack}」を追加済み。必要なら開始前に編集できます。`;
  };

  let completed = false;
  const observe = () => {
    applyPack();
    if (!completed && eventConfig && document.querySelector('[data-screen-title]')?.textContent === '最終結果') {
      completed = true;
      void trackEvent(eventSlug, 'complete_game', eventConfig.analytics_endpoint);
    }
  };
  observe();

  const gameRoot = document.querySelector('[data-game-root]');
  if (!gameRoot) throw new Error('Answer Impostor game root is missing');
  new MutationObserver(observe).observe(gameRoot, { childList: true, subtree: true });
}
