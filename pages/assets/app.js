import { games } from '../games/registry.js';
import { escapeHtml, registerServiceWorker } from './platform.js';

const grid = document.querySelector('[data-game-grid]');
if (grid) {
  grid.innerHTML = games.map((game) => `
    <article class="game-card" data-tone="${escapeHtml(game.tone)}">
      <div>
        <div class="badges">
          <span class="badge">${escapeHtml(game.status)}</span>
          <span class="badge">${escapeHtml(game.players)}</span>
          <span class="badge">${escapeHtml(game.duration)}</span>
        </div>
        <p class="eyebrow">${escapeHtml(game.subtitle)}</p>
        <h2>${escapeHtml(game.title)}</h2>
        <p>${escapeHtml(game.description)}</p>
      </div>
      <div>
        <div class="badges">${game.tags.map((tag) => `<span class="badge">${escapeHtml(tag)}</span>`).join('')}</div>
        <div class="card-actions"><a class="btn btn-primary" href="${game.href}">ゲームを開く</a></div>
      </div>
    </article>
  `).join('');
}

document.querySelector('[data-game-count]')?.replaceChildren(String(games.length));
registerServiceWorker();
