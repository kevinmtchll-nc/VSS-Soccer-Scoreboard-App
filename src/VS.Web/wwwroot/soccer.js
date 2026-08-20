const matches = document.querySelector('#matches');
const dateInput = document.querySelector('#matchDate');
const refresh = document.querySelector('#refresh');
const matchCount = document.querySelector('#matchCount');
const updated = document.querySelector('#updated');
const summaryUpdated = document.querySelector('#summaryUpdated');

const localDate = new Date();
dateInput.value = `${localDate.getFullYear()}-${String(localDate.getMonth()+1).padStart(2,'0')}-${String(localDate.getDate()).padStart(2,'0')}`;

const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const statusText = status => ({scheduled:'Scheduled',warmup:'Warmup',firstHalf:'First Half',halfTime:'Halftime',secondHalf:'Second Half',finalWhistle:'Final',postponed:'Postponed'}[status] || status || 'Scheduled');

function teamRow(team) {
  const logo = `/api/soccer/team-logo?name=${encodeURIComponent(team.name)}&code=${encodeURIComponent(team.code || '')}`;
  return `<div class="team-row"><img class="team-logo" src="${logo}" alt="" onerror="this.hidden=true"><div class="team-name-wrap"><strong>${esc(team.name)}</strong><span class="muted">${esc(team.code)}</span></div><strong class="team-score">${team.score ?? 0}</strong></div>`;
}

function renderLeaders(targetId, leaders, decimals = 0) {
  const target = document.querySelector(targetId);
  target.innerHTML = leaders.length ? leaders.slice(0, 5).map(leader => `<div class="leader-row"><strong>${esc(leader.playerName)}<small>${esc(leader.teamName === leader.playerName ? '' : leader.teamName)}</small></strong><span>${Number(leader.value).toFixed(decimals)}</span></div>`).join('') : '<p class="muted">No results yet.</p>';
}

async function loadSummary() {
  try {
    const response = await fetch(`/api/soccer/daily-summary?date=${encodeURIComponent(dateInput.value)}`, {cache:'no-store'});
    if (!response.ok) throw new Error(`Request failed (${response.status})`);
    const summary = await response.json();
    const category = name => summary.leaders.filter(leader => leader.category === name);
    renderLeaders('#goalLeaders', category('Goals'));
    renderLeaders('#shotLeaders', category('Shots'));
    renderLeaders('#xgLeaders', category('Expected Goals'), 2);
    const alerts = document.querySelector('#matchAlerts');
    alerts.innerHTML = summary.alerts.length ? summary.alerts.slice(-8).reverse().map(alert => `<a class="alert-row ${esc(alert.kind)}" href="/soccer-matchcenter.html?matchId=${encodeURIComponent(alert.matchId)}&date=${encodeURIComponent(dateInput.value)}"><strong>${esc(alert.minute ? `${alert.minute}'` : alert.kind.toUpperCase())}</strong><span>${esc(alert.text)}</span></a>`).join('') : '<p class="muted">No match alerts yet.</p>';
    summaryUpdated.textContent = `Updated ${new Date(summary.updatedAt).toLocaleTimeString()}`;
  } catch (error) {
    summaryUpdated.textContent = `Leaders unavailable: ${error.message}`;
  }
}

async function load() {
  matches.innerHTML = '<article class="panel">Loading MLS matches…</article>';
  try {
    const response = await fetch(`/api/soccer/matches?date=${encodeURIComponent(dateInput.value)}`, {cache:'no-store'});
    if (!response.ok) throw new Error(`Request failed (${response.status})`);
    const data = await response.json();
    matchCount.textContent = `${data.length} MLS ${data.length === 1 ? 'match' : 'matches'}`;
    updated.textContent = `Updated ${new Date().toLocaleTimeString()}`;
    matches.innerHTML = data.length ? data.map(game => `<article class="game-card">
      <div class="game-card-top"><strong>${esc(statusText(game.status))}${game.minute ? ` · ${esc(game.minute)}'` : ''}</strong><span class="muted">${esc(game.stadium)}</span></div>
      ${teamRow(game.away)}${teamRow(game.home)}
      <div class="game-context">Matchweek ${game.matchDay} · ${new Date(game.plannedKickoff).toLocaleTimeString([], {hour:'numeric',minute:'2-digit'})}</div>
      <a class="game-center-link" href="/soccer-matchcenter.html?matchId=${encodeURIComponent(game.matchId)}&date=${encodeURIComponent(dateInput.value)}">Open MatchCenter</a>
    </article>`).join('') : '<article class="panel">No MLS matches are scheduled for this date.</article>';
  } catch (error) {
    matches.innerHTML = `<article class="panel error">Unable to load MLS matches: ${esc(error.message)}</article>`;
  }
  await loadSummary();
}

refresh.addEventListener('click', load);
dateInput.addEventListener('change', load);
load();
setInterval(load, 30000);
