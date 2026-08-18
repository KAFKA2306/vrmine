import { downloadEventCounts, trackEvent } from './telemetry.js';

const config = await fetch('./config.json').then((response) => {
  if (!response.ok) throw new Error(`event config HTTP ${response.status}`);
  return response.json();
});

document.querySelector('[data-event-name]').textContent = config.event_name;
document.querySelector('[data-organizer]').textContent = `主催: ${config.organizer_name}`;
const cta = document.querySelector('[data-event-cta]');
cta.textContent = config.cta_label;
cta.href = config.cta_url;

document.querySelector('[data-start-game]').addEventListener('click', () => {
  void trackEvent(config.event_slug, 'start_game', config.analytics_endpoint);
});
cta.addEventListener('click', () => {
  void trackEvent(config.event_slug, 'cta_click', config.analytics_endpoint);
});
document.querySelector('[data-export-events]').addEventListener('click', () => downloadEventCounts(config.event_slug));

await trackEvent(config.event_slug, 'view_hub', config.analytics_endpoint);
