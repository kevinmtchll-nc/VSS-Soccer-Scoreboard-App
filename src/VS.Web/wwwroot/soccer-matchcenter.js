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
