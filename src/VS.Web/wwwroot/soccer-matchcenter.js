const query = new URLSearchParams(location.search);
const id = query.get('matchId');
const matchDate = query.get('date') || '';
const useHistory = query.get('history') === '1';

if (useHistory) {
  document.querySelector('.app-header p').textContent = 'Stored PostgreSQL MatchCenter snapshot';
  const backLink = document.querySelector('.header-actions a');
  backLink.href = '/history.html';
  backLink.textContent = '← History';
}

const esc = value => String(value ?? '').replace(/[&<>"']/g, character => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
}[character]));
const setHtmlIfChanged = (element, html) => { if (element.innerHTML !== html) element.innerHTML = html; };

const playerList = side => `<h3>${esc(side.team.name)} · ${esc(side.formation)}</h3><div class="soccer-table-scroll"><table class="soccer-data-table lineup-table"><colgroup><col class="number-col"><col class="player-col"><col class="position-col"></colgroup><thead><tr><th>#</th><th>Player</th><th>Position</th></tr></thead><tbody>${side.players.filter(player => player.isStarter).map(player => `<tr><td>${player.shirtNumber ?? ''}</td><td>${esc(player.firstName)} ${esc(player.lastName)}${player.isCaptain ? ' (C)' : ''}</td><td>${esc(player.position)}</td></tr>`).join('')}</tbody></table></div>`;

const shotMapView = document.querySelector('#shot-map-view');
const shotMapStyle = document.querySelector('#shot-map-style');
const shotMapViewKey = 'vitec-soccer-shot-map-view-v1';
const shotMapStyleKey = 'vitec-soccer-shot-map-style-v1';
let currentMatchCenter = null;
shotMapView.value = localStorage.getItem(shotMapViewKey) || 'full';
shotMapStyle.value = localStorage.getItem(shotMapStyleKey) || 'broadcast';

function pitchLines(style) {
  const themes={broadcast:{base:'#157a43',alt:'#116b3a',line:'#f2fff5'},classic:{base:'#12643a',alt:'#12643a',line:'#d9f2df'},tactical:{base:'#111923',alt:'#0c131c',line:'#61c9ff'},blueprint:{base:'#153760',alt:'#102e51',line:'#a8ddff'},light:{base:'#dce8d3',alt:'#cbdcc2',line:'#243a2a'}},theme=themes[style]||themes.broadcast;
  const stripes=theme.base===theme.alt?'':`<path d="M0 0H17.5V68H0zM35 0h17.5v68H35zM70 0h17.5v68H70z" fill="${theme.alt}" opacity=".72"/>`;
  return `<rect x="0" y="0" width="105" height="68" rx="1" fill="${theme.base}" stroke="${theme.line}" stroke-width=".6"/>${stripes}<path d="M52.5 0V68 M0 13.84H16.5V54.16H0 M105 13.84H88.5V54.16H105 M0 24.84H5.5V43.16H0 M105 24.84H99.5V43.16H105" fill="none" stroke="${theme.line}" stroke-width=".55"/><circle cx="52.5" cy="34" r="9.15" fill="none" stroke="${theme.line}" stroke-width=".55"/><circle cx="52.5" cy="34" r=".7" fill="${theme.line}"/><circle cx="11" cy="34" r=".55" fill="${theme.line}"/><circle cx="94" cy="34" r=".55" fill="${theme.line}"/>`;
}

function shotMap(matchCenter, view = 'full', style = 'broadcast') {
  const shots=matchCenter.events.filter(event=>(event.type==='shot_at_goals'||event.subType==='goals')&&Number.isFinite(event.x)&&Number.isFinite(event.y));
  const mark = (event, offset=0, normalize=false) => {let x=Math.max(0,Math.min(105,Number(event.x))),y=Math.max(0,Math.min(68,Number(event.y)));const goal=event.subType==='goals',home=event.teamId===matchCenter.match.home.teamId;if(normalize&&x<52.5){x=105-x;y=68-y;}x+=offset;const xg=Number(event.expectedGoals||0),r=view==='heat'?Math.max(3.8,6+xg*12):view==='xg'?Math.max(1.5,2+xg*9):goal?2.2:Math.max(1.15,Math.min(2,1.1+xg*2.2)),color=home?'#ffc857':'#66bfff',opacity=view==='heat'?'.22':'.9';return `<circle cx="${x}" cy="${y}" r="${r}" fill="${color}" stroke="${goal?'#fff':view==='heat'?'none':'#08111d'}" stroke-width="${goal?'.8':'.35'}" opacity="${opacity}"><title>${esc(event.playerName||'Unknown player')} · ${esc(event.description)} · ${esc(event.minute)}'${event.expectedGoals==null?'':` · xG ${Number(event.expectedGoals).toFixed(2)}`}</title></circle>`;};
  let svg;
  if(view==='teams') {const away=shots.filter(e=>e.teamId===matchCenter.match.away.teamId).map(e=>mark(e,0,true)).join(''),home=shots.filter(e=>e.teamId===matchCenter.match.home.teamId).map(e=>mark(e,112,true)).join('');svg=`<svg class="soccer-shot-map team-comparison" viewBox="-3 -8 223 80" role="img" aria-label="Separate normalized shot pitches for each team"><g>${pitchLines(style)}${away}<text x="52.5" y="-2" text-anchor="middle" fill="#d9f2df" font-size="3.2">${esc(matchCenter.match.away.name)}</text></g><g transform="translate(112 0)">${pitchLines(style)}<text x="52.5" y="-2" text-anchor="middle" fill="#d9f2df" font-size="3.2">${esc(matchCenter.match.home.name)}</text></g>${home}</svg>`;} else {const visible=view==='attacking'?shots.filter(e=>Number(e.x)<=35||Number(e.x)>=70):shots;const overlay=view==='attacking'?'<rect x="35" y="0" width="35" height="68" fill="#020a06" opacity=".48"/><text x="52.5" y="35" text-anchor="middle" fill="#d9f2df" opacity=".72" font-size="3">ATTACKING ENDS</text>':'';svg=`<svg class="soccer-shot-map" viewBox="-3 -4 111 76" role="img" aria-label="Overhead pitch showing shot locations">${pitchLines(style)}${overlay}${visible.map(e=>mark(e)).join('')}</svg>`;}
  const labels={full:'Full Pitch',attacking:'Attacking Ends',teams:'Team Comparison',heat:'Shot Density',xg:'xG Bubbles'};
  return `<div class="shot-map-legend"><span><i class="away-shot"></i>${esc(matchCenter.match.away.name)}</span><span><i class="home-shot"></i>${esc(matchCenter.match.home.name)}</span><span><i class="goal-shot"></i>Goal</span><strong>${esc(labels[view]||labels.full)} · ${shots.length} located shots</strong></div>${svg}`;
}

const redrawShotMap=()=>{if(currentMatchCenter)setHtmlIfChanged(document.querySelector('#shot-map'),shotMap(currentMatchCenter,shotMapView.value,shotMapStyle.value));};
shotMapView.addEventListener('change',()=>{localStorage.setItem(shotMapViewKey,shotMapView.value);redrawShotMap();});
shotMapStyle.addEventListener('change',()=>{localStorage.setItem(shotMapStyleKey,shotMapStyle.value);redrawShotMap();});

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
  const endpoint = useHistory ? `/api/soccer/history/${encodeURIComponent(id)}` : `/api/soccer/matches/${encodeURIComponent(id)}/matchcenter${dateQuery}`;
  const response = await fetch(endpoint, { cache: 'no-store' });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);

  const matchCenter = await response.json();
  currentMatchCenter = matchCenter;
  const match = matchCenter.match;
  setHtmlIfChanged(document.querySelector('#score'), `<div class="gc-team away">${teamMark(match.away)}</div><div class="gc-score-center"><div class="gc-score-num">${match.away.score} – ${match.home.score}</div><div class="gc-status">${matchStatus(match)}</div></div><div class="gc-team home">${teamMark(match.home)}</div>`);
  setHtmlIfChanged(document.querySelector('#context'), `<span>${esc(match.competition)}</span><span>${esc(match.stadium)}</span><span>Matchweek ${match.matchDay}</span>`);
  setHtmlIfChanged(document.querySelector('#away'), playerList(matchCenter.away));
  setHtmlIfChanged(document.querySelector('#home'), playerList(matchCenter.home));
  setHtmlIfChanged(document.querySelector('#timeline'), matchCenter.events.slice(0, 20).map(event => `<div class="pitch-item"><strong>${esc(event.minute)}' · ${esc(event.description)}</strong><span class="muted">${esc(event.teamName)}</span></div>`).join(''));
  setHtmlIfChanged(document.querySelector('#stats'), `<div class="soccer-table-scroll"><table class="soccer-data-table team-statistics-table"><thead><tr><th>Team</th><th>Possession</th><th>Shots</th><th>On Target</th><th>xG</th><th>Corners</th><th>Fouls</th><th>Cards</th></tr></thead><tbody>${matchCenter.teamStatistics.map(stat => `<tr><td>${esc(stat.teamName)}</td><td>${stat.possession.toFixed(1)}%</td><td>${stat.shots}</td><td>${stat.shotsOnTarget}</td><td>${stat.expectedGoals.toFixed(2)}</td><td>${stat.corners}</td><td>${stat.fouls}</td><td>${stat.yellowCards}Y ${stat.redCards}R</td></tr>`).join('')}</tbody></table></div>`);
  setHtmlIfChanged(document.querySelector('#shot-map'), shotMap(matchCenter, shotMapView.value, shotMapStyle.value));
}

function showError(error) {
  document.querySelector('#score').innerHTML = `<div class="panel error">Unable to load this MLS MatchCenter: ${esc(error.message)}</div>`;
}

document.querySelector('#refresh').addEventListener('click', () => load().catch(showError));
load().catch(showError);
setInterval(() => load().catch(showError), 30000);
