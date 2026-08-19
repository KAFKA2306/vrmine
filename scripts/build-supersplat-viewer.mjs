import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { html, css, js } from '@playcanvas/supersplat-viewer';

const outputDir = resolve(process.argv[2] ?? '_site/3dgs/viewer');
await mkdir(outputDir, { recursive: true });

const bridge = `<script>
  (() => {
    const params = new URL(location.href).searchParams;
    const id = params.get('vrmine_id');
    const token = Number(params.get('vrmine_token'));
    const root = document.documentElement;
    root.dataset.vrmineBridge = 'ready';
    const post = (type, message) => parent.postMessage({ type, id, token, message }, location.origin);
    const fail = (message) => {
      root.dataset.vrmineError = String(message || 'viewer error').slice(0, 500);
      post('vrmine:3dgs:error', message || 'viewer error');
    };
    window.firstFrame = () => {
      root.dataset.vrmineFirstFrame = 'pass';
      if (id) root.dataset.vrmineSourceId = id;
      post('vrmine:3dgs:first-frame');
    };
    window.addEventListener('DOMContentLoaded', () => { root.dataset.vrmineDom = 'ready'; }, { once: true });
    window.addEventListener('error', (event) => fail(event.message || 'viewer error'));
    window.addEventListener('unhandledrejection', (event) => fail(String(event.reason || 'viewer promise rejection')));
  })();
</script>`;

const document = html.replace('</head>', `${bridge}</head>`);
await Promise.all([
  writeFile(resolve(outputDir, 'index.html'), document, 'utf8'),
  writeFile(resolve(outputDir, 'index.css'), css, 'utf8'),
  writeFile(resolve(outputDir, 'index.js'), js, 'utf8'),
]);

console.log(`Materialized @playcanvas/supersplat-viewer into ${outputDir}`);
