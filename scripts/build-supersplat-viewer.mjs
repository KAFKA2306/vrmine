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
    const post = (type, message) => parent.postMessage({ type, id, token, message }, location.origin);
    window.firstFrame = () => post('vrmine:3dgs:first-frame');
    window.addEventListener('error', (event) => post('vrmine:3dgs:error', event.message || 'viewer error'));
    window.addEventListener('unhandledrejection', (event) => post('vrmine:3dgs:error', String(event.reason || 'viewer promise rejection')));
  })();
</script>`;

const document = html.replace('</head>', `${bridge}</head>`);
await Promise.all([
  writeFile(resolve(outputDir, 'index.html'), document, 'utf8'),
  writeFile(resolve(outputDir, 'index.css'), css, 'utf8'),
  writeFile(resolve(outputDir, 'index.js'), js, 'utf8'),
]);

console.log(`Materialized @playcanvas/supersplat-viewer into ${outputDir}`);
