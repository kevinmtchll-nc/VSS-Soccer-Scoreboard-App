const params = new URLSearchParams(location.search);
const gamePk = params.get("gamePk") || "824240";
const outputMode = params.get("output") === "1";
if (outputMode) document.body.classList.add("gamecenter-output-mode");

const $ = id => document.getElementById(id);
$("gameId").textContent = `gamePk ${gamePk}`;

let currentGame = null;
let allPitches = [];
let lastSuccessfulUpdate = null;
let previousScoreKey = null;
let followLive = true;
let previousLastPlay = "";
let previousPitcherIdentity = null;
let eventHideTimer = null;
let stadiumBoardTimer = null;
let previousFinalState = false;
let showLivePitchesLayer = true;
let heatMapLayer = "none"; // none | atbat | game
let liveDetailView = "live";
let recentActionCount = Math.max(1, Math.min(20, Number(localStorage.getItem("vitecGameCenterRecentActionCount")) || 10));

$("refreshButton").addEventListener("click", refreshGameCenter);
$("eventClose").addEventListener("click", hideEventOverlay);

document.querySelectorAll(".game-tab").forEach(button => {
  button.addEventListener("click", () => {
    document.querySelectorAll(".game-tab").forEach(b => b.classList.remove("active"));
    button.classList.add("active");

    const live = button.dataset.view === "live";
    $("liveGameView").hidden = !live;
    $("pitchingView").hidden = live;
  });
});

function setGameCenterLayout(layout) {
  const expanded = layout === "expanded";
  document.body.classList.toggle("gamecenter-expanded", expanded);
  $("standardLayout").classList.toggle("active", !expanded);
  $("expandedLayout").classList.toggle("active", expanded);
  localStorage.setItem("vitecGameCenterLayout", expanded ? "expanded" : "standard");
}

$("standardLayout").addEventListener("click", () => setGameCenterLayout("standard"));
$("expandedLayout").addEventListener("click", () => setGameCenterLayout("expanded"));
setGameCenterLayout("standard");

function combineLiveMatchupTile() {
  const livePanel=document.querySelector("#liveDetailPanel>.live-primary"),visualPanel=document.querySelector("#liveDetailPanel>.matchup-visual-panel");
  if(!livePanel||!visualPanel)return;
  livePanel.classList.add("combined-matchup-panel");livePanel.querySelector("h2").textContent="Live Game & Matchup";
  livePanel.querySelector(".panel-heading-row").append(visualPanel.querySelector(".matchup-layer-controls"));
  const atbat=livePanel.querySelector(".live-atbat"),players=atbat.querySelectorAll(".live-player"),zoneColumn=visualPanel.querySelector(".matchup-zone-column");
  players.forEach(player=>{const photo=player.querySelector("img"),details=player.querySelector("div"),name=details.querySelector("strong");player.className="combined-player-card";photo.className="combined-player-photo";details.className="combined-player-details";name.className="combined-player-name";player.replaceChildren(name,photo,details);});
  const state=document.createElement("div");state.className="combined-game-state";state.append(livePanel.querySelector(".live-count-grid"),livePanel.querySelector(".live-bases-section"));
  zoneColumn.prepend(state);zoneColumn.append(livePanel.querySelector("#runnerMovement"),zoneColumn.querySelector(".current-pitch-card"));
  const stage=document.createElement("div");stage.className="combined-matchup-stage";stage.append(players[0],zoneColumn,players[1]);
  visualPanel.querySelectorAll(".matchup-portrait").forEach(portrait=>{portrait.hidden=true;livePanel.append(portrait);});
  atbat.remove();livePanel.querySelector(".live-last-play").before(stage);visualPanel.remove();
}
combineLiveMatchupTile();

const boxScoreFitObserver=new ResizeObserver(entries=>entries.forEach(entry=>{
  const width=entry.contentRect.width,height=entry.contentRect.height;
  entry.target.style.setProperty("--box-fit-scale",Math.max(.52,Math.min(1.3,width/390,height/720)).toFixed(3));
}));
[$("awayBoxScorePanel"),$("homeBoxScorePanel")].forEach(panel=>panel&&boxScoreFitObserver.observe(panel));

const dashboardDefaults = {
  live:{x:20.5,y:13,w:58,h:57}, recent:{x:54,y:71,w:46,h:29}, linescore:{x:20.5,y:0,w:58,h:12},
  awaybox:{x:0,y:0,w:20,h:70}, homebox:{x:79,y:0,w:21,h:70}, scoring:{x:0,y:71,w:53.5,h:29}
};
let dashboardLayout = JSON.parse(localStorage.getItem("vitecGameCenterTileLayout") || "null") || structuredClone(dashboardDefaults);
if(localStorage.getItem("vitecGameCenterTileLayoutVersion")!=="4"){dashboardLayout=structuredClone(dashboardDefaults);localStorage.setItem("vitecGameCenterTileLayout",JSON.stringify(dashboardLayout));localStorage.setItem("vitecGameCenterTileLayoutVersion","4");}
for(const [id,value] of Object.entries(dashboardLayout)) {
  if(value && value.col != null) dashboardLayout[id]={x:(value.col-1)/12*100,y:(value.row-1)/10*100,w:value.w/12*100,h:value.h/10*100};
}
if(dashboardLayout.matchup){const a=dashboardLayout.live||dashboardDefaults.live,b=dashboardLayout.matchup;const x=Math.min(a.x,b.x),y=Math.min(a.y,b.y);dashboardLayout.live={x,y,w:Math.max(a.x+a.w,b.x+b.w)-x,h:Math.max(a.y+a.h,b.y+b.h)-y};delete dashboardLayout.matchup;}
if (dashboardLayout.boxscore) {
  const old=dashboardLayout.boxscore;
  dashboardLayout.awaybox={x:old.x,y:old.y,w:old.w/2,h:old.h};
  dashboardLayout.homebox={x:old.x+old.w/2,y:old.y,w:old.w/2,h:old.h};
  delete dashboardLayout.boxscore;
}
for(const [id,value] of Object.entries(dashboardDefaults)) dashboardLayout[id]??=structuredClone(value);
let dashboardEditing = false;
const dashboardTiles = () => ({
  live:document.querySelector("#liveDetailPanel>.live-primary"),
  recent:document.querySelector("#liveDetailPanel>.live-events"),
  linescore:$("lineScorePanel"), awaybox:$("awayBoxScorePanel"), homebox:$("homeBoxScorePanel"), scoring:$("scoringDetailPanel")
});

const dashboardTileNames={live:"Live Game & Matchup",recent:"Recent Action",linescore:"Inning Line Score",awaybox:"Away Box Score",homebox:"Home Box Score",scoring:"Scoring Plays"};
function raiseDashboardTile(id){const tiles=Object.values(dashboardTiles()).filter(Boolean),tile=dashboardTiles()[id];if(!tile)return;tile.style.zIndex=String(Math.max(...tiles.map(item=>Number(item.style.zIndex)||1))+1);tile.classList.remove("dashboard-tile-focus");void tile.offsetWidth;tile.classList.add("dashboard-tile-focus");}

function applyDashboardLayout() {
  for (const [id,tile] of Object.entries(dashboardTiles())) {
    if (!tile) continue; const value=dashboardLayout[id] || dashboardDefaults[id];
    tile.dataset.dashboardTile=id; tile.style.left=`${value.x}%`;tile.style.top=`${value.y}%`;tile.style.width=`${value.w}%`;tile.style.height=`${value.h}%`;tile.style.zIndex=tile.style.zIndex||String(Object.keys(dashboardDefaults).indexOf(id)+1);
  }
}

function beginDashboardPointer(event, id, mode) {
  if (!dashboardEditing) return; event.preventDefault(); event.stopPropagation();
  const grid=$("liveDetailPanel").getBoundingClientRect(), start={...dashboardLayout[id]}, x=event.clientX, y=event.clientY;
  document.body.classList.add("dashboard-dragging"); const dragged=dashboardTiles()[id];
  dragged.style.zIndex=String(Math.max(...Object.values(dashboardTiles()).map(tile=>Number(tile?.style.zIndex)||1))+1);
  const update=e=>{
    const dx=(e.clientX-x)/grid.width*100,dy=(e.clientY-y)/grid.height*100; let next={...start};
    if(mode==="move") { next.x=Math.max(0,Math.min(100-start.w,start.x+dx)); next.y=Math.max(0,Math.min(100-start.h,start.y+dy)); }
    else if(mode==="nw") { const nx=Math.max(0,Math.min(start.x+start.w-5,start.x+dx)),ny=Math.max(0,Math.min(start.y+start.h-5,start.y+dy));next.w=start.w+(start.x-nx);next.h=start.h+(start.y-ny);next.x=nx;next.y=ny; }
    else { next.w=Math.max(5,Math.min(100-start.x,start.w+dx)); next.h=Math.max(5,Math.min(100-start.y,start.h+dy)); }
    dashboardLayout[id]=next;
    dragged.style.left=`${next.x}%`;dragged.style.top=`${next.y}%`;dragged.style.width=`${next.w}%`;dragged.style.height=`${next.h}%`;
  };
  let pendingEvent=null,animationFrame=0;
  const move=e=>{pendingEvent=e;if(!animationFrame)animationFrame=requestAnimationFrame(()=>{animationFrame=0;update(pendingEvent);});};
  const up=()=>{if(animationFrame){cancelAnimationFrame(animationFrame);update(pendingEvent);}window.removeEventListener("pointermove",move);window.removeEventListener("pointerup",up);document.body.classList.remove("dashboard-dragging");localStorage.setItem("vitecGameCenterTileLayout",JSON.stringify(dashboardLayout));};
  window.addEventListener("pointermove",move);window.addEventListener("pointerup",up);
}

function setDashboardEditing(enabled) {
  dashboardEditing=enabled; document.body.classList.toggle("dashboard-editing",enabled); $("dashboardLayoutToolbar").hidden=!enabled;
  if(enabled) { $("settingsPanel").hidden=false; $("layoutSettingsSection").open=true; }
  const boxes=[$("lineScorePanel"),$("awayBoxScorePanel"),$("homeBoxScorePanel"),$("scoringDetailPanel")], grid=$("liveDetailPanel");
  document.body.classList.add("dashboard-layout-applied"); setGameCenterLayout("standard"); $("showBoxScore").checked=true; boxes.forEach(box=>{box.hidden=false;grid.append(box);});
  if(enabled) {
    for(const [id,tile] of Object.entries(dashboardTiles())) {
      if(!tile.querySelector(".dashboard-resize-handle")) { const nw=document.createElement("span"),se=document.createElement("span"); nw.className="dashboard-resize-handle dashboard-resize-nw";se.className="dashboard-resize-handle dashboard-resize-se";nw.addEventListener("pointerdown",e=>beginDashboardPointer(e,id,"nw"));se.addEventListener("pointerdown",e=>beginDashboardPointer(e,id,"se"));tile.append(nw,se); }
      const heading=tile.querySelector(".panel-heading-row"); if(heading && !heading.dataset.dashboardBound){heading.dataset.dashboardBound="1";heading.addEventListener("pointerdown",e=>{if(!e.target.closest("button,input,label"))beginDashboardPointer(e,id,"move");});}
    }
    const buttons=$("dashboardTileButtons");buttons.replaceChildren();for(const id of Object.keys(dashboardDefaults)){const button=document.createElement("button");button.type="button";button.textContent=dashboardTileNames[id];button.addEventListener("click",()=>raiseDashboardTile(id));buttons.append(button);}
    applyDashboardLayout();
  } else {
    applyDashboardLayout();
  }
}

$("editDashboardLayout").addEventListener("click",()=>setDashboardEditing(true));
$("finishDashboardLayout").addEventListener("click",()=>setDashboardEditing(false));
$("resetDashboardLayout").addEventListener("click",()=>{dashboardLayout=structuredClone(dashboardDefaults);applyDashboardLayout();localStorage.removeItem("vitecGameCenterTileLayout");});
$("saveDashboardTemplate").addEventListener("click",async()=>{
  const name=$("dashboardTemplateName").value.trim(), tiles=Object.entries(dashboardLayout).map(([id,v],z)=>({id,x:v.x,y:v.y,width:v.w,height:v.h,z:Number(dashboardTiles()[id]?.style.zIndex)||z+1,visible:true}));
  const response=await fetch("/api/workspace/templates",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({id:"",name,tiles})}); const body=await response.json().catch(()=>({}));
  $("dashboardLayoutStatus").textContent=response.ok?`Saved “${body.name}” for browser and video output.`:body.message||"Unable to save template.";
});
setDashboardEditing(false);

$("recentActionCount").value = String(recentActionCount);
$("recentActionCount").addEventListener("change", event => {
  recentActionCount = Math.max(1, Math.min(20, Number(event.target.value) || 10));
  localStorage.setItem("vitecGameCenterRecentActionCount", String(recentActionCount));
  renderLiveRecentAction(allPitches);
});

$("closeSettings").addEventListener("click",()=>$("settingsPanel").hidden=true);
const requestedSettings=params.get("settings");
if(requestedSettings){$("settingsPanel").hidden=false;if(requestedSettings==="appearance")$("appearanceSettingsSection").open=true;if(requestedSettings==="layout")$("layoutSettingsSection").open=true;}

const mlbTeamColors={108:"#ce1141",109:"#a71930",110:"#df4601",111:"#bd3039",112:"#c6011f",113:"#0e3386",114:"#33006f",115:"#c4ced4",116:"#0c2340",117:"#005a9c",118:"#002d72",119:"#ff5910",120:"#003831",121:"#002d72",133:"#003278",134:"#0c2c56",135:"#2f241d",136:"#005c5c",137:"#c41e3a",138:"#092c5c",139:"#134a8e",140:"#002b5c",141:"#00385d",142:"#27251f",143:"#e81828",144:"#fd5a1e",145:"#0d2b56",146:"#041e42",147:"#132448",158:"#12284b"};
let themeBackgroundUrl="";
function colorToRgb(hex){const value=parseInt(hex.slice(1),16);return [(value>>16)&255,(value>>8)&255,value&255];}
function rgbToColor(){return `#${[$("themeRed").value,$("themeGreen").value,$("themeBlue").value].map(v=>Math.max(0,Math.min(255,Number(v)||0)).toString(16).padStart(2,"0")).join("")}`;}
function applyTheme(theme){const color=theme.color||"#080b11";document.body.style.setProperty("--theme-bg",color);document.body.style.setProperty("--theme-shade",String((theme.shade??55)/100));document.body.style.setProperty("--theme-panel-alpha",String(1-(theme.transparency??6)/100));document.body.style.setProperty("--tile-font-scale",String((theme.fontSize??100)/100));document.body.style.setProperty("--tile-font-color",theme.fontColor||"#f4f7fb");document.body.style.setProperty("--theme-image",theme.backgroundUrl?`url("${theme.backgroundUrl}")`:"radial-gradient(circle at top,#182334,var(--theme-bg) 55%)");$("themeColor").value=color;const [r,g,b]=colorToRgb(color);$("themeRed").value=r;$("themeGreen").value=g;$("themeBlue").value=b;$("themeShade").value=theme.shade??55;$("themeTransparency").value=theme.transparency??6;$("themeFontSize").value=theme.fontSize??100;$("themeFontColor").value=theme.fontColor||"#f4f7fb";$("themeShadeValue").textContent=`${$("themeShade").value}%`;$("themeTransparencyValue").textContent=`${$("themeTransparency").value}%`;$("themeFontSizeValue").textContent=`${$("themeFontSize").value}%`;}
function currentTheme(){return {color:$("themeColor").value,shade:Number($("themeShade").value),transparency:Number($("themeTransparency").value),fontSize:Number($("themeFontSize").value),fontColor:$("themeFontColor").value,backgroundUrl:themeBackgroundUrl};}
async function loadTheme(){try{const response=await fetch("/api/theme/background",{cache:"no-store"}),data=await response.json();themeBackgroundUrl=data.url||"";}catch{}applyTheme({...JSON.parse(localStorage.getItem("vitecGameCenterTheme")||"{}"),backgroundUrl:themeBackgroundUrl});}
$("themeColor").addEventListener("input",()=>{const [r,g,b]=colorToRgb($("themeColor").value);$("themeRed").value=r;$("themeGreen").value=g;$("themeBlue").value=b;applyTheme(currentTheme());});
[$("themeRed"),$("themeGreen"),$("themeBlue")].forEach(input=>input.addEventListener("input",()=>{$("themeColor").value=rgbToColor();applyTheme(currentTheme());}));
[$("themeShade"),$("themeTransparency"),$("themeFontSize"),$("themeFontColor")].forEach(input=>input.addEventListener("input",()=>applyTheme(currentTheme())));
$("themePreset").addEventListener("change",()=>{const preset=$("themePreset").value;if(preset==="default")$("themeColor").value="#080b11";else if(preset==="away")$("themeColor").value=mlbTeamColors[currentGame?.awayTeamId]||"#080b11";else if(preset==="home")$("themeColor").value=mlbTeamColors[currentGame?.homeTeamId]||"#080b11";const [r,g,b]=colorToRgb($("themeColor").value);$("themeRed").value=r;$("themeGreen").value=g;$("themeBlue").value=b;applyTheme(currentTheme());});
$("saveTheme").addEventListener("click",()=>{localStorage.setItem("vitecGameCenterTheme",JSON.stringify(currentTheme()));$("themeStatus").textContent="Theme saved.";});
$("resetTheme").addEventListener("click",()=>{localStorage.removeItem("vitecGameCenterTheme");$("themePreset").value="default";applyTheme({color:"#080b11",shade:55,transparency:6,fontSize:100,fontColor:"#f4f7fb",backgroundUrl:themeBackgroundUrl});});
$("uploadThemeBackground").addEventListener("click",async()=>{const file=$("themeBackgroundFile").files[0];if(!file)return;const form=new FormData();form.append("background",file);const response=await fetch("/api/theme/background",{method:"POST",body:form}),body=await response.json();if(response.ok){themeBackgroundUrl=body.url;applyTheme(currentTheme());$("themeStatus").textContent="Background uploaded.";}else $("themeStatus").textContent=body.message;});
$("removeThemeBackground").addEventListener("click",async()=>{await fetch("/api/theme/background",{method:"DELETE"});themeBackgroundUrl="";applyTheme(currentTheme());$("themeStatus").textContent="Background removed.";});
loadTheme();

document.querySelectorAll(".live-detail-tab").forEach(button => {
  button.addEventListener("click", () => {
    liveDetailView = button.dataset.liveDetail || "live";
    document.querySelectorAll(".live-detail-tab").forEach(item =>
      item.classList.toggle("active", item === button));
    $("liveDetailPanel").hidden = false;
    if(liveDetailView === "scoring") raiseDashboardTile("scoring");
  });
});


$("layerLivePitches").addEventListener("change", event => {
  showLivePitchesLayer = event.target.checked;
  renderLiveGameView();
});

$("layerAtBatHeat").addEventListener("change", event => {
  if (event.target.checked) {
    heatMapLayer = "atbat";
    $("layerGameHeat").checked = false;
  } else if (heatMapLayer === "atbat") {
    heatMapLayer = "none";
  }
  renderLiveGameView();
});

$("layerGameHeat").addEventListener("change", event => {
  if (event.target.checked) {
    heatMapLayer = "game";
    $("layerAtBatHeat").checked = false;
  } else if (heatMapLayer === "game") {
    heatMapLayer = "none";
  }
  renderLiveGameView();
});

$("followLive").addEventListener("change", event => {
  followLive = event.target.checked;
  if (followLive && currentGame) syncLiveSelectors(currentGame);
});
$("pitcherSelect").addEventListener("change", () => {
  if (followLive) {
    followLive = false;
    $("followLive").checked = false;
  }
  loadScopedAnalytics();
  renderLiveStats();
});
$("batterSelect").addEventListener("change", () => {
  if (followLive) {
    followLive = false;
    $("followLive").checked = false;
  }
  loadScopedAnalytics();
});
$("pitchTypeSelect").addEventListener("change", loadScopedAnalytics);
$("vizMode").addEventListener("change", renderAnalytics);
$("scopeSelect").addEventListener("change", loadScopedAnalytics);

function setBase(id, on) {
  $(id).classList.toggle("on", Boolean(on));
}

function pitchClass(result = "") {
  const r = result.toLowerCase();
  if (r.includes("in play")) return "inplay";
  if (r.includes("ball") || r.includes("hit by pitch")) return "ball";
  if (r.includes("strike") || r.includes("foul")) return "strike";
  return "other";
}

function inningOrdinal(inning) {
  const value = Number(inning);
  if (!Number.isInteger(value) || value < 1) return "";
  const lastTwo = value % 100;
  if (lastTwo >= 11 && lastTwo <= 13) return `${value}th`;
  return `${value}${value % 10 === 1 ? "st" : value % 10 === 2 ? "nd" : value % 10 === 3 ? "rd" : "th"}`;
}

function markColor(kind) {
  return {
    strike: "#ff6b6b",
    ball: "#58d68d",
    inplay: "#66a3ff",
    other: "#b7c2d3"
  }[kind];
}

function svgEl(name, attrs = {}) {
  const el = document.createElementNS("http://www.w3.org/2000/svg", name);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  return el;
}

function formatMph(v) {
  return typeof v === "number" ? `${v.toFixed(1)} mph` : "—";
}




function teamLogoUrl(teamId) {
  return teamId ? `https://www.mlbstatic.com/team-logos/${teamId}.svg` : "";
}

function playerHeadshotUrl(playerId) {
  return playerId
    ? `https://img.mlbstatic.com/mlb-photos/image/upload/w_213,q_100/v1/people/${playerId}/headshot/67/current`
    : "";
}

function setRemoteImage(id, src, alt) {
  const img = $(id);
  if (!img) return;

  img.alt = alt || "";
  if (!src) {
    img.hidden = true;
    img.removeAttribute("src");
    return;
  }

  if (img.dataset.src === src) return;
  img.dataset.src = src;
  img.hidden = false;

  img.onerror = () => {
    img.hidden = true;
  };

  img.onload = () => {
    img.hidden = false;
  };

  img.src = src;
}



function formatStat(v, digits = 1) {
  return typeof v === "number" ? v.toFixed(digits) : "—";
}

function renderGameContext(game) {
  const seriesParts = [];
  if (game.seriesDescription) seriesParts.push(game.seriesDescription);
  if (game.seriesGameNumber && game.gamesInSeries)
    seriesParts.push(`Game ${game.seriesGameNumber} of ${game.gamesInSeries}`);
  $("seriesContext").textContent = seriesParts.join(" · ") || "Regular Season";

  $("venueContext").textContent = game.venue || "";

  const scheduleParts = [];
  if (game.scheduledStart) scheduleParts.push(`${game.scheduledStart} start`);
  if (game.dayNight) scheduleParts.push(game.dayNight === "night" ? "Night Game" : "Day Game");
  if (game.scheduledInnings) scheduleParts.push(`${game.scheduledInnings} innings`);
  if (game.doubleHeader && game.doubleHeader.toUpperCase() !== "N")
    scheduleParts.push("Doubleheader");
  $("scheduleContext").textContent = scheduleParts.join(" · ");

  const weatherParts = [];
  if (typeof game.weatherTempF === "number") weatherParts.push(`${game.weatherTempF}°F`);
  if (game.weatherCondition) weatherParts.push(game.weatherCondition);
  if (game.weatherWind) weatherParts.push(`Wind ${game.weatherWind}`);
  $("weatherContext").textContent =
    weatherParts.length ? `Game Weather: ${weatherParts.join(" · ")}` : "Game Weather: —";
}

function renderRunnerMovement(game) {
  const e = game?.lastEvent;
  if (!e) {
    $("runnerMovement").textContent = "";
    return;
  }

  const parts = [];
  if (e.startBase || e.endBase)
    parts.push(`Runner: ${e.startBase || "—"} → ${e.endBase || "—"}`);
  if (e.runnerScored) parts.push("Scored");
  if (e.runnerRbi) parts.push("RBI");
  $("runnerMovement").textContent = parts.join(" · ");
}

function showStructuredEvent(game) {
  const e = game?.lastEvent;
  if (!e) return false;

  const eventType = (e.eventType || "").toLowerCase();
  const eventName = (e.event || "").toLowerCase();
  const description = e.description || game.lastPlay || "";
  const player = e.batter || game?.matchup?.batter || "";

  let type = "";
  let title = "";
  let duration = 5000;

  if (eventType === "home_run" || eventName === "home run") {
    type = "home-run"; title = e.rbi >= 4 ? "GRAND SLAM" : "HOME RUN"; duration = 8500;
  } else if (e.isScoringPlay) {
    type = "scoring"; title = e.rbi > 1 ? `${e.rbi}-RUN PLAY` : "RUN SCORES"; duration = 6500;
  } else if (eventType.includes("triple")) {
    type = "scoring"; title = "TRIPLE"; duration = 5000;
  } else if (eventType.includes("double")) {
    type = "scoring"; title = "DOUBLE"; duration = 4500;
  } else if (eventType.includes("stolen_base")) {
    type = "scoring"; title = "STOLEN BASE"; duration = 4200;
  } else if (eventType.includes("strikeout")) {
    type = "pitching-change"; title = "STRIKEOUT"; duration = 3500;
  } else if (eventType.includes("double_play")) {
    type = "pitching-change"; title = "DOUBLE PLAY"; duration = 4200;
  } else if ((e.captivatingIndex ?? 0) >= 80) {
    type = "scoring"; title = (e.event || "BIG PLAY").toUpperCase(); duration = 5000;
  }

  if (!title) return false;

  showEventOverlay(type, title, player, description, duration);

  const statcast = $("eventStatcast");
  const hasStatcast =
    type === "home-run" &&
    (typeof e.exitVelocity === "number" ||
     typeof e.launchAngle === "number" ||
     typeof e.distanceFeet === "number");

  statcast.hidden = !hasStatcast;
  $("eventExitVelo").textContent = typeof e.exitVelocity === "number" ? `${e.exitVelocity.toFixed(1)} mph` : "—";
  $("eventLaunchAngle").textContent = typeof e.launchAngle === "number" ? `${e.launchAngle.toFixed(0)}°` : "—";
  $("eventDistance").textContent = typeof e.distanceFeet === "number" ? `${e.distanceFeet.toFixed(0)} ft` : "—";

  return true;
}

function hideEventOverlay() {
  if (eventHideTimer) {
    clearTimeout(eventHideTimer);
    eventHideTimer = null;
  }
  $("eventOverlay").hidden = true;
}

function showStadiumBoardMessage(message, type, durationMs) {
  const board = $("stadiumBoardMessage");
  if (!board) return;
  if (stadiumBoardTimer) clearTimeout(stadiumBoardTimer);
  board.textContent = message;
  board.className = `stadium-board-message ${type}`;
  board.hidden = false;
  stadiumBoardTimer = setTimeout(() => {
    board.hidden = true;
    stadiumBoardTimer = null;
  }, durationMs);
}

function lastScoringPlayBelongsToHomeTeam(game) {
  const plays = game?.scoringPlays;
  if (!Array.isArray(plays) || !plays.length) return false;
  return String(plays[plays.length - 1]?.halfInning || "").toLowerCase() === "bottom";
}

function updateStadiumBoardCelebration(game) {
  const eventType = String(game?.lastEvent?.eventType || "").toLowerCase();
  const eventName = String(game?.lastEvent?.event || "").toLowerCase();
  const isHomeRun = eventType === "home_run" || eventName === "home run";
  const eventChanged = Boolean(game?.lastPlay) && game.lastPlay !== previousLastPlay;

  const homeRunTriggered = eventChanged && isHomeRun && lastScoringPlayBelongsToHomeTeam(game);
  if (homeRunTriggered) {
    showStadiumBoardMessage("HOME RUN", "home-run", 8500);
  }

  const status = String(game?.status || game?.detailedStatus || "").toLowerCase();
  const isFinal = status.includes("final");
  if (isFinal && !previousFinalState && (game?.homeScore ?? 0) > (game?.awayScore ?? 0)) {
    if (homeRunTriggered) {
      setTimeout(() => showStadiumBoardMessage("WIN", "win", 12000), 8700);
    } else {
      showStadiumBoardMessage("WIN", "win", 12000);
    }
  }
  previousFinalState = isFinal;
}

function inferPlayerFromDescription(description, fallback = "") {
  if (!description) return fallback;
  const verbs = [
    " homers", " doubles", " triples", " singles", " strikes out",
    " steals", " grounds into", " hits"
  ];
  const lower = description.toLowerCase();
  for (const verb of verbs) {
    const i = lower.indexOf(verb);
    if (i > 0) return description.slice(0, i).trim();
  }
  return fallback;
}

function findPlayerIdByName(name) {
  if (!name) return null;

  if (currentGame?.matchup?.batter === name) return currentGame.matchup.batterId;
  if (currentGame?.matchup?.pitcher === name) return currentGame.matchup.pitcherId;

  const pitch = [...allPitches].reverse().find(p => p.batter === name || p.pitcher === name);
  if (!pitch) return null;
  if (pitch.batter === name) return pitch.batterId;
  if (pitch.pitcher === name) return pitch.pitcherId;
  return null;
}

function showEventOverlay(type, title, player, description, durationMs = 6500) {
  const overlay = $("eventOverlay");
  overlay.className = `event-overlay ${type} animate-in`;
  $("eventStatcast").hidden = true;

  $("eventTitle").textContent = title;
  $("eventPlayer").textContent = player || "";
  $("eventDescription").textContent = description || "";

  const away = currentGame?.awayTeam || "Away";
  const home = currentGame?.homeTeam || "Home";
  const awayScore = currentGame?.awayScore ?? 0;
  const homeScore = currentGame?.homeScore ?? 0;
  $("eventScore").textContent = `${away} ${awayScore} – ${homeScore} ${home}`;

  const playerId = findPlayerIdByName(player);
  setRemoteImage("eventPlayerPhoto", playerHeadshotUrl(playerId), player ? `${player} headshot` : "");

  $("eventKicker").textContent =
    type === "home-run" ? "🔥 BIG PLAY 🔥" :
    type === "scoring" ? "SCORING PLAY" :
    type === "pitching-change" ? "PITCHING CHANGE" :
    "GAME EVENT";

  overlay.hidden = false;

  if (eventHideTimer) clearTimeout(eventHideTimer);
  eventHideTimer = setTimeout(hideEventOverlay, durationMs);
}

function detectLiveEvent(game) {
  const description = game?.lastPlay || "";
  const normalized = description.toLowerCase();

  // MLB can briefly omit the matchup between innings and can serialize the same
  // player ID as either a string or number. Keep the last confirmed pitcher and
  // compare a normalized identity so an alert only appears for a new player.
  const pitcherId = game?.matchup?.pitcherId;
  const pitcherName = String(game?.matchup?.pitcher || "").trim();
  const pitcherIdentity = pitcherId != null && String(pitcherId).trim()
    ? `id:${String(pitcherId).trim()}`
    : pitcherName
      ? `name:${pitcherName.toLocaleLowerCase("en-US")}`
      : null;
  if (previousPitcherIdentity && pitcherIdentity && pitcherIdentity !== previousPitcherIdentity) {
    const pitcher = game?.matchup?.pitcher || "New pitcher";
    showEventOverlay(
      "pitching-change",
      "PITCHING CHANGE",
      pitcher,
      `${pitcher} is now pitching.`,
      5000
    );
  }
  if (pitcherIdentity) previousPitcherIdentity = pitcherIdentity;

  if (!description || description === previousLastPlay) return;
  previousLastPlay = description;

  if (showStructuredEvent(game)) return;

  const player = inferPlayerFromDescription(description, game?.matchup?.batter || "");

  if (normalized.includes("home run") || normalized.includes("homers")) {
    showEventOverlay("home-run", "HOME RUN", player, description, 8000);
    return;
  }

  if (normalized.includes("grand slam")) {
    showEventOverlay("home-run", "GRAND SLAM", player, description, 9000);
    return;
  }

  if (
    normalized.includes("scores") ||
    normalized.includes("scoring play") ||
    normalized.includes("rbi")
  ) {
    showEventOverlay("scoring", "RUN SCORES", player, description, 6000);
    return;
  }

  if (normalized.includes("triples")) {
    showEventOverlay("scoring", "TRIPLE", player, description, 5000);
    return;
  }

  if (normalized.includes("doubles")) {
    showEventOverlay("scoring", "DOUBLE", player, description, 4500);
    return;
  }

  if (normalized.includes("steals")) {
    showEventOverlay("scoring", "STOLEN BASE", player, description, 4000);
    return;
  }

  if (normalized.includes("double play")) {
    showEventOverlay("pitching-change", "DOUBLE PLAY", player, description, 4000);
    return;
  }

  if (normalized.includes("strikes out")) {
    showEventOverlay("pitching-change", "STRIKEOUT", player, description, 3500);
  }
}

function setLiveBase(id, on) {
  $(id).classList.toggle("on", Boolean(on));
}

function normalizeDegrees(value) {
  return ((value % 360) + 360) % 360;
}

function solarUtcHour(year, month, day, latitude, longitude, sunrise) {
  const start = Date.UTC(year, 0, 1);
  const current = Date.UTC(year, month - 1, day);
  const dayOfYear = Math.floor((current - start) / 86400000) + 1;
  const longitudeHour = longitude / 15;
  const approximateTime = dayOfYear + ((sunrise ? 6 : 18) - longitudeHour) / 24;
  const meanAnomaly = (0.9856 * approximateTime) - 3.289;
  let trueLongitude = meanAnomaly + (1.916 * Math.sin(meanAnomaly * Math.PI / 180)) + (0.020 * Math.sin(2 * meanAnomaly * Math.PI / 180)) + 282.634;
  trueLongitude = normalizeDegrees(trueLongitude);
  let rightAscension = Math.atan(0.91764 * Math.tan(trueLongitude * Math.PI / 180)) * 180 / Math.PI;
  rightAscension = normalizeDegrees(rightAscension);
  rightAscension += Math.floor(trueLongitude / 90) * 90 - Math.floor(rightAscension / 90) * 90;
  rightAscension /= 15;
  const sinDeclination = 0.39782 * Math.sin(trueLongitude * Math.PI / 180);
  const cosDeclination = Math.cos(Math.asin(sinDeclination));
  const zenith = 90.833 * Math.PI / 180;
  const latitudeRadians = latitude * Math.PI / 180;
  const cosHourAngle = (Math.cos(zenith) - sinDeclination * Math.sin(latitudeRadians)) / (cosDeclination * Math.cos(latitudeRadians));
  if (cosHourAngle < -1 || cosHourAngle > 1) return null;
  let hourAngle = Math.acos(cosHourAngle) * 180 / Math.PI;
  if (sunrise) hourAngle = 360 - hourAngle;
  hourAngle /= 15;
  const localMeanTime = hourAngle + rightAscension - (0.06571 * approximateTime) - 6.622;
  return ((localMeanTime - longitudeHour) % 24 + 24) % 24;
}

function venueDateParts(game, now) {
  try {
    const parts = new Intl.DateTimeFormat("en-US", {
      timeZone: game.venueTimeZone || undefined,
      year: "numeric", month: "numeric", day: "numeric"
    }).formatToParts(now);
    const value = type => Number(parts.find(part => part.type === type)?.value);
    return { year:value("year"), month:value("month"), day:value("day") };
  } catch {
    return { year:now.getUTCFullYear(), month:now.getUTCMonth() + 1, day:now.getUTCDate() };
  }
}

function isDaylightAtVenue(game, now = new Date()) {
  const latitude = Number(game?.venueLatitude);
  const longitude = Number(game?.venueLongitude);
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return String(game?.dayNight || "").toLowerCase() !== "night";
  const date = venueDateParts(game, now);
  const rise = solarUtcHour(date.year, date.month, date.day, latitude, longitude, true);
  const set = solarUtcHour(date.year, date.month, date.day, latitude, longitude, false);
  if (rise === null || set === null) return String(game?.dayNight || "").toLowerCase() !== "night";
  const utcHour = now.getUTCHours() + now.getUTCMinutes() / 60 + now.getUTCSeconds() / 3600;
  return rise <= set ? utcHour >= rise && utcHour < set : utcHour >= rise || utcHour < set;
}

function renderStadiumScene(game, matchup) {
  const field = document.querySelector(".combined-matchup-panel .mini-zone-wrap");
  if (field) {
    const daylight = isDaylightAtVenue(game);
    field.classList.toggle("pitch-field-day", daylight);
    field.classList.toggle("pitch-field-night", !daylight);
  }
  const batter = $("stadiumBatter");
  if (!batter) return;
  batter.src = "/assets/gamecenter/batter-generic.png";
  const batsLeft = String(matchup?.batSide || "R").toUpperCase().startsWith("L");
  batter.className = `stadium-batter ${batsLeft ? "bats-left" : "bats-right"}`;
}

function renderLiveGameView() {
  if (!currentGame) return;

  renderGameContext(currentGame);
  renderRunnerMovement(currentGame);

  const matchup = currentGame.matchup || {};
  renderStadiumScene(currentGame, matchup);
  $("liveBatter").textContent = matchup.batter || "—";
  setRemoteImage("liveBatterPhoto", playerHeadshotUrl(matchup.batterId), `${matchup.batter || "Batter"} headshot`);
  $("liveBatterSide").textContent = matchup.batSide ? `Bats ${matchup.batSide}` : "—";
  $("liveBatterMeta").textContent = [
    matchup.batterPosition,
    matchup.batterJerseyNumber ? `#${matchup.batterJerseyNumber}` : "",
    matchup.batterHeight,
    matchup.batterWeight ? `${matchup.batterWeight} lb` : ""
  ].filter(Boolean).join(" · ") || "—";
  $("liveBatterStats").textContent = `AVG ${matchup.batterAverage || "—"} · HR ${matchup.batterHomeRuns || "—"} · RBI ${matchup.batterRbi || "—"}`;
  $("livePitcher").textContent = matchup.pitcher || "—";
  setRemoteImage("livePitcherPhoto", playerHeadshotUrl(matchup.pitcherId), `${matchup.pitcher || "Pitcher"} headshot`);
  $("livePitcherHand").textContent = matchup.pitchHand ? `${matchup.pitchHand}HP` : "—";
  $("livePitcherMeta").textContent = [
    matchup.pitcherPosition,
    matchup.pitcherJerseyNumber ? `#${matchup.pitcherJerseyNumber}` : "",
    matchup.pitcherHeight,
    matchup.pitcherWeight ? `${matchup.pitcherWeight} lb` : ""
  ].filter(Boolean).join(" · ") || "—";
  const pitcherRecord = matchup.pitcherWins || matchup.pitcherLosses
    ? `${matchup.pitcherWins || "0"}-${matchup.pitcherLosses || "0"}`
    : "—";
  $("livePitcherStats").textContent = `W-L ${pitcherRecord} · ERA ${matchup.pitcherEra || "—"} · SO ${matchup.pitcherStrikeouts || "—"}`;

  $("liveBalls").textContent = currentGame.balls ?? 0;
  $("liveStrikes").textContent = currentGame.strikes ?? 0;
  $("liveOuts").textContent = currentGame.outs ?? 0;

  setLiveBase("liveBaseFirst", currentGame.bases?.first);
  setLiveBase("liveBaseSecond", currentGame.bases?.second);
  setLiveBase("liveBaseThird", currentGame.bases?.third);

  $("liveLastPlay").textContent = currentGame.lastPlay || "Waiting for play data…";

  const state = (currentGame.status || currentGame.detailedStatus || "").toLowerCase();
  $("liveStateBadge").textContent = state.includes("final") ? "FINAL" : state.includes("progress") ? "LIVE" : "GAME";

  const pitcher = matchup.pitcher || "";
  const batter = matchup.batter || "";
  const pair = allPitches.filter(p => (!pitcher || p.pitcher === pitcher) && (!batter || p.batter === batter));

  const last = allPitches.length ? allPitches[allPitches.length - 1] : null;
  $("liveLastPitch").textContent = last
    ? `${last.pitchCode || "—"} · ${formatMph(last.startSpeedMph)}`
    : "—";
  $("liveLastPitchResult").textContent = last?.result || "—";
  $("livePitchCount").textContent = `${pair.length} matchup pitches`;

  $("centerBatterName").textContent = matchup.batter || "—";
  $("centerPitcherName").textContent = matchup.pitcher || "—";
  setRemoteImage("centerBatterPhoto", playerHeadshotUrl(matchup.batterId), `${matchup.batter || "Batter"} headshot`);
  setRemoteImage("centerPitcherPhoto", playerHeadshotUrl(matchup.pitcherId), `${matchup.pitcher || "Pitcher"} headshot`);

  renderLiveMiniZone(pair);
  renderLiveRecentAction(pair);
  renderBoxScore(currentGame.boxScore);
  renderLineScore(currentGame);
  renderScoringPlays(currentGame.scoringPlays);
}

function boxScoreTable(title, columns, rows) {
  const section = document.createElement("section");
  section.className = "box-score-table-wrap";
  const heading = document.createElement("h4");
  heading.textContent = title;
  const table = document.createElement("table");
  table.className = "box-score-table";
  const head = document.createElement("thead");
  const headRow = document.createElement("tr");
  columns.forEach(column => {
    const th = document.createElement("th");
    th.textContent = column.label;
    headRow.appendChild(th);
  });
  head.appendChild(headRow);
  const body = document.createElement("tbody");
  rows.forEach(row => {
    const tr = document.createElement("tr");
    columns.forEach(column => {
      const td = document.createElement("td");
      td.textContent = column.key === "name" && row.role ? `${row.name}, ${row.role}` : row[column.key] ?? "—";
      tr.appendChild(td);
    });
    body.appendChild(tr);
  });
  table.append(head, body);
  section.append(heading, table);
  return section;
}

function renderBoxScore(boxScore) {
  if (!boxScore) return;
  const battingColumns = [
    { key: "name", label: "Batter" }, { key: "position", label: "Pos" },
    { key: "atBats", label: "AB" }, { key: "runs", label: "R" },
    { key: "hits", label: "H" }, { key: "rbi", label: "RBI" },
    { key: "walks", label: "BB" }, { key: "strikeouts", label: "SO" },
    { key: "average", label: "AVG" }, { key: "homeRuns", label: "HR" }
  ];
  const pitchingColumns = [
    { key: "name", label: "Pitcher" }, { key: "inningsPitched", label: "IP" },
    { key: "hits", label: "H" }, { key: "runs", label: "R" },
    { key: "earnedRuns", label: "ER" }, { key: "walks", label: "BB" },
    { key: "strikeouts", label: "SO" }, { key: "era", label: "ERA" },
    { key: "pitchCount", label: "PC" }
  ];
  [[boxScore.away,$("awayBoxScoreContent"),$("awayBoxScoreTitle")],[boxScore.home,$("homeBoxScoreContent"),$("homeBoxScoreTitle")]].forEach(([team,content,title]) => {
    if (!content) return;
    content.replaceChildren();
    const logo=document.createElement("img");logo.className="box-score-team-logo";logo.src=`https://www.mlbstatic.com/team-logos/${team.teamId}.svg`;logo.alt=`${team.teamName} logo`;
    const name=document.createElement("span");name.textContent=team.teamName;title.replaceChildren(logo,name);
    const teamSection = document.createElement("div");
    teamSection.className = "team-box-score";
    const teamName = document.createElement("h3");
    teamName.textContent = team.teamName;
    const highlights = document.createElement("div");
    highlights.className = "box-score-highlights";
    const highlightSections = ["Batting", "Baserunning", "Fielding"];
    highlightSections.forEach(sectionName => {
      const wantedLabels = sectionName === "Batting"
        ? new Set(["2B", "3B", "RBI", "HR"])
        : sectionName === "Baserunning" ? new Set(["SB"]) : null;
      const items = (team.highlights || []).filter(item =>
        item.section === sectionName && (!wantedLabels || wantedLabels.has(String(item.label || "").toUpperCase()))
      );
      if (!items.length) return;
      const section = document.createElement("section");
      section.className = "box-score-highlight-section";
      const heading = document.createElement("h4");
      heading.textContent = sectionName;
      section.appendChild(heading);
      items.forEach(item => {
        const line = document.createElement("div");
        const label = document.createElement("strong");
        label.textContent = `${item.label} — `;
        line.append(label, document.createTextNode(item.value || "—"));
        section.appendChild(line);
      });
      highlights.appendChild(section);
    });
    const battingTable = boxScoreTable("Batting", battingColumns, team.batting || []);
    const pitchingTable = boxScoreTable("Pitching", pitchingColumns, team.pitching || []);
    teamSection.append(teamName, battingTable, highlights, pitchingTable);
    content.appendChild(teamSection);
  });
}

function renderLineScore(game) {
  const content=$("lineScoreContent"), score=game?.lineScore;
  if(!content || !score) return;
  const table=document.createElement("table"); table.className="line-score-table";
  const innings=(score.innings||[]).length ? score.innings : Array.from({length:Math.max(9,Number(game.inning)||0)},(_,i)=>({inning:i+1,awayRuns:null,homeRuns:null}));
  const head=document.createElement("tr"); ["Team",...innings.map(x=>x.inning),"R","H","E"].forEach(value=>{const th=document.createElement("th");th.textContent=value;head.append(th);});
  const thead=document.createElement("thead");thead.append(head);table.append(thead);
  const body=document.createElement("tbody");
  [[game.awayTeam,innings.map(x=>x.awayRuns),score.awayRuns,score.awayHits,score.awayErrors],[game.homeTeam,innings.map(x=>x.homeRuns),score.homeRuns,score.homeHits,score.homeErrors]].forEach(([name,runs,total,hits,errors])=>{
    const row=document.createElement("tr"); [name,...runs.map(value=>value??"-"),total,hits,errors].forEach((value,index)=>{const td=document.createElement("td");td.textContent=value; if(index>innings.length)td.className="line-score-total";row.append(td);});body.append(row);
  });
  table.append(body);content.replaceChildren(table);
}

function renderScoringPlays(plays) {
  const list = $("scoringPlaysList");
  if (!list) return;
  list.replaceChildren();

  (plays || []).forEach(play => {
    const item = document.createElement("article");
    item.className = "scoring-play-item";
    const inning = document.createElement("div");
    inning.className = "scoring-play-inning";
    const half = String(play.halfInning || "").toLowerCase() === "bottom" ? "Bottom" : "Top";
    inning.textContent = `${half} ${inningOrdinal(play.inning)}`;
    const logo=document.createElement("img");logo.className="scoring-team-logo";const teamId=half==="Bottom"?currentGame?.homeTeamId:currentGame?.awayTeamId;logo.src=`https://www.mlbstatic.com/team-logos/${teamId}.svg`;logo.alt=`${half==="Bottom"?currentGame?.homeTeam:currentGame?.awayTeam} logo`;inning.append(logo);
    const content = document.createElement("div");
    const title = document.createElement("strong");
    title.textContent = play.event || play.batter || "Scoring play";
    const description = document.createElement("p");
    description.textContent = play.description || "Run scored.";
    content.append(title, description);
    const score = document.createElement("div");
    score.className = "scoring-play-score";
    score.textContent = `${currentGame?.awayAbbreviation || "Away"} ${play.awayScore} · ${currentGame?.homeAbbreviation || "Home"} ${play.homeScore}`;
    item.append(inning, content, score);
    list.appendChild(item);
  });

  if (!(plays || []).length) {
    const empty = document.createElement("div");
    empty.className = "muted scoring-empty";
    empty.textContent = "No scoring plays yet.";
    list.appendChild(empty);
  }
}

function renderLiveRecentAction(pitches) {
  const list = $("liveRecentAction");
  list.replaceChildren();

  pitches.slice(-recentActionCount).reverse().forEach(p => {
    const item = document.createElement("div");
    item.className = "live-action-item";
    const result=String(p.result||"").toLowerCase(),battingAction=result.includes("in play")||result.includes("hit")||result.includes("home run");
    if(result.includes("home run")||result.includes("run scores")||result.includes("run(s)")||result.includes("scores")) item.classList.add("scoring-action");
    const teams=[currentGame?.boxScore?.away,currentGame?.boxScore?.home],playerId=battingAction?p.batterId:p.pitcherId,type=battingAction?"batting":"pitching";
    const actingTeam=teams.find(team=>(team?.[type]||[]).some(player=>Number(player.playerId)===Number(playerId)));
    const logo=document.createElement("img");logo.className="recent-action-team-logo";logo.src=`https://www.mlbstatic.com/team-logos/${actingTeam?.teamId||currentGame?.homeTeamId}.svg`;logo.alt=`${actingTeam?.teamName||"Team"} logo`;
    const text=document.createElement("div");text.className="live-action-text";

    const title = document.createElement("strong");
    title.textContent = `${p.pitchCode || "—"} · ${formatMph(p.startSpeedMph)} · ${p.result || "Pitch"}`;

    const meta = document.createElement("span");
    meta.textContent = `${p.pitcher || ""} → ${p.batter || ""}`;

    text.append(title,meta);item.append(logo,text);
    list.appendChild(item);
  });

  if (!pitches.length) {
    const empty = document.createElement("div");
    empty.className = "muted";
    empty.textContent = "Waiting for current matchup pitch data…";
    list.appendChild(empty);
  }
  list.dataset.cycleIndex = "0";
  list.scrollTo({ top: 0, behavior: "instant" });
}

function cycleRecentActions() {
  const list = $("liveRecentAction");
  const actions = [...list.querySelectorAll(".live-action-item")];
  if (actions.length < 2 || list.scrollHeight <= list.clientHeight + 2) return;
  const nextIndex = (Number(list.dataset.cycleIndex || 0) + 1) % actions.length;
  list.dataset.cycleIndex = String(nextIndex);
  list.scrollTo({ top: actions[nextIndex].offsetTop - list.offsetTop, behavior: "smooth" });
}

function findPlayerGameLine(playerId,type){for(const team of [currentGame?.boxScore?.away,currentGame?.boxScore?.home]){const row=(team?.[type]||[]).find(item=>Number(item.playerId)===Number(playerId));if(row)return row;}return null;}
function openPlayerStats(kind){
  const matchup=currentGame?.currentMatchup||{},batter=kind==="batter",playerId=batter?matchup.batterId:matchup.pitcherId;if(!playerId)return;
  document.querySelector(".player-stats-window")?.remove();const line=findPlayerGameLine(playerId,batter?"batting":"pitching"),name=batter?matchup.batter:matchup.pitcher,photo=batter?$("liveBatterPhoto").src:$("livePitcherPhoto").src;
  const game=line?(batter?`AB ${line.atBats} · R ${line.runs} · H ${line.hits} · RBI ${line.rbi} · HR ${line.homeRuns}`:`${line.role||"P"} · IP ${line.inningsPitched} · H ${line.hits} · ER ${line.earnedRuns} · SO ${line.strikeouts} · PC ${line.pitchCount}`):"No current-game line available.";
  const season=batter?`AVG ${matchup.batterAverage||"—"} · HR ${matchup.batterHomeRuns||"—"} · RBI ${matchup.batterRbi||"—"}`:`W-L ${matchup.pitcherWins||0}-${matchup.pitcherLosses||0} · ERA ${matchup.pitcherEra||"—"} · SO ${matchup.pitcherStrikeouts||"—"}`;
  const bio=batter?`${matchup.batterPosition||"Batter"} · #${matchup.batterJerseyNumber||"—"} · Bats ${matchup.batterBatSide||"—"} · ${matchup.batterHeight||"—"} · ${matchup.batterWeight||"—"} lb`:`Pitcher · #${matchup.pitcherJerseyNumber||"—"} · Throws ${matchup.pitcherThrowHand||"—"} · ${matchup.pitcherHeight||"—"} · ${matchup.pitcherWeight||"—"} lb`;
  const win=document.createElement("section");win.className="player-stats-window";win.innerHTML=`<header><strong>${name||"Player"}</strong><button type="button" aria-label="Close player statistics">×</button></header><div class="player-stats-body"><img src="${photo}" alt="${name||"Player"}"><div><p>${bio}</p><h3>Current Game</h3><p>${game}</p><h3>Current Season</h3><p>${season}</p></div></div>`;document.body.append(win);win.querySelector("button").onclick=()=>win.remove();
  const header=win.querySelector("header");header.addEventListener("pointerdown",event=>{if(event.target.closest("button"))return;event.preventDefault();const rect=win.getBoundingClientRect(),sx=event.clientX,sy=event.clientY;const move=e=>{win.style.left=`${Math.max(0,rect.left+e.clientX-sx)}px`;win.style.top=`${Math.max(0,rect.top+e.clientY-sy)}px`;win.style.right="auto"};const up=()=>{window.removeEventListener("pointermove",move);window.removeEventListener("pointerup",up)};window.addEventListener("pointermove",move);window.addEventListener("pointerup",up)});
}
$("liveBatterPhoto").addEventListener("click",()=>openPlayerStats("batter"));
$("livePitcherPhoto").addEventListener("click",()=>openPlayerStats("pitcher"));

function renderLiveMiniZone(pitches) {
  const grid = $("liveMiniGrid");
  const marks = $("liveMiniMarks");
  const heat = $("liveMiniHeat");

  grid.replaceChildren();
  marks.replaceChildren();
  heat.replaceChildren();

  const matchup = currentGame?.matchup || {};
  const pitcher = matchup.pitcher || "";
  const batter = matchup.batter || "";

  const atBatSample = pitches;
  const gameSample = allPitches.filter(p =>
    (!pitcher || p.pitcher === pitcher) &&
    (!batter || p.batter === batter));

  const heatSample =
    heatMapLayer === "game" ? gameSample :
    heatMapLayer === "atbat" ? atBatSample :
    [];

  // Strike-zone geometry uses the freshest valid pitch from either sample.
  const geometrySample = gameSample.length ? gameSample : atBatSample;
  const geometryValid = geometrySample.filter(p =>
    typeof p.plateX === "number" &&
    typeof p.plateZ === "number");

  const latest = [...geometryValid].reverse().find(p =>
    typeof p.strikeZoneTop === "number" &&
    typeof p.strikeZoneBottom === "number");

  const zoneTop = latest?.strikeZoneTop ?? 3.5;
  const zoneBottom = latest?.strikeZoneBottom ?? 1.5;
  const halfWidth = 17 / 24;

  const xMin=-2, xMax=2, zMin=.5, zMax=4.6;
  const left=35, right=285, top=20, bottom=335;
  const px=x => left + ((x-xMin)/(xMax-xMin))*(right-left);
  const pz=z => bottom - ((z-zMin)/(zMax-zMin))*(bottom-top);

  const zl=px(-halfWidth), zr=px(halfWidth), zt=pz(zoneTop), zb=pz(zoneBottom);
  const zw=zr-zl, zh=zb-zt;

  // Heat-map layer is drawn FIRST.
  if (heatMapLayer !== "none") {
    const validHeat = heatSample.filter(p =>
      typeof p.plateX === "number" &&
      typeof p.plateZ === "number");

    const cols = 7;
    const rows = 9;
    const cells = Array.from({length:rows}, () => Array(cols).fill(0));
    let maxCount = 0;

    validHeat.forEach(p => {
      const xi = Math.floor((p.plateX - xMin) / (xMax - xMin) * cols);
      const zi = Math.floor((p.plateZ - zMin) / (zMax - zMin) * rows);
      const cx = Math.max(0, Math.min(cols - 1, xi));
      const cz = Math.max(0, Math.min(rows - 1, zi));
      cells[cz][cx] += 1;
      maxCount = Math.max(maxCount, cells[cz][cx]);
    });

    const cellW = (right-left)/cols;
    const cellH = (bottom-top)/rows;

    for (let rz=0; rz<rows; rz++) {
      for (let cx=0; cx<cols; cx++) {
        const count = cells[rz][cx];
        if (!count) continue;

        const opacity = maxCount ? 0.14 + 0.58*(count/maxCount) : 0.18;
        heat.appendChild(svgEl("rect", {
          x:left+cx*cellW,
          y:bottom-(rz+1)*cellH,
          width:cellW,
          height:cellH,
          fill:`rgba(255,105,78,${opacity.toFixed(2)})`,
          class:"heat-cell"
        }));
      }
    }
  }

  // Strike zone/grid sits over the heat map.
  grid.appendChild(svgEl("rect", {
    x:zl,y:zt,width:zw,height:zh,
    fill:"none",stroke:"#f4f7fb","stroke-width":"2.5"
  }));

  for (let i=1;i<=2;i++) {
    grid.appendChild(svgEl("line", {
      x1:zl+zw*i/3,y1:zt,x2:zl+zw*i/3,y2:zb,
      stroke:"#52637c","stroke-width":"1"
    }));
    grid.appendChild(svgEl("line", {
      x1:zl,y1:zt+zh*i/3,x2:zr,y2:zt+zh*i/3,
      stroke:"#52637c","stroke-width":"1"
    }));
  }

  // Live pitch markers are drawn LAST, on top of the heat map.
  if (showLivePitchesLayer) {
    const validLive = atBatSample.filter(p =>
      typeof p.plateX === "number" &&
      typeof p.plateZ === "number");

    validLive.slice(-12).forEach((p,index,arr) => {
      const kind = pitchClass(p.result);
      marks.appendChild(svgEl("circle", {
        cx:px(p.plateX),
        cy:pz(p.plateZ),
        r:index===arr.length-1 ? "7":"5",
        fill:markColor(kind),
        stroke:index===arr.length-1 ? "#fff":"#080d15",
        "stroke-width":index===arr.length-1 ? "2.5":"1.2"
      }));
    });
  }

  const notes = [];
  if (showLivePitchesLayer) notes.push("Live pitches");
  if (heatMapLayer === "atbat") notes.push("At-bat heat map");
  if (heatMapLayer === "game") notes.push("Game matchup heat map");

  $("matchupViewNote").textContent =
    notes.length ? notes.join(" + ") : "Strike zone only";
}

function syncLiveSelectors(game) {
  if (!followLive || !game?.matchup) return;

  const pitcher = game.matchup.pitcher || "";
  const batter = game.matchup.batter || "";

  if (pitcher && [...$("pitcherSelect").options].some(o => o.value === pitcher)) {
    $("pitcherSelect").value = pitcher;
  }

  if (batter && [...$("batterSelect").options].some(o => o.value === batter)) {
    $("batterSelect").value = batter;
  }
}

function renderLiveStats() {
  const pitcher = currentGame?.matchup?.pitcher || $("pitcherSelect").value;
  const pitcherPitches = allPitches.filter(p => !pitcher || p.pitcher === pitcher);

  const lastPitch = allPitches.length ? allPitches[allPitches.length - 1] : null;
  $("lastPitchType").textContent = lastPitch
    ? `${lastPitch.pitchCode || "—"} · ${formatMph(lastPitch.startSpeedMph)}`
    : "—";
  $("lastPitchDetail").textContent = lastPitch?.result || "—";

  $("pitcherPitchCount").textContent =
    `${pitcherPitches.length} pitch${pitcherPitches.length === 1 ? "" : "es"}`;

  const speeds = pitcherPitches
    .map(p => p.startSpeedMph)
    .filter(v => typeof v === "number");

  if (speeds.length) {
    const avg = speeds.reduce((a,b) => a+b, 0) / speeds.length;
    const max = Math.max(...speeds);
    $("pitcherVelocity").textContent = `Avg ${avg.toFixed(1)} mph · Max ${max.toFixed(1)} mph`;
  } else {
    $("pitcherVelocity").textContent = "Avg — · Max —";
  }

  const mix = new Map();
  pitcherPitches.forEach(p => {
    const code = p.pitchCode || "UNK";
    mix.set(code, (mix.get(code) || 0) + 1);
  });

  const mixText = [...mix.entries()]
    .sort((a,b) => b[1] - a[1])
    .slice(0, 5)
    .map(([code,count]) => {
      const pct = pitcherPitches.length ? Math.round(count/pitcherPitches.length*100) : 0;
      return `${code} ${pct}%`;
    })
    .join(" · ");

  $("pitchMix").textContent = mixText || "—";
}

function renderScoreChange(game) {
  const key = `${game.awayScore}-${game.homeScore}`;

  if (previousScoreKey && key !== previousScoreKey) {
    const alert = $("scoringAlert");
    alert.textContent = `Score update: ${game.awayTeam} ${game.awayScore} – ${game.homeScore} ${game.homeTeam}`;
    alert.hidden = false;
    alert.classList.remove("flash");
    void alert.offsetWidth;
    alert.classList.add("flash");
  }

  previousScoreKey = key;
}

function updateConnectionAge() {
  const el = $("connectionAge");
  if (!lastSuccessfulUpdate) {
    el.textContent = "Waiting for first update…";
    el.className = "live-age";
    return;
  }

  const seconds = Math.max(0, Math.round((Date.now() - lastSuccessfulUpdate.getTime()) / 1000));
  el.textContent = `Last good update ${seconds}s ago`;

  if (seconds <= 15) el.className = "live-age fresh";
  else if (seconds <= 45) el.className = "live-age stale";
  else el.className = "live-age bad";
}

setInterval(updateConnectionAge, 1000);

function populateSelectors(pitches) {
  const pitchers = [...new Set(pitches.map(p => p.pitcher).filter(Boolean))].sort();
  const batters = [...new Set(pitches.map(p => p.batter).filter(Boolean))].sort();
  const pitchTypes = [...new Map(
    pitches
      .filter(p => p.pitchCode)
      .map(p => [p.pitchCode, p.pitchType || p.pitchCode])
  ).entries()].sort((a,b) => a[1].localeCompare(b[1]));

  replaceOptions($("pitcherSelect"), [["", "All pitchers"], ...pitchers.map(v => [v, v])]);
  replaceOptions($("batterSelect"), [["", "All batters"], ...batters.map(v => [v, v])]);
  replaceOptions($("pitchTypeSelect"), [["", "All pitches"], ...pitchTypes.map(([code, desc]) => [code, `${code} · ${desc}`])]);
}

function replaceOptions(select, entries) {
  const old = select.value;
  select.replaceChildren();
  entries.forEach(([value, label]) => {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = label;
    select.appendChild(option);
  });
  if ([...select.options].some(o => o.value === old)) select.value = old;
}


function filteredPitches(source = allPitches) {
  const pitcher = $("pitcherSelect").value;
  const batter = $("batterSelect").value;
  const pitchType = $("pitchTypeSelect").value;

  return source.filter(p =>
    (!pitcher || p.pitcher === pitcher) &&
    (!batter || p.batter === batter) &&
    (!pitchType || p.pitchCode === pitchType)
  );
}

function renderAnalytics(source = allPitches) {
  const pitches = filteredPitches(source);
  const pitcher = $("pitcherSelect").value;
  const batter = $("batterSelect").value;
  const pitchType = $("pitchTypeSelect").value;
  const mode = $("vizMode").value;
  const scope = $("scopeSelect").value;

  const parts = [];
  if (pitcher) parts.push(`Pitcher: ${pitcher}`);
  if (batter) parts.push(`Batter: ${batter}`);
  if (pitchType) parts.push(`Pitch: ${pitchType}`);

  const scopeLabel = {
    game: "current game",
    "7d": "last 7 days",
    "30d": "last 30 days",
    season: "current season"
  }[scope] || scope;

  $("selectionSummary").textContent =
    (parts.length ? parts.join(" · ") : "All selected pitches") + ` · ${scopeLabel}`;
  $("selectionCount").textContent = `${pitches.length} pitch${pitches.length === 1 ? "" : "es"}`;
  $("scopeBadge").textContent = scope === "game" ? "GAME" : "POSTGRES";

  if (mode === "heat") {
    $("vizHeading").textContent = `Pitch location density · ${scopeLabel}`;
    $("zoneExplanation").textContent =
      "Heat intensity represents where the selected pitch sample crossed the plate most often.";
    drawHeatMap(pitches);
  } else if (mode === "zones") {
    $("vizHeading").textContent = `Batter zone results · ${scopeLabel}`;
    $("zoneExplanation").textContent =
      scope === "game"
        ? "Zone shading is derived only from this game's selected pitch outcomes."
        : "Zone shading is derived from pitches stored in the VS PostgreSQL historical database for the selected scope.";
    drawBatterZones(pitches);
  } else {
    $("vizHeading").textContent = `Pitch location · ${scopeLabel}`;
    $("zoneExplanation").textContent =
      "Each marker is one tracked MLB pitch. The newest displayed pitch is outlined in white.";
    drawDots(pitches);
  }

  renderPitchList(pitches);
}

async function loadScopedAnalytics() {
  const scope = $("scopeSelect").value;

  if (scope === "game") {
    renderAnalytics(allPitches);
    return;
  }

  const now = new Date();
  let from = new Date(now);

  if (scope === "7d") {
    from.setDate(now.getDate() - 7);
  } else if (scope === "30d") {
    from.setDate(now.getDate() - 30);
  } else if (scope === "season") {
    from = new Date(now.getFullYear(), 0, 1);
  }

  const query = new URLSearchParams();
  query.set("from", from.toISOString());
  query.set("to", now.toISOString());
  if ($("pitcherSelect").value) query.set("pitcher", $("pitcherSelect").value);
  if ($("batterSelect").value) query.set("batter", $("batterSelect").value);
  if ($("pitchTypeSelect").value) query.set("pitchType", $("pitchTypeSelect").value);
  query.set("limit", "50000");

  $("selectionSummary").textContent = "Loading PostgreSQL history…";

  try {
    const response = await fetch(`/api/analytics/pitches?${query}`);
    if (!response.ok) {
      const body = await response.json().catch(() => ({}));
      throw new Error(body.message || `HTTP ${response.status}`);
    }
    const historical = await response.json();
    renderAnalytics(Array.isArray(historical) ? historical : []);
  } catch (error) {
    $("selectionSummary").textContent = `Historical database unavailable: ${error.message}`;
    $("selectionCount").textContent = "0 pitches";
    drawDots([]);
    renderPitchList([]);
  }
}

function zoneGeometry(pitches) {
  const valid = pitches.filter(p =>
    typeof p.plateX === "number" &&
    typeof p.plateZ === "number"
  );

  const latest = [...valid].reverse().find(p =>
    typeof p.strikeZoneTop === "number" &&
    typeof p.strikeZoneBottom === "number"
  );

  const zoneTop = latest?.strikeZoneTop ?? 3.5;
  const zoneBottom = latest?.strikeZoneBottom ?? 1.5;
  const halfWidthFeet = 17 / 24;

  const xMin = -2.0, xMax = 2.0;
  const zMin = 0.5, zMax = 4.6;
  const left = 55, right = 475, top = 35, bottom = 520;
  const px = x => left + ((x - xMin) / (xMax - xMin)) * (right - left);
  const pz = z => bottom - ((z - zMin) / (zMax - zMin)) * (bottom - top);

  const zoneLeft = px(-halfWidthFeet);
  const zoneRight = px(halfWidthFeet);
  const zoneYTop = pz(zoneTop);
  const zoneYBottom = pz(zoneBottom);

  return {
    valid, zoneTop, zoneBottom, zoneLeft, zoneRight, zoneYTop, zoneYBottom,
    zoneW: zoneRight-zoneLeft, zoneH: zoneYBottom-zoneYTop,
    px, pz
  };
}

function drawBaseZone(g) {
  const grid = $("zoneGrid");
  const heat = $("heatLayer");
  const cells = $("zoneCells");
  const marks = $("pitchMarks");
  grid.replaceChildren();
  heat.replaceChildren();
  cells.replaceChildren();
  marks.replaceChildren();

  grid.appendChild(svgEl("rect", {
    x:g.zoneLeft, y:g.zoneYTop, width:g.zoneW, height:g.zoneH,
    fill:"none", stroke:"#f4f7fb", "stroke-width":"3"
  }));

  for (let i=1;i<=2;i++) {
    grid.appendChild(svgEl("line", {
      x1:g.zoneLeft + g.zoneW*i/3, y1:g.zoneYTop,
      x2:g.zoneLeft + g.zoneW*i/3, y2:g.zoneYBottom,
      stroke:"#52637c", "stroke-width":"1"
    }));
    grid.appendChild(svgEl("line", {
      x1:g.zoneLeft, y1:g.zoneYTop + g.zoneH*i/3,
      x2:g.zoneRight, y2:g.zoneYTop + g.zoneH*i/3,
      stroke:"#52637c", "stroke-width":"1"
    }));
  }

  const topText = svgEl("text", {x:18,y:g.zoneYTop+5,fill:"#8fa0b8","font-size":"14"});
  topText.textContent = `${g.zoneTop.toFixed(2)} ft`;
  grid.appendChild(topText);

  const bottomText = svgEl("text", {x:18,y:g.zoneYBottom+5,fill:"#8fa0b8","font-size":"14"});
  bottomText.textContent = `${g.zoneBottom.toFixed(2)} ft`;
  grid.appendChild(bottomText);
}

function drawDots(pitches) {
  const g = zoneGeometry(pitches);
  drawBaseZone(g);

  const marks = $("pitchMarks");
  const recent = g.valid.slice(-60);

  recent.forEach((p,index) => {
    const kind = pitchClass(p.result);
    const circle = svgEl("circle", {
      cx:g.px(p.plateX),
      cy:g.pz(p.plateZ),
      r:index === recent.length-1 ? "9":"6",
      fill:markColor(kind),
      stroke:index === recent.length-1 ? "#ffffff":"#0b1018",
      "stroke-width":index === recent.length-1 ? "3":"1.5",
      opacity:index === recent.length-1 ? "1":".82"
    });
    const title = svgEl("title");
    title.textContent = `${p.pitchType || p.pitchCode} ${formatMph(p.startSpeedMph)} · ${p.result}`;
    circle.appendChild(title);
    marks.appendChild(circle);
  });
}

function drawHeatMap(pitches) {
  const g = zoneGeometry(pitches);
  drawBaseZone(g);

  const heat = $("heatLayer");
  const cols = 8, rows = 10;
  const xMin = -2.0, xMax = 2.0, zMin = 0.5, zMax = 4.6;
  const counts = Array.from({length:rows}, () => Array(cols).fill(0));

  g.valid.forEach(p => {
    let c = Math.floor((p.plateX-xMin)/(xMax-xMin)*cols);
    let r = Math.floor((zMax-p.plateZ)/(zMax-zMin)*rows);
    if (c>=0 && c<cols && r>=0 && r<rows) counts[r][c]++;
  });

  const max = Math.max(1, ...counts.flat());
  const plotLeft = g.px(xMin), plotRight = g.px(xMax);
  const plotTop = g.pz(zMax), plotBottom = g.pz(zMin);
  const cw = (plotRight-plotLeft)/cols;
  const ch = (plotBottom-plotTop)/rows;

  counts.forEach((row,r) => row.forEach((count,c) => {
    if (!count) return;
    const intensity = count/max;
    heat.appendChild(svgEl("rect", {
      x:plotLeft+c*cw,
      y:plotTop+r*ch,
      width:cw+0.5,
      height:ch+0.5,
      fill:"#ff8a65",
      opacity:(0.12 + intensity*0.72).toFixed(2),
      class:"heat-cell"
    }));
  }));
}

function zoneIndex(p, g) {
  if (p.plateX < -17/24 || p.plateX > 17/24 ||
      p.plateZ < g.zoneBottom || p.plateZ > g.zoneTop) return null;

  const col = Math.min(2, Math.max(0, Math.floor((p.plateX + 17/24) / ((17/12)/3))));
  const row = Math.min(2, Math.max(0, Math.floor((g.zoneTop - p.plateZ) / ((g.zoneTop-g.zoneBottom)/3))));
  return row*3 + col;
}

function drawBatterZones(pitches) {
  const g = zoneGeometry(pitches);
  drawBaseZone(g);

  const cellsLayer = $("zoneCells");
  const stats = Array.from({length:9}, () => ({total:0, positive:0, negative:0}));

  g.valid.forEach(p => {
    const zi = zoneIndex(p,g);
    if (zi == null) return;
    stats[zi].total++;
    const kind = pitchClass(p.result);
    if (kind === "inplay") stats[zi].positive++;
    if (kind === "strike") stats[zi].negative++;
  });

  for (let i=0;i<9;i++) {
    const row = Math.floor(i/3), col = i%3;
    const s = stats[i];
    let fill = "#667085";
    let opacity = 0.10;

    if (s.total) {
      const score = (s.positive - s.negative) / s.total;
      if (score > 0.15) fill = "#66a3ff";
      else if (score < -0.15) fill = "#ff6b6b";
      else fill = "#b7c2d3";
      opacity = Math.min(.68, .18 + s.total*.08);
    }

    const rect = svgEl("rect", {
      x:g.zoneLeft + col*g.zoneW/3,
      y:g.zoneYTop + row*g.zoneH/3,
      width:g.zoneW/3,
      height:g.zoneH/3,
      fill,
      opacity
    });

    const title = svgEl("title");
    title.textContent = s.total
      ? `${s.total} pitches · ${s.positive} in play · ${s.negative} strike/foul outcomes`
      : "No pitches in this zone";
    rect.appendChild(title);
    cellsLayer.appendChild(rect);

    const label = svgEl("text", {
      x:g.zoneLeft + (col+.5)*g.zoneW/3,
      y:g.zoneYTop + (row+.55)*g.zoneH/3,
      "text-anchor":"middle",
      fill:"#ffffff",
      "font-size":"18",
      "font-weight":"700",
      opacity:s.total ? "1":"0.45"
    });
    label.textContent = s.total || "–";
    cellsLayer.appendChild(label);
  }
}

function renderPitchList(pitches) {
  const list = $("pitchList");
  list.replaceChildren();

  pitches.slice(-40).reverse().forEach(p => {
    const row = document.createElement("div");
    row.className = "pitch-row";

    const num = document.createElement("span");
    num.textContent = `#${p.pitchNumber || "–"}`;

    const code = document.createElement("span");
    code.className = "pitch-code";
    code.textContent = p.pitchCode || "—";

    const mph = document.createElement("span");
    mph.textContent = formatMph(p.startSpeedMph);

    const result = document.createElement("span");
    result.className = "pitch-result";
    result.textContent = `${p.result || ""}${p.pitcher ? ` · ${p.pitcher} → ${p.batter}` : ""}`;

    row.append(num, code, mph, result);
    list.appendChild(row);
  });
}

function renderGameState(game) {
  $("awayTeam").textContent = game.awayTeam || "Away";
  $("homeTeam").textContent = game.homeTeam || "Home";
  $("awayRecord").textContent = Number.isInteger(game.awayWins) && Number.isInteger(game.awayLosses) ? `${game.awayWins}-${game.awayLosses}` : "—";
  $("homeRecord").textContent = Number.isInteger(game.homeWins) && Number.isInteger(game.homeLosses) ? `${game.homeWins}-${game.homeLosses}` : "—";
  setRemoteImage("awayTeamLogo", teamLogoUrl(game.awayTeamId), `${game.awayTeam || "Away"} logo`);
  setRemoteImage("homeTeamLogo", teamLogoUrl(game.homeTeamId), `${game.homeTeam || "Home"} logo`);
  $("awayScore").textContent = game.awayScore ?? 0;
  $("homeScore").textContent = game.homeScore ?? 0;
  $("status").textContent = game.detailedStatus || game.status || "";

  const isFinal = (game.status || "").toLowerCase() === "final" ||
                  (game.detailedStatus || "").toLowerCase().includes("final");

  if (isFinal) {
    $("stateHeading").textContent = "Final game summary";
    $("matchupHeading").textContent = "Final matchup";
    $("inning").textContent = game.inning ? `Final · ${game.inning} innings` : "Final";
    $("countSection").style.display = "none";
  } else {
    $("stateHeading").textContent = "Live game state";
    $("matchupHeading").textContent = "Current matchup";
    $("countSection").style.display = "";
    $("inning").textContent = game.inning ? `${game.inningState || ""} ${game.inning}`.trim() : "";
  }

  $("balls").textContent = game.balls ?? 0;
  $("strikes").textContent = game.strikes ?? 0;
  $("outs").textContent = game.outs ?? 0;

  setBase("baseFirst", game.bases?.first);
  setBase("baseSecond", game.bases?.second);
  setBase("baseThird", game.bases?.third);

  $("pitcher").textContent = game.matchup?.pitcher || "—";
  $("pitcherHand").textContent = game.matchup?.pitchHand ? `${game.matchup.pitchHand}HP` : "";
  $("batter").textContent = game.matchup?.batter || "—";
  $("batterSide").textContent = game.matchup?.batSide ? `Bats ${game.matchup.batSide}` : "";
  $("lastPlay").textContent = game.lastPlay || "";
}


async function loadGameSummary() {
  try {
    const response = await fetch(`/api/mlb/games/${encodeURIComponent(gamePk)}/summary`, {
      cache: "no-store"
    });
    if (!response.ok) {
      const body = await response.json().catch(() => ({}));
      throw new Error(body.detail || body.message || `HTTP ${response.status}`);
    }
    const summary = await response.json();
    currentGame = summary;
    renderGameState(summary);
    renderScoreChange(summary);
    renderLiveGameView();
    updateStadiumBoardCelebration(summary);
    detectLiveEvent(summary);
    syncLiveSelectors(summary);
    lastSuccessfulUpdate = new Date();
    updateConnectionAge();
    $("updated").textContent = `Game updated ${new Date().toLocaleTimeString()}`;
  } catch (error) {
    $("updated").textContent = `Unable to load game summary: ${error.message}`;
  }
}

async function loadPitchAnalytics() {
  $("analyticsLoading").style.display = "";
  $("analyticsLoading").textContent = "Loading pitch analytics…";

  try {
    const response = await fetch(`/api/mlb/games/${encodeURIComponent(gamePk)}/pitches`, {
      cache: "no-store"
    });
    if (!response.ok) {
      const body = await response.json().catch(() => ({}));
      throw new Error(body.detail || body.message || `HTTP ${response.status}`);
    }
    const pitches = await response.json();

    allPitches = Array.isArray(pitches) ? pitches : [];
    populateSelectors(allPitches);
    syncLiveSelectors(currentGame);
    await loadScopedAnalytics();
    renderLiveStats();
    renderLiveGameView();

    $("analyticsLoading").textContent = `${allPitches.length} tracked pitches loaded`;
    setTimeout(() => {
      $("analyticsLoading").style.display = "none";
    }, 1800);
  } catch (error) {
    $("analyticsLoading").textContent = `Unable to load pitch analytics: ${error.message}`;
  }
}

async function loadGameCenter() {
  await loadGameSummary();
  await loadPitchAnalytics();
  fitOutputViewport();
}

function fitOutputViewport() {
  if (!outputMode) return;
  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;
  document.body.style.zoom = "1";
  document.body.style.width = `${viewportWidth}px`;
  document.body.style.minHeight = `${viewportHeight}px`;
  requestAnimationFrame(() => {
    const contentWidth = Math.max(document.documentElement.scrollWidth, document.body.scrollWidth);
    const contentHeight = Math.max(document.documentElement.scrollHeight, document.body.scrollHeight);
    const scale = Math.min(1, window.innerWidth / contentWidth, window.innerHeight / contentHeight);
    // CSS percentage widths are resolved before zoom and left unused black
    // space at fractional scales. An explicit virtual canvas fills the full
    // encoded frame while retaining every tile.
    document.body.style.width = `${viewportWidth / scale}px`;
    document.body.style.minHeight = `${viewportHeight / scale}px`;
    document.body.style.zoom = String(scale);
  });
}

$("showBoxScore")?.addEventListener("change", event => {
  [$("lineScorePanel"),$("awayBoxScorePanel"),$("homeBoxScorePanel")].forEach(panel=>panel.hidden=!event.target.checked);
});

async function refreshGameCenter() {
  await loadGameSummary();

  const statusText = (currentGame?.status || currentGame?.detailedStatus || "").toLowerCase();
  const isFinal = statusText.includes("final");

  // Completed games do not need their full pitch history downloaded repeatedly.
  if (!isFinal) {
    await loadPitchAnalytics();
  }
  fitOutputViewport();
}

window.addEventListener("resize", fitOutputViewport);

loadGameCenter();
setInterval(refreshGameCenter, 10000);
setInterval(cycleRecentActions, 3500);
