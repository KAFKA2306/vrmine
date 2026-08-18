const ALLOWED_EVENTS = new Set(['view_hub', 'start_game', 'complete_game', 'cta_click']);

function storageKey(slug) {
  return `vrmine.event.${slug}.counts`;
}

export function loadEventCounts(slug) {
  try {
    const value = JSON.parse(localStorage.getItem(storageKey(slug)) ?? '{}');
    return Object.fromEntries([...ALLOWED_EVENTS].map((name) => [name, Number(value[name] ?? 0)]));
  } catch {
    return Object.fromEntries([...ALLOWED_EVENTS].map((name) => [name, 0]));
  }
}

export async function trackEvent(slug, name, endpoint = null) {
  if (!ALLOWED_EVENTS.has(name)) throw new Error(`unsupported event: ${name}`);
  const counts = loadEventCounts(slug);
  counts[name] += 1;
  localStorage.setItem(storageKey(slug), JSON.stringify(counts));
  if (!endpoint) return;
  const response = await fetch(endpoint, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ event_slug: slug, event: name })
  });
  if (!response.ok) throw new Error(`analytics HTTP ${response.status}`);
}

export function downloadEventCounts(slug) {
  const blob = new Blob([JSON.stringify({ event_slug: slug, counts: loadEventCounts(slug) }, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${slug}-event-counts.json`;
  link.click();
  URL.revokeObjectURL(url);
}
