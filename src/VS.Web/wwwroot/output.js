const params = new URLSearchParams(location.search);
const scene = params.get("scene") || "scoreboard";
const workspaceTemplateId = params.get("template") || "default";
const workspaceEditing = params.get("edit") === "1";
const gamePk = params.get("gamePk") || "";
const date = params.get("date") || new Date().toISOString().slice(0, 10);
const root = document.getElementById("outputRoot");
if (scene === "game-broadcast-lbar" || scene === "game-broadcast-bottom") root.classList.add("lbar-output-root");
if (scene === "gamecenter-standard") root.classList.add("gamecenter-standard-root");
async function applySavedTheme(){let theme={};try{theme=JSON.parse(localStorage.getItem("vitecGameCenterTheme")||"{}");}catch{}let backgroundUrl="";try{const response=await fetch("/api/theme/background",{cache:"no-store"}),data=await response.json();backgroundUrl=data.url||"";}catch{}document.documentElement.style.setProperty("--theme-bg",theme.color||"#060a10");document.documentElement.style.setProperty("--theme-shade",String((theme.shade??55)/100));document.documentElement.style.setProperty("--theme-panel-alpha",String(1-(theme.transparency??0)/100));document.documentElement.style.setProperty("--tile-font-scale",String((theme.fontSize??100)/100));document.documentElement.style.setProperty("--tile-font-color",theme.fontColor||"#f4f7fb");if(backgroundUrl)document.documentElement.style.setProperty("--theme-image",`url("${backgroundUrl}")`);}
applySavedTheme();

const el = (tag, className, text) => {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
};

const logo = (teamId, teamName) => {
  const image = el("img", "output-team-logo");
  image.src = `https://www.mlbstatic.com/team-logos/${teamId}.svg`;
  image.alt = `${teamName} logo`;
  return image;
};

function ordinal(inning) {
  const n = Number(inning);
  if (!n) return "";
  if (n % 100 >= 11 && n % 100 <= 13) return `${n}th`;
  return `${n}${n % 10 === 1 ? "st" : n % 10 === 2 ? "nd" : n % 10 === 3 ? "rd" : "th"}`;
}

function statusText(game) {
  if (String(game.status).toLowerCase() !== "live" || !game.currentInning) return game.detailedStatus || game.status;
  return `${game.detailedStatus || game.status} · ${game.inningState} ${game.inningOrdinal || ordinal(game.currentInning)}`;
}

function scoreboardScene(games) {
  const shell = el("section", "output-scoreboard");
  shell.append(el("h1", "output-title", "MLB Scoreboard"));
  const grid = el("div", "output-game-grid");
  games.forEach(game => {
    const card = el("article", "output-game-card");
    card.append(el("div", "output-game-status", statusText(game)));
    [[game.away, "away"], [game.home, "home"]].forEach(([team]) => {
      const row = el("div", "output-team-row");
      row.append(logo(team.teamId, team.name), el("strong", "output-team-name", team.name), el("span", "output-team-score", team.score));
      card.append(row);
    });
    grid.append(card);
  });
  shell.append(grid);
  return shell;
}

function gameHeader(game) {
  const header = el("section", "output-game-header");
  const away = el("div", "output-feature-team");
  away.append(logo(game.awayTeamId, game.awayTeam), el("strong", "", game.awayTeam), el("span", "output-feature-score", game.awayScore));
  const center = el("div", "output-game-center");
  center.append(el("strong", "", game.detailedStatus), el("span", "", `${game.inningState || ""} ${ordinal(game.inning)}`.trim()));
  const home = el("div", "output-feature-team output-feature-home");
  home.append(el("span", "output-feature-score", game.homeScore), el("strong", "", game.homeTeam), logo(game.homeTeamId, game.homeTeam));
  header.append(away, center, home);
  return header;
}

function liveScene(game) {
  const shell = el("section", "output-feature-scene");
  shell.append(gameHeader(game));
  const content = el("div", "output-live-grid");
  const matchup = el("article", "output-panel");
  matchup.append(el("h2", "", "Live Matchup"));
  matchup.append(el("div", "output-matchup-name", game.matchup?.batter || "—"));
  matchup.append(el("div", "output-muted", `AVG ${game.matchup?.batterAverage || "—"} · HR ${game.matchup?.batterHomeRuns || "—"} · RBI ${game.matchup?.batterRbi || "—"}`));
  matchup.append(el("div", "output-vs", "VS"));
  matchup.append(el("div", "output-matchup-name", game.matchup?.pitcher || "—"));
  matchup.append(el("div", "output-muted", `ERA ${game.matchup?.pitcherEra || "—"} · SO ${game.matchup?.pitcherStrikeouts || "—"}`));
  const state = el("article", "output-panel output-state-panel");
  state.append(el("h2", "", "Game State"), el("div", "output-count", `${game.balls}-${game.strikes} · ${game.outs} out${game.outs === 1 ? "" : "s"}`), el("p", "", game.lastPlay || "Waiting for play data…"));
  content.append(matchup, state);
  shell.append(content);
  return shell;
}

function scoringScene(game) {
  const shell = el("section", "output-feature-scene");
  shell.append(gameHeader(game), el("h2", "output-section-title", "Scoring Plays"));
  const list = el("div", "output-scoring-list");
  (game.scoringPlays || []).slice(-8).reverse().forEach(play => {
    const item = el("article", "output-scoring-item");
    item.append(el("strong", "output-scoring-inning", `${play.halfInning === "bottom" ? "Bottom" : "Top"} ${ordinal(play.inning)}`), el("span", "output-scoring-description", play.description), el("strong", "output-scoring-score", `${game.awayAbbreviation} ${play.awayScore} · ${game.homeAbbreviation} ${play.homeScore}`));
    list.append(item);
  });
  if (!list.children.length) list.append(el("div", "output-empty", "No scoring plays yet."));
  shell.append(list);
  return shell;
}

function boxScoreScene(game) {
  const shell = el("section", "output-feature-scene");
  shell.append(gameHeader(game), el("h2", "output-section-title", "Current Box Score"));
  const teams = el("div", "output-box-teams");
  [game.boxScore.away, game.boxScore.home].forEach(team => {
    const section = el("section", "output-panel");
    section.append(el("h2", "", team.teamName));
    (team.highlights || []).filter(item => ["Batting", "Baserunning", "Fielding"].includes(item.section)).forEach(item => section.append(el("div", "output-highlight", `${item.label} — ${item.value}`)));
    const table = el("div", "output-compact-table");
    (team.batting || []).slice(0, 10).forEach(player => table.append(el("div", "", `${player.name}  ${player.hits}-${player.atBats}  RBI ${player.rbi}  AVG ${player.average}`)));
    section.append(table);
    teams.append(section);
  });
  shell.append(teams);
  return shell;
}

const headshot = (playerId, name) => {
  const image = el("img", "broadcast-headshot");
  image.src = playerId ? `https://img.mlbstatic.com/mlb-photos/image/upload/w_213,q_100/v1/people/${playerId}/headshot/67/current` : "";
  image.alt = name ? `${name} headshot` : "";
  return image;
};

function broadcastTeamBox(team) {
  const section = el("section", "broadcast-team-box");
  const title=el("h2", "broadcast-team-title");title.append(logo(team.teamId,team.teamName),el("span","",team.teamName));section.append(title);
  const highlights = el("div", "broadcast-highlights");
  (team.highlights || []).filter(item => item.section !== "Pitchers").slice(0, 4)
    .forEach(item => highlights.append(el("span", "", `${item.label}: ${item.value}`)));
  section.append(highlights);
  const header = el("div", "broadcast-table-row broadcast-table-header");
  ["BATTER","POS","AB","R","H","RBI","AVG","HR"].forEach(value => header.append(el("span", "", value)));
  section.append(header);
  (team.batting || []).slice(0, 9).forEach(player => {
    const row = el("div", "broadcast-table-row");
    [player.name,player.position,player.atBats,player.runs,player.hits,player.rbi,player.average,player.homeRuns].forEach(value => row.append(el("span", "", value ?? "—")));
    section.append(row);
  });
  const pitchers = el("div", "broadcast-pitchers");
  (team.pitching || []).slice(0, 3).forEach(player => pitchers.append(el("span", "", `${player.name}  ${player.inningsPitched} IP · ${player.strikeouts} SO · ${player.era} ERA · ${player.pitchCount} PC`)));
  section.append(pitchers);
  return section;
}

function workspaceLineScore(game) {
  const score=game.lineScore||{}, innings=(score.innings||[]).length?score.innings:Array.from({length:9},(_,i)=>({inning:i+1}));
  const table=el("table","workspace-line-score");
  const head=el("tr",""); ["TEAM",...innings.map(x=>x.inning),"R","H","E"].forEach(x=>head.append(el("th","",x))); table.append(head);
  [[game.awayTeam,innings.map(x=>x.awayRuns),score.awayRuns,score.awayHits,score.awayErrors],[game.homeTeam,innings.map(x=>x.homeRuns),score.homeRuns,score.homeHits,score.homeErrors]].forEach(([name,runs,r,h,e])=>{const row=el("tr","");[name,...runs.map(x=>x??"â€”"),r,h,e].forEach(x=>row.append(el("td","",x??"â€”")));table.append(row);});
  table.querySelectorAll("td").forEach(cell=>{if(cell.textContent && /[^\x00-\x7F]/.test(cell.textContent))cell.textContent="-";});
  const wrap=el("div","workspace-line-score-wrap");wrap.append(table);return wrap;
}

function pitchZone(pitches) {
  const zone = el("div", "broadcast-zone");
  const currentAtBat = Math.max(...(pitches || []).map(p => Number(p.atBatIndex) || 0), 0);
  (pitches || []).filter(p => Number(p.atBatIndex) === currentAtBat && Number.isFinite(p.plateX) && Number.isFinite(p.plateZ)).slice(-8).forEach((pitch, index, set) => {
    const dot = el("span", `broadcast-pitch-dot ${index === set.length - 1 ? "latest" : ""}`, String(pitch.pitchNumber));
    dot.style.left = `${Math.max(3, Math.min(97, ((pitch.plateX + 2.25) / 4.5) * 100))}%`;
    dot.style.top = `${Math.max(3, Math.min(97, (1 - ((pitch.plateZ - 0.5) / 4.5)) * 100))}%`;
    zone.append(dot);
  });
  return zone;
}

function broadcastScene(game, pitches) {
  const shell = el("section", "output-feature-scene broadcast-scene");
  shell.append(gameHeader(game));
  const body = el("div", "broadcast-grid");
  const live = el("section", "broadcast-live");
  const matchup = el("div", "broadcast-matchup");
  const batter = el("div", "broadcast-player");
  batter.append(headshot(game.matchup?.batterId, game.matchup?.batter), el("small", "", "AT BAT"), el("strong", "", game.matchup?.batter || "—"), el("span", "", `AVG ${game.matchup?.batterAverage || "—"} · HR ${game.matchup?.batterHomeRuns || "—"} · RBI ${game.matchup?.batterRbi || "—"}`));
  const pitcher = el("div", "broadcast-player");
  pitcher.append(headshot(game.matchup?.pitcherId, game.matchup?.pitcher), el("small", "", "PITCHING"), el("strong", "", game.matchup?.pitcher || "—"), el("span", "", `${game.matchup?.pitcherWins || "—"}-${game.matchup?.pitcherLosses || "—"} · ERA ${game.matchup?.pitcherEra || "—"} · SO ${game.matchup?.pitcherStrikeouts || "—"}`));
  matchup.append(batter, el("div", "broadcast-vs", "VS"), pitcher);
  const state = el("div", "broadcast-state");
  [["BALLS",game.balls],["STRIKES",game.strikes],["OUTS",game.outs]].forEach(([label,value]) => { const item=el("div",""); item.append(el("small","",label),el("strong","",value)); state.append(item); });
  const visual = el("div", "broadcast-visual");
  visual.append(pitchZone(pitches));
  const recent = el("div", "broadcast-recent");
  recent.append(el("h3", "", "Recent Pitches"));
  (pitches || []).slice(-5).reverse().forEach(p => recent.append(el("div", "", `${p.pitchCode || "Pitch"} · ${p.startSpeedMph ? `${p.startSpeedMph.toFixed(1)} mph` : "—"} · ${p.result || ""}`)));
  visual.append(recent);
  const last = el("div", "broadcast-last-play", game.lastPlay || game.lastEvent?.description || "Waiting for the next play...");
  live.append(matchup, state, visual, last);
  const boxes = el("section", "broadcast-boxes");
  boxes.append(broadcastTeamBox(game.boxScore.away), broadcastTeamBox(game.boxScore.home));
  body.append(live, boxes); shell.append(body); return shell;
}

function workspaceTile(id, title, content, layout) {
  const tile = el("section", `workspace-tile workspace-tile-${id}`);
  tile.dataset.tile = id;
  tile.style.cssText = `left:${layout.x}%;top:${layout.y}%;width:${layout.width}%;height:${layout.height}%;z-index:${layout.z};${layout.visible === false ? "display:none;" : ""}`;
  const heading = el("div", "workspace-tile-heading", title);
  tile.append(heading, content);
  if (workspaceEditing) tile.append(el("span", "workspace-resize-handle", ""));
  return tile;
}

async function workspaceScene(game, pitches) {
  const response = await fetch(`/api/workspace/templates/${encodeURIComponent(workspaceTemplateId)}`, {cache:"no-store"});
  if (!response.ok) throw new Error("The selected workspace template was not found.");
  const template = await response.json();
  const layouts = Object.fromEntries((template.tiles || []).map(tile => [tile.id, tile]));
  if(layouts.matchup&&layouts.live){const a=layouts.live,b=layouts.matchup,x=Math.min(a.x,b.x),y=Math.min(a.y,b.y);layouts.live={...a,x,y,width:Math.max(a.x+a.width,b.x+b.width)-x,height:Math.max(a.y+a.height,b.y+b.height)-y};delete layouts.matchup;}
  if(layouts.boxscore&&!layouts.awaybox){layouts.awaybox={...layouts.boxscore,width:layouts.boxscore.width/2};layouts.homebox={...layouts.boxscore,x:layouts.boxscore.x+layouts.boxscore.width/2,width:layouts.boxscore.width/2};}
  const fallback={linescore:{x:0,y:0,width:100,height:18,z:4,visible:true},awaybox:{x:66,y:18,width:17,height:82,z:5,visible:true},homebox:{x:83,y:18,width:17,height:82,z:6,visible:true}};
  Object.entries(fallback).forEach(([id,value])=>layouts[id]??=value);
  const composed = broadcastScene(game, pitches);
  const header = composed.firstElementChild;
  const body = composed.lastElementChild;
  const liveSource = body.firstElementChild;
  const boxes = body.lastElementChild;
  const awayBox=boxes.children[0], homeBox=boxes.children[1];
  const matchup = liveSource.children[0], state = liveSource.children[1], visual = liveSource.children[2], last = liveSource.children[3];
  const zone = visual.children[0], recent = visual.children[1];
  const live = el("div", "workspace-live-content workspace-combined-content"); live.append(matchup, state, zone, last);
  const recentContent = el("div", "workspace-recent-content"); recentContent.append(recent);
  const shell = el("section", `workspace-scene ${workspaceEditing ? "workspace-editing" : ""}`);
  shell.append(header);
  const canvas = el("div", "workspace-canvas");
  canvas.append(
    workspaceTile("live", "Live Game & Matchup", live, layouts.live),
    workspaceTile("recent", "Recent Action", recentContent, layouts.recent),
    workspaceTile("linescore", "Inning Line Score", workspaceLineScore(game), layouts.linescore),
    workspaceTile("awaybox", `${game.awayTeam} Box Score`, awayBox, layouts.awaybox),
    workspaceTile("homebox", `${game.homeTeam} Box Score`, homeBox, layouts.homebox)
  );
  shell.append(canvas);
  if (workspaceEditing) enableWorkspaceEditing(shell, template);
  return shell;
}

function enableWorkspaceEditing(shell, template) {
  const toolbar = el("div", "workspace-editor-toolbar");
  const name = document.createElement("input"); name.value = template.id === "default" ? "My Workspace" : template.name;
  const save = el("button", "", "Save Template");
  const status = el("span", "", "Drag tiles by their title bars; resize from the lower-right corner.");
  toolbar.append(name);
  [["live","Live & Matchup"],["recent","Recent"],["linescore","Line Score"],["awaybox","Away Box"],["homebox","Home Box"]].forEach(([id,label]) => {
    const control=document.createElement("label"), checkbox=document.createElement("input"); checkbox.type="checkbox"; checkbox.checked=shell.querySelector(`[data-tile="${id}"]`).style.display!=="none";
    checkbox.addEventListener("change",()=>shell.querySelector(`[data-tile="${id}"]`).style.display=checkbox.checked?"":"none"); control.append(checkbox,document.createTextNode(label)); toolbar.append(control);
  });
  toolbar.append(save, status); document.body.append(toolbar);
  shell.querySelectorAll(".workspace-tile").forEach(tile => {
    const begin = (event, resizing) => {
      event.preventDefault(); tile.style.zIndex=String(Math.max(...[...shell.querySelectorAll(".workspace-tile")].map(item=>Number(item.style.zIndex)||1))+1); const canvas = tile.parentElement.getBoundingClientRect(); const start = tile.getBoundingClientRect();
      const startX = event.clientX, startY = event.clientY;
      const move = e => {
        if (resizing) {
          tile.style.width = `${Math.max(5, Math.min(100 - ((start.left-canvas.left)/canvas.width*100), (start.width + e.clientX-startX)/canvas.width*100))}%`;
          tile.style.height = `${Math.max(5, Math.min(100 - ((start.top-canvas.top)/canvas.height*100), (start.height + e.clientY-startY)/canvas.height*100))}%`;
        } else {
          tile.style.left = `${Math.max(0, Math.min(100 - tile.offsetWidth/canvas.width*100, (start.left-canvas.left + e.clientX-startX)/canvas.width*100))}%`;
          tile.style.top = `${Math.max(0, Math.min(100 - tile.offsetHeight/canvas.height*100, (start.top-canvas.top + e.clientY-startY)/canvas.height*100))}%`;
        }
      };
      const up = () => { window.removeEventListener("pointermove", move); window.removeEventListener("pointerup", up); };
      window.addEventListener("pointermove", move); window.addEventListener("pointerup", up);
    };
    tile.querySelector(".workspace-tile-heading").addEventListener("pointerdown", e => begin(e, false));
    tile.querySelector(".workspace-resize-handle").addEventListener("pointerdown", e => begin(e, true));
  });
  save.addEventListener("click", async () => {
    const canvas = shell.querySelector(".workspace-canvas").getBoundingClientRect();
    const tiles = [...shell.querySelectorAll(".workspace-tile")].map((tile, index) => { const r=tile.getBoundingClientRect(); return {id:tile.dataset.tile,x:(r.left-canvas.left)/canvas.width*100,y:(r.top-canvas.top)/canvas.height*100,width:r.width/canvas.width*100,height:r.height/canvas.height*100,z:index+1,visible:tile.style.display!=="none"}; });
    const response = await fetch("/api/workspace/templates", {method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({id:template.id === "default" ? "" : template.id,name:name.value,tiles})});
    const body = await response.json().catch(()=>({}));
    if (response.ok) { template.id=body.id; template.name=body.name; const url=new URL(location.href); url.searchParams.set("template",body.id); history.replaceState(null,"",url); status.textContent=`Saved as “${body.name}”. It is now available in the output scene lists.`; }
    else status.textContent=body.message || "Unable to save template.";
  });
}

function advertisingMedia(media, slot) {
  const frame = el("div", `lbar-ad lbar-ad-${slot}`);
  if (!media?.url) {
    frame.append(el("div", "lbar-placeholder", slot === "rail" ? "LEFT RAIL ADVERTISING" : "BOTTOM BANNER ADVERTISING"));
    return frame;
  }
  if (media.mediaType === "video") {
    const video = el("video", "lbar-media");
    video.src = `${media.url}?v=${Date.now()}`; video.autoplay = true; video.muted = true; video.loop = true; video.playsInline = true;
    video.addEventListener("loadedmetadata", () => applyAdvertisingFit(video, slot, video.videoWidth, video.videoHeight));
    frame.append(video);
  } else {
    const image = el("img", "lbar-media"); image.src = `${media.url}?v=${Date.now()}`; image.alt = `${slot} advertising`;
    image.addEventListener("load", () => applyAdvertisingFit(image, slot, image.naturalWidth, image.naturalHeight));
    frame.append(image);
  }
  return frame;
}

function applyAdvertisingFit(media, slot, width, height) {
  const targetRatio = slot === "rail" ? 0.5 : 8;
  const actualRatio = width > 0 && height > 0 ? width / height : targetRatio;
  const difference = Math.abs(actualRatio - targetRatio) / targetRatio;
  media.classList.toggle("ratio-match", difference <= 0.03);
  media.classList.toggle("ratio-adjusted", difference > 0.03);
}

function broadcastLBarScene(game, pitches, ads) {
  const shell = el("section", "lbar-scene");
  const program = el("div", "lbar-program");
  program.append(broadcastScene(game, pitches));
  shell.append(program, advertisingMedia(ads?.rail, "rail"), advertisingMedia(ads?.banner, "banner"));
  return shell;
}

function broadcastBottomBarScene(game, pitches, ads) {
  const shell = el("section", "bottom-bar-scene");
  const program = el("div", "bottom-bar-program"); program.append(broadcastScene(game, pitches));
  shell.append(program, advertisingMedia(ads?.banner, "banner")); return shell;
}

async function refresh() {
  try {
    let content;
    if (scene === "gamecenter-standard") {
      if (!gamePk) throw new Error("A gamePk is required for this scene.");
      if (!root.querySelector(".gamecenter-standard-frame")) {
        const frame = el("iframe", "gamecenter-standard-frame");
        frame.src = `/gamecenter.html?gamePk=${encodeURIComponent(gamePk)}&output=1`;
        frame.title = "GameCenter Standard View";
        root.replaceChildren(frame);
      }
      return;
    } else if (scene === "scoreboard") {
      const response = await fetch(`/api/mlb/games?date=${encodeURIComponent(date)}`, {cache:"no-store"});
      content = scoreboardScene(await response.json());
    } else {
      if (!gamePk) throw new Error("A gamePk is required for this scene.");
      const response = await fetch(`/api/mlb/games/${encodeURIComponent(gamePk)}/summary`, {cache:"no-store"});
      const game = await response.json();
      if (scene === "game-broadcast" || scene === "game-broadcast-lbar" || scene === "game-broadcast-bottom" || scene === "game-workspace") {
        const pitchResponse = await fetch(`/api/mlb/games/${encodeURIComponent(gamePk)}/pitches`, {cache:"no-store"});
        const pitches = await pitchResponse.json();
        if (scene === "game-workspace") content = await workspaceScene(game, pitches);
        else if (scene === "game-broadcast-lbar" || scene === "game-broadcast-bottom") {
          const adResponse = await fetch("/api/advertising/status", {cache:"no-store"});
          const ads = await adResponse.json();
          content = scene === "game-broadcast-lbar" ? broadcastLBarScene(game, pitches, ads) : broadcastBottomBarScene(game, pitches, ads);
        } else content = broadcastScene(game, pitches);
      } else content = scene === "game-scoring" ? scoringScene(game) : scene === "game-boxscore" ? boxScoreScene(game) : liveScene(game);
    }
    root.replaceChildren(content);
  } catch (error) {
    root.replaceChildren(el("div", "output-error", `Unable to load output: ${error.message}`));
  }
}

refresh();
setInterval(refresh, 10000);
