const gamesEl = document.getElementById("games");
const statusText = document.getElementById("statusText");
const lastUpdated = document.getElementById("lastUpdated");
const datePicker = document.getElementById("datePicker");
const refreshButton = document.getElementById("refreshButton");
const template = document.getElementById("gameTemplate");

const cardsByGamePk = new Map();
let knownAlertKeys = null;

const today = new Date();
datePicker.value = [
  today.getFullYear(),
  String(today.getMonth() + 1).padStart(2, "0"),
  String(today.getDate()).padStart(2, "0")
].join("-");

function createGameCard(game) {
  const fragment = template.content.cloneNode(true);
  const card = fragment.querySelector(".game-card");
  card.dataset.gamePk = game.gamePk;

  const refs = {
    card,
    status: card.querySelector(".game-status"),
    venue: card.querySelector(".venue"),
    awayLogo: card.querySelector(".away-logo"),
    awayName: card.querySelector(".away .team-name"),
    awayRecord: card.querySelector(".away .record"),
    awayScore: card.querySelector(".away .score"),
    homeLogo: card.querySelector(".home-logo"),
    homeName: card.querySelector(".home .team-name"),
    homeRecord: card.querySelector(".home .record"),
    homeScore: card.querySelector(".home .score"),
    context: card.querySelector(".card-context"),
    link: card.querySelector(".gamecenter-button")
  };

  cardsByGamePk.set(String(game.gamePk), refs);
  gamesEl.appendChild(fragment);
  return refs;
}


function teamLogoUrl(teamId) {
  return `https://www.mlbstatic.com/team-logos/${teamId}.svg`;
}

function setImage(img, src, alt) {
  if (!img) return;
  img.alt = alt || "";
  if (img.dataset.src === src) return;

  img.dataset.src = src;
  img.hidden = false;
  img.onerror = () => { img.hidden = true; };
  img.onload = () => { img.hidden = false; };
  img.src = src;
}

function setText(el, value) {
  const next = String(value ?? "");
  if (el.textContent !== next) el.textContent = next;
}

function inningOrdinal(inning) {
  const value = Number(inning);
  if (!Number.isInteger(value) || value < 1) return "";
  const lastTwo = value % 100;
  if (lastTwo >= 11 && lastTwo <= 13) return `${value}th`;
  return `${value}${value % 10 === 1 ? "st" : value % 10 === 2 ? "nd" : value % 10 === 3 ? "rd" : "th"}`;
}

function gameStatusText(game) {
  const status = game.detailedStatus || game.status || "";
  const isLive = String(game.status || "").toLowerCase() === "live";
  if (!isLive || !game.currentInning || !game.inningState) return status;

  const ordinal = game.inningOrdinal || inningOrdinal(game.currentInning);
  return `${status} · ${game.inningState} ${ordinal}`;
}

function updateGameCard(game) {
  const key = String(game.gamePk);
  const refs = cardsByGamePk.get(key) || createGameCard(game);

  setText(refs.status, gameStatusText(game));
  setText(refs.venue, game.venue || "");
  setImage(refs.awayLogo, teamLogoUrl(game.away.teamId), `${game.away.name} logo`);
  setText(refs.awayName, game.away.name);
  setText(refs.awayRecord, `${game.away.wins}-${game.away.losses}`);
  setText(refs.awayScore, game.away.score);
  setImage(refs.homeLogo, teamLogoUrl(game.home.teamId), `${game.home.name} logo`);
  setText(refs.homeName, game.home.name);
  setText(refs.homeRecord, `${game.home.wins}-${game.home.losses}`);
  setText(refs.homeScore, game.home.score);

  const contextParts = [];
  if (game.seriesDescription) contextParts.push(game.seriesDescription);
  if (game.seriesGameNumber && game.gamesInSeries)
    contextParts.push(`Game ${game.seriesGameNumber} of ${game.gamesInSeries}`);
  if (game.displayStart) contextParts.push(`${game.displayStart} start`);
  if (game.dayNight) contextParts.push(game.dayNight === "night" ? "Night" : "Day");
  setText(refs.context, contextParts.join(" · "));

  const href = `/gamecenter.html?gamePk=${game.gamePk}`;
  if (refs.link.getAttribute("href") !== href) refs.link.href = href;
}

function reconcileCards(games) {
  const incoming = new Set(games.map(g => String(g.gamePk)));

  for (const [gamePk, refs] of cardsByGamePk.entries()) {
    if (!incoming.has(gamePk)) {
      refs.card.remove();
      cardsByGamePk.delete(gamePk);
    }
  }

  for (const game of games) updateGameCard(game);
}

function leaderRows(target, items, format) {
  target.replaceChildren();
  if (!items?.length) { const empty=document.createElement("div");empty.className="muted";empty.textContent="No qualifying data yet.";target.append(empty);return; }
  items.forEach((item,index)=>{const row=document.createElement("div");row.className="leader-row";const name=document.createElement("strong"),value=document.createElement("span");name.textContent=`${index+1}. ${item.name || item.team || item.value}`;name.title=item.team||"";value.textContent=format(item);row.append(name,value);target.append(row);});
}
function groupedLeaderRows(target,items,format){target.replaceChildren();if(!items?.length){const empty=document.createElement("div");empty.className="muted";empty.textContent="No qualifying data yet.";target.append(empty);return;}for(const category of [...new Set(items.map(item=>item.category))]){const section=document.createElement("section");section.className="leader-category";const heading=document.createElement("h4");heading.textContent=category;section.append(heading);items.filter(item=>item.category===category).forEach((item,index)=>{const row=document.createElement("div");row.className="leader-row";const name=document.createElement("strong"),value=document.createElement("span");name.textContent=`${index+1}. ${item.name}`;name.title=item.team||"";value.textContent=format(item);row.append(name,value);section.append(row)});target.append(section)}}

function showGameAlert(alert) {
  const box=document.getElementById("scoreAlert");box.textContent=alert.text;box.hidden=false;
  const card=cardsByGamePk.get(String(alert.gamePk))?.card;if(card){card.classList.remove("game-alert-flash");void card.offsetWidth;card.classList.add("game-alert-flash");setTimeout(()=>card.classList.remove("game-alert-flash"),4200);}
  clearTimeout(window.scoreAlertTimer);window.scoreAlertTimer=setTimeout(()=>box.hidden=true,5000);
}

async function loadDailyDashboard() {
  try {
    const response=await fetch(`/api/mlb/daily-dashboard?date=${encodeURIComponent(datePicker.value)}`,{cache:"no-store"});if(!response.ok)return;const data=await response.json();
    groupedLeaderRows(document.getElementById("offenseLeaders"),data.offense,item=>item.value);
    groupedLeaderRows(document.getElementById("pitchingLeaders"),data.pitching,item=>item.detail||item.value);
    groupedLeaderRows(document.getElementById("runningLeaders"),data.running,item=>item.value);
    groupedLeaderRows(document.getElementById("defenseLeaders"),data.defense,item=>item.value);
    const alerts=data.alerts||[], keys=new Set(alerts.map(item=>`${item.gamePk}|${item.kind}|${item.text}`));
    if(knownAlertKeys) alerts.filter(item=>!knownAlertKeys.has(`${item.gamePk}|${item.kind}|${item.text}`)).slice(-1).forEach(showGameAlert);
    knownAlertKeys=keys;
  } catch { }
}

async function loadGames({fullReset = false} = {}) {
  statusText.textContent = "Updating MLB games…";

  try {
    const response = await fetch(`/api/mlb/games?date=${encodeURIComponent(datePicker.value)}`, {
      cache: "no-store"
    });

    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const games = await response.json();

    if (fullReset) {
      gamesEl.replaceChildren();
      cardsByGamePk.clear();
    }

    reconcileCards(games);
    await loadDailyDashboard();

    statusText.textContent = `${games.length} MLB game${games.length === 1 ? "" : "s"}`;
    lastUpdated.textContent = `Updated ${new Date().toLocaleTimeString()}`;
  } catch (error) {
    statusText.textContent = `Unable to load MLB data: ${error.message}`;
  }
}

refreshButton.addEventListener("click", () => loadGames());
datePicker.addEventListener("change", () => loadGames({fullReset:true}));

loadGames({fullReset:true});
setInterval(() => loadGames(), 30000);
