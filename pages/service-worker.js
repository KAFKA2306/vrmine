const CACHE = 'vrmine-game-hub-v2';
const scope = self.registration.scope;
const assets = [
  './',
  './index.html',
  './manifest.webmanifest',
  './assets/styles.css',
  './assets/app.js',
  './assets/platform.js',
  './games/registry.js',
  './games/stich-meister/',
  './games/stich-meister/index.html',
  './games/answer-impostor/',
  './games/answer-impostor/index.html',
  './games/answer-impostor/game.js',
  './games/answer-impostor/engine.mjs',
  './games/abyss-invasion/',
  './games/abyss-invasion/index.html',
  './games/abyss-invasion/tracker.js'
].map((path) => new URL(path, scope).href);

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll(assets)).then(() => self.skipWaiting()));
});
self.addEventListener('activate', (event) => {
  event.waitUntil(caches.keys().then((keys) => Promise.all(keys.filter((key) => key !== CACHE).map((key) => caches.delete(key)))).then(() => self.clients.claim()));
});
self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET' || new URL(event.request.url).origin !== self.location.origin) return;
  event.respondWith(caches.match(event.request).then((cached) => cached || fetch(event.request).then((response) => {
    const copy = response.clone();
    caches.open(CACHE).then((cache) => cache.put(event.request, copy));
    return response;
  }).catch(() => caches.match(new URL('./index.html', scope).href))));
});
