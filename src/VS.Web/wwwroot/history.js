const dateInput = document.querySelector('#historyDate');
const statusNode = document.querySelector('#historyStatus');
const matchesNode = document.querySelector('#historyMatches');
const today = new Date();
dateInput.value = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;

const esc = value => String(value ?? '').replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]));

function render(matches) {
  matchesNode.innerHTML = matches.map(snapshot => `<article class="panel history-card">
    <div class="history-card-top"><span class="history-status">${esc(String(snapshot.status).replace(/([a-z])([A-Z])/g,'$1 $2'))}</span><time>${new Date(snapshot.plannedKickoff).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</time></div>
    <div class="history-team-row"><img src="/api/soccer/team-logo?name=${encodeURIComponent(snapshot.awayTeam)}" alt="" onerror="this.hidden=true"><strong>${esc(snapshot.awayTeam)}</strong><b>${snapshot.awayScore}</b></div>
    <div class="history-team-row"><img src="/api/soccer/team-logo?name=${encodeURIComponent(snapshot.homeTeam)}" alt="" onerror="this.hidden=true"><strong>${esc(snapshot.homeTeam)}</strong><b>${snapshot.homeScore}</b></div>
    <div class="history-card-footer"><span>${esc(snapshot.competition)}</span><a class="button-link" href="/soccer-matchcenter.html?matchId=${encodeURIComponent(snapshot.matchId)}&history=1">Open MatchCenter</a></div>
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
document.querySelector('#exportHistoryJson').addEventListener('click',()=>{location.href=`/api/soccer/history-export?date=${encodeURIComponent(dateInput.value)}&format=json`;});
document.querySelector('#exportHistoryXml').addEventListener('click',()=>{location.href=`/api/soccer/history-export?date=${encodeURIComponent(dateInput.value)}&format=xml`;});
document.querySelector('#importHistory').addEventListener('click',async()=>{const file=document.querySelector('#historyImportFile').files[0];if(!file){statusNode.textContent='Choose a VITEC Soccer history JSON file first.';return;}const form=new FormData();form.append('history',file);statusNode.textContent='Importing historical MLS snapshots…';try{const response=await fetch('/api/soccer/history-import',{method:'POST',body:form}),data=await response.json().catch(()=>({}));if(!response.ok)throw new Error(data.message||`HTTP ${response.status}`);statusNode.textContent=data.message;document.querySelector('#historyImportFile').value='';await loadHistory();}catch(error){showError(error);}});
dateInput.addEventListener('change', () => loadHistory().catch(showError));
loadHistory().catch(showError);
