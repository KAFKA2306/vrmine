import { trackEvent } from '../../events/telemetry.js';

const params = new URLSearchParams(location.search);
const eventSlug = params.get('event');
const packId = params.get('pack');

if (eventSlug || packId) {
  const eventConfig = eventSlug
    ? await fetch(`../../events/${encodeURIComponent(eventSlug)}/config.json`).then((response) => response.ok ? response.json() : null)
    : null;
  const resolvedPack = packId || eventConfig?.question_pack;
  const pack = resolvedPack
    ? await fetch(`../../events/question-packs/${encodeURIComponent(resolvedPack)}.json`).then((response) => response.ok ? response.json() : null)
    : null;

  const applyPack = () => {
    if (!pack?.questions?.length) return;
    const textarea = document.querySelector('textarea[name="customQuestions"]');
    if (!textarea || textarea.dataset.eventPackApplied) return;
    textarea.value = pack.questions.join('\n');
    textarea.dataset.eventPackApplied = 'true';
    const help = textarea.parentElement?.querySelector('.help');
    if (help) help.textContent = `イベント質問パック「${pack.id}」を追加済み。必要なら開始前に編集できます。`;
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
  new MutationObserver(observe).observe(document.querySelector('[data-game-root]'), { childList: true, subtree: true });
}
