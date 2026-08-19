const query = new URLSearchParams(location.search);
const id = query.get('matchId');
const matchDate = query.get('date') || '';

const esc = value => String(value ?? '').replace(/[&<>"']/g, character => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
}[character]));

const playerList = side => `<h3>${esc(side.team.name)} · ${esc(side.formation)}</h3><table><thead><tr><th>#</th><th>Player</th><th>Position</th></tr></thead><tbody>${side.players.filter(player => player.isStarter).map(player => `<tr><td>${player.shirtNumber ?? ''}</td><td>${esc(player.firstName)} ${esc(player.lastName)}${player.isCaptain ? ' (C)' : ''}</td><td>${esc(player.position)}</td></tr>`).join('')}</tbody></table>`;

const teamMark = team => `<img class="gc-team-logo" src="/api/soccer/team-logo?name=${encodeURIComponent(team.name)}&code=${encodeURIComponent(team.code || '')}" alt="" onerror="this.hidden=true"><strong>${esc(team.name)}</strong>`;

const matchStatus = match => {
  const minute = String(match.minute ?? '').trim();
  return minute ? `${esc(match.status)} · ${esc(minute)}'` : esc(match.status);
};

const layout = document.querySelector('#matchcenter-layout');
const layoutKey = 'vitec-soccer-matchcenter-layout-v1';
let draggedPanel = null;

function saveLayout() {
  const order = [...layout.querySelectorAll('[data-panel-id]')].map(panel => panel.dataset.panelId);
  localStorage.setItem(layoutKey, JSON.stringify(order));
}

function restoreLayout() {
  try {
    const order = JSON.parse(localStorage.getItem(layoutKey) || '[]');
    for (const panelId of order) {
      const panel = layout.querySelector(`[data-panel-id="${CSS.escape(panelId)}"]`);
      if (panel) layout.append(panel);
    }
  } catch {
    localStorage.removeItem(layoutKey);
  }
}

for (const panel of layout.querySelectorAll('.draggable-panel')) {
  panel.draggable = false;
  const heading = panel.querySelector('h2');
  heading.addEventListener('pointerdown', event => {
    if (event.button !== 0) return;
    draggedPanel = panel;
    panel.classList.add('dragging');
    heading.setPointerCapture(event.pointerId);
    event.preventDefault();
  });
  heading.addEventListener('pointermove', event => {
    if (!draggedPanel || !heading.hasPointerCapture(event.pointerId)) return;
    const target = document.elementFromPoint(event.clientX, event.clientY)?.closest('.draggable-panel');
    if (!target || target === draggedPanel || target.parentElement !== layout) return;
    const bounds = target.getBoundingClientRect();
    const insertBefore = event.clientX < bounds.left + bounds.width / 2;
    layout.insertBefore(draggedPanel, insertBefore ? target : target.nextSibling);
  });
  const finishPointerDrag = event => {
    if (draggedPanel !== panel) return;
    if (heading.hasPointerCapture(event.pointerId)) heading.releasePointerCapture(event.pointerId);
    panel.classList.remove('dragging');
    draggedPanel = null;
    saveLayout();
  };
  heading.addEventListener('pointerup', finishPointerDrag);
  heading.addEventListener('pointercancel', finishPointerDrag);
  panel.addEventListener('dragstart', event => {
    draggedPanel = panel;
    panel.classList.add('dragging');
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', panel.dataset.panelId);
  });
  panel.addEventListener('dragover', event => {
    if (!draggedPanel || draggedPanel === panel) return;
    event.preventDefault();
    panel.classList.add('drag-over');
  });
  panel.addEventListener('dragleave', () => panel.classList.remove('drag-over'));
  panel.addEventListener('drop', event => {
    event.preventDefault();
    panel.classList.remove('drag-over');
    if (!draggedPanel || draggedPanel === panel) return;
    const bounds = panel.getBoundingClientRect();
    const insertBefore = event.clientX < bounds.left + bounds.width / 2;
    layout.insertBefore(draggedPanel, insertBefore ? panel : panel.nextSibling);
    saveLayout();
  });
  panel.addEventListener('dragend', () => {
    for (const item of layout.querySelectorAll('.draggable-panel')) item.classList.remove('dragging', 'drag-over');
    draggedPanel = null;
  });
}

document.querySelector('#reset-layout').addEventListener('click', () => {
  for (const panelId of ['away', 'timeline', 'home']) layout.append(layout.querySelector(`[data-panel-id="${panelId}"]`));
  localStorage.removeItem(layoutKey);
});

restoreLayout();

async function load() {
  if (!id) return;
  const dateQuery = matchDate ? `?date=${encodeURIComponent(matchDate)}` : '';
  const response = await fetch(`/api/soccer/matches/${encodeURIComponent(id)}/matchcenter${dateQuery}`, { cache: 'no-store' });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);

  const matchCenter = await response.json();
  const match = matchCenter.match;
  document.querySelector('#score').innerHTML = `<div class="gc-team away">${teamMark(match.away)}</div><div class="gc-score-center"><div class="gc-score-num">${match.away.score} – ${match.home.score}</div><div class="gc-status">${matchStatus(match)}</div></div><div class="gc-team home">${teamMark(match.home)}</div>`;
  document.querySelector('#context').innerHTML = `<span>${esc(match.competition)}</span><span>${esc(match.stadium)}</span><span>Matchweek ${match.matchDay}</span>`;
  document.querySelector('#away').innerHTML = playerList(matchCenter.away);
  document.querySelector('#home').innerHTML = playerList(matchCenter.home);
  document.querySelector('#timeline').innerHTML = matchCenter.events.slice(0, 20).map(event => `<div class="pitch-item"><strong>${esc(event.minute)}' · ${esc(event.description)}</strong><span class="muted">${esc(event.teamName)}</span></div>`).join('');
  document.querySelector('#stats').innerHTML = `<table><thead><tr><th>Team</th><th>Possession</th><th>Shots</th><th>On Target</th><th>xG</th><th>Corners</th><th>Fouls</th><th>Cards</th></tr></thead><tbody>${matchCenter.teamStatistics.map(stat => `<tr><td>${esc(stat.teamName)}</td><td>${stat.possession.toFixed(1)}%</td><td>${stat.shots}</td><td>${stat.shotsOnTarget}</td><td>${stat.expectedGoals.toFixed(2)}</td><td>${stat.corners}</td><td>${stat.fouls}</td><td>${stat.yellowCards}Y ${stat.redCards}R</td></tr>`).join('')}</tbody></table>`;
}

function showError(error) {
  document.querySelector('#score').innerHTML = `<div class="panel error">Unable to load this MLS MatchCenter: ${esc(error.message)}</div>`;
}

document.querySelector('#refresh').addEventListener('click', () => load().catch(showError));
load().catch(showError);
setInterval(() => load().catch(showError), 30000);
