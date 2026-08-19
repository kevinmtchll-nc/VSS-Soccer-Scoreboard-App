const $ = id => document.getElementById(id);

let allDivisions = [];
let selectedLeague = 103;

function teamLogoUrl(teamId) {
  return `https://www.mlbstatic.com/team-logos/${teamId}.svg`;
}

function safe(value, fallback = "—") {
  return value === null || value === undefined || value === "" ? fallback : value;
}

function divisionDisplayName(id, apiName) {
  const names = {
    200: "AL West",
    201: "AL East",
    202: "AL Central",
    203: "NL West",
    204: "NL East",
    205: "NL Central"
  };
  return names[id] || apiName || `Division ${id}`;
}

function rankNumber(value) {
  const n = Number.parseInt(value, 10);
  return Number.isFinite(n) ? n : 999;
}

async function loadStandings() {
  $("standingsStatus").textContent = "Loading MLB standings…";

  try {
    const response = await fetch(`/api/mlb/standings?season=${new Date().getFullYear()}`, {cache:"no-store"});
    if (!response.ok) throw new Error(`HTTP ${response.status}`);

    const data = await response.json();
    allDivisions = data.divisions || [];
    renderStandings();

    $("standingsStatus").textContent =
      `${data.season} MLB standings · ${allDivisions.reduce((n,d) => n + (d.teams?.length || 0), 0)} teams`;
    $("standingsUpdated").textContent = `Updated ${new Date().toLocaleTimeString()}`;
  } catch (error) {
    $("standingsStatus").textContent = `Unable to load standings: ${error.message}`;
  }
}

function renderStandings() {
  const host = $("standingsDivisions");
  host.replaceChildren();

  const divisions = allDivisions
    .filter(d => d.leagueId === selectedLeague)
    .sort((a,b) => a.divisionId - b.divisionId);

  for (const division of divisions) {
    const fragment = $("divisionTemplate").content.cloneNode(true);
    fragment.querySelector(".standings-league").textContent = division.leagueName || "";
    fragment.querySelector(".standings-division-name").textContent =
      divisionDisplayName(division.divisionId, division.divisionName);

    const body = fragment.querySelector("tbody");

    [...(division.teams || [])]
      .sort((a,b) => rankNumber(a.divisionRank) - rankNumber(b.divisionRank))
      .forEach(team => {
        const row = document.createElement("tr");
        if (team.divisionLeader) row.classList.add("division-leader");

        const teamCell = document.createElement("td");
        teamCell.className = "standings-team-cell";

        const logo = document.createElement("img");
        logo.className = "standings-logo";
        logo.alt = "";
        logo.src = teamLogoUrl(team.teamId);
        logo.onerror = () => logo.remove();

        const text = document.createElement("div");
        const name = document.createElement("strong");
        name.textContent = team.teamName;

        const sub = document.createElement("span");
        const tags = [];
        if (team.clinchIndicator) tags.push(team.clinchIndicator);
        if (team.divisionLeader) tags.push("Division Leader");
        else if (team.wildCardRank && rankNumber(team.wildCardRank) <= 3)
          tags.push(`WC ${team.wildCardRank}`);
        sub.textContent = tags.join(" · ");

        text.append(name, sub);
        teamCell.append(logo, text);

        const values = [
          team.wins,
          team.losses,
          safe(team.pct),
          safe(team.gamesBack, "0"),
          safe(team.wildCardGamesBack, "—"),
          safe(team.lastTen),
          safe(team.streak),
          safe(team.homeRecord),
          safe(team.awayRecord)
        ];

        row.appendChild(teamCell);

        values.forEach(value => {
          const td = document.createElement("td");
          td.textContent = value;
          row.appendChild(td);
        });

        const diff = document.createElement("td");
        const n = team.runDifferential ?? 0;
        diff.textContent = n > 0 ? `+${n}` : `${n}`;
        diff.className = n > 0 ? "run-diff positive" : n < 0 ? "run-diff negative" : "run-diff";
        row.appendChild(diff);

        body.appendChild(row);
      });

    host.appendChild(fragment);
  }

  renderWildCard();
}

function renderWildCard() {
  const body = $("wildCardTableBody");
  if (!body) return;
  body.replaceChildren();

  $("wildCardLeagueLabel").textContent =
    selectedLeague === 103 ? "American League" : "National League";

  const allTeams = allDivisions
    .filter(d => d.leagueId === selectedLeague)
    .flatMap(d => d.teams || []);

  const candidates = allTeams
    .filter(team => !team.divisionLeader)
    .sort((a,b) => {
      const ar = rankNumber(a.wildCardRank);
      const br = rankNumber(b.wildCardRank);
      if (ar !== br) return ar - br;

      const ap = Number.parseFloat(a.pct || "0");
      const bp = Number.parseFloat(b.pct || "0");
      return bp - ap;
    });

  candidates.forEach((team, index) => {
    const row = document.createElement("tr");
    if (index < 3) row.classList.add("wildcard-position");

    const teamCell = document.createElement("td");
    teamCell.className = "standings-team-cell";

    const logo = document.createElement("img");
    logo.className = "standings-logo";
    logo.alt = "";
    logo.src = teamLogoUrl(team.teamId);
    logo.onerror = () => logo.remove();

    const text = document.createElement("div");
    const name = document.createElement("strong");
    name.textContent = team.teamName;

    const sub = document.createElement("span");
    const label = index < 3 ? `WC ${index + 1}` : `WC ${index + 1}`;
    sub.textContent = team.clinchIndicator
      ? `${label} · ${team.clinchIndicator}`
      : label;

    text.append(name, sub);
    teamCell.append(logo, text);
    row.appendChild(teamCell);

    const values = [
      team.wins,
      team.losses,
      safe(team.pct),
      index === 0 ? "—" : safe(team.wildCardGamesBack, "—"),
      safe(team.lastTen),
      safe(team.streak),
      safe(team.homeRecord),
      safe(team.awayRecord)
    ];

    values.forEach(value => {
      const td = document.createElement("td");
      td.textContent = value;
      row.appendChild(td);
    });

    const diff = document.createElement("td");
    const n = team.runDifferential ?? 0;
    diff.textContent = n > 0 ? `+${n}` : `${n}`;
    diff.className = n > 0 ? "run-diff positive" : n < 0 ? "run-diff negative" : "run-diff";
    row.appendChild(diff);

    body.appendChild(row);
  });
}

document.querySelectorAll(".league-tab").forEach(button => {
  button.addEventListener("click", () => {
    selectedLeague = Number(button.dataset.league);
    document.querySelectorAll(".league-tab").forEach(b => b.classList.toggle("active", b === button));
    renderStandings();
  });
});

$("refreshStandings").addEventListener("click", loadStandings);
loadStandings();
setInterval(loadStandings, 60000);
