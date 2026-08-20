const dateInput = document.querySelector('#historyDate');
const statusNode = document.querySelector('#historyStatus');
const matchesNode = document.querySelector('#historyMatches');
const today = new Date();
dateInput.value = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;

const esc = value => String(value ?? '').replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]));

function render(matches) {
  matchesNode.innerHTML = matches.map(snapshot => `<article class="match-card history-card">
    <div class="match-meta"><strong>${esc(snapshot.status)}</strong><span>${new Date(snapshot.plannedKickoff).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</span></div>
    <div class="history-score"><span>${esc(snapshot.awayTeam)}</span><strong>${snapshot.awayScore} – ${snapshot.homeScore}</strong><span>${esc(snapshot.homeTeam)}</span></div>
    <div class="match-footer"><span>${esc(snapshot.competition)}</span><a href="/soccer-matchcenter.html?matchId=${encodeURIComponent(snapshot.matchId)}&history=1">Open Stored MatchCenter</a></div>
  </article>`).join('');
}

async function loadHistory() {
  statusNode.textContent = 'Loading stored MLS matches…';
  const response = await fetch(`/api/soccer/history?date=${encodeURIComponent(dateInput.value)}&limit=100`, { cache: 'no-store' });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.message || `HTTP ${response.status}`);
  const matches = data.matches || [];
  render(matches);
  statusNode.textContent = matches.length ? `${matches.length} stored MLS match${matches.length === 1 ? '' : 'es'}.` : 'No stored MLS matches for this date.';
}

async function captureDate() {
  const button = document.querySelector('#captureDate');
  button.disabled = true;
  statusNode.textContent = 'Capturing MLS MatchCenter snapshots…';
  try {
    const response = await fetch(`/api/soccer/history/capture-date?date=${encodeURIComponent(dateInput.value)}`, { method: 'POST' });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.message || `HTTP ${response.status}`);
    statusNode.textContent = `Saved ${data.saved} of ${data.matches} MLS matches.`;
    await loadHistory();
  } finally { button.disabled = false; }
}

function showError(error) {
  matchesNode.innerHTML = '';
  statusNode.textContent = `MLS history unavailable: ${error.message}`;
}

document.querySelector('#loadHistory').addEventListener('click', () => loadHistory().catch(showError));
document.querySelector('#captureDate').addEventListener('click', () => captureDate().catch(showError));
dateInput.addEventListener('change', () => loadHistory().catch(showError));
loadHistory().catch(showError);
