const ALLOWED_EVENTS = new Set(['view_hub', 'start_game', 'complete_game', 'cta_click']);
const config = await fetch('./config.json').then((response) => {
  if (!response.ok) throw new Error(`event config HTTP ${response.status}`);
  return response.json();
});

const storageKey = `vrmine.event.${config.event_slug}.counts`;

function loadCounts() {
  try {
    const value = JSON.parse(localStorage.getItem(storageKey) ?? '{}');
    return Object.fromEntries([...ALLOWED_EVENTS].map((name) => [name, Number(value[name] ?? 0)]));
  } catch {
    return Object.fromEntries([...ALLOWED_EVENTS].map((name) => [name, 0]));
  }
}

async function track(name) {
  if (!ALLOWED_EVENTS.has(name)) throw new Error(`unsupported event: ${name}`);
  const counts = loadCounts();
  counts[name] += 1;
  localStorage.setItem(storageKey, JSON.stringify(counts));
  if (!config.analytics_endpoint) return;
  const response = await fetch(config.analytics_endpoint, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ event_slug: config.event_slug, event: name })
  });
  if (!response.ok) throw new Error(`analytics HTTP ${response.status}`);
}

function downloadCounts() {
  const blob = new Blob([JSON.stringify({ event_slug: config.event_slug, counts: loadCounts() }, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${config.event_slug}-event-counts.json`;
  link.click();
  URL.revokeObjectURL(url);
}

document.querySelector('[data-event-name]').textContent = config.event_name;
document.querySelector('[data-organizer]').textContent = `主催: ${config.organizer_name}`;
const cta = document.querySelector('[data-event-cta]');
cta.textContent = config.cta_label;
cta.href = config.cta_url;

document.querySelector('[data-start-game]').addEventListener('click', () => { void track('start_game'); });
cta.addEventListener('click', () => { void track('cta_click'); });
document.querySelector('[data-export-events]').addEventListener('click', downloadCounts);

await track('view_hub');
