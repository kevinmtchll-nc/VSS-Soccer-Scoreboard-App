const $ = id => document.getElementById(id);

const eztvIntegrationSection = $("eztvIntegrationSection");
const videoOutputSection = $("videoOutputSection");
if (eztvIntegrationSection && videoOutputSection) {
  eztvIntegrationSection.after(videoOutputSection);
}

function makeSettingsCollapsible() {
  const sections = [...document.querySelectorAll(".setup-shell > .setup-grid > .panel, .setup-shell > .panel")];
  sections.forEach((section,index) => {
    const heading = section.querySelector(":scope > h2");
    if (!heading) return;
    const details = document.createElement("details");
    details.className = `${section.className} settings-main-section`;
    // Keep the two summary tiles even when Settings first opens.
    details.open = index < 2;
    const summary = document.createElement("summary");
    summary.textContent = heading.textContent;
    details.append(summary);
    [...section.childNodes].forEach(node => { if (node !== heading) details.append(node); });
    section.replaceWith(details);
  });
}
makeSettingsCollapsible();

const today = new Date();
$("importDate").value = [
  today.getFullYear(),
  String(today.getMonth()+1).padStart(2,"0"),
  String(today.getDate()).padStart(2,"0")
].join("-");
$("exportDate").value = $("importDate").value;

$("refreshStatus").addEventListener("click", loadStatus);
$("initializeDb").addEventListener("click", initializeDatabase);
$("importDateButton").addEventListener("click", importDate);
$("exportJson").addEventListener("click", () => exportHistory("json"));
$("exportCsv").addEventListener("click", () => exportHistory("csv"));
$("saveDisplaySettings").addEventListener("click", saveDisplaySettings);
$("saveNetworkSettings").addEventListener("click", saveNetworkSettings);
$("testDbConnection").addEventListener("click", testDbConnection);
$("saveDbSettings").addEventListener("click", saveDbSettings);
$("changeDbPassword").addEventListener("click", changeDbPassword);
$("addDbUser").addEventListener("click", addDbUser);
$("provisionPostgres").addEventListener("click", provisionPostgres);
$("newDbPassword").addEventListener("input", updatePasswordChecklist);

function yesNo(value) {
  return value ? "Yes" : "No";
}

async function loadNetworkSettings() {
  const response = await fetch("/api/settings/network", {cache:"no-store"});
  if (!response.ok) return;
  const data = await response.json();
  $("listenPort").value = data.port || 5000;
}

async function saveNetworkSettings() {
  const banner = $("networkSettingsBanner");
  const port = Number($("listenPort").value);
  banner.textContent = "Saving listening port...";
  try {
    const response = await fetch("/api/settings/network", {
      method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify({port})
    });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(body.message || `HTTP ${response.status}`);
    banner.textContent = body.message;
    banner.classList.add("ok"); banner.classList.remove("bad");
  } catch (error) {
    banner.textContent = `Unable to save listening port: ${error.message}`;
    banner.classList.add("bad"); banner.classList.remove("ok");
  }
}


async function loadDisplaySettings() {
  const banner = $("displaySettingsBanner");
  banner.textContent = "Loading display settings…";

  try {
    const response = await fetch("/api/settings/display", {cache:"no-store"});
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();

    const select = $("displayTimeZone");
    select.replaceChildren();

    const server = document.createElement("option");
    server.value = "SERVER_LOCAL";
    server.textContent = `Server Local (${data.serverLocalTimeZoneName || data.serverLocalTimeZoneId})`;
    select.appendChild(server);

    for (const zone of data.zones || []) {
      const option = document.createElement("option");
      option.value = zone.id;
      option.textContent = zone.name || zone.id;
      select.appendChild(option);
    }

    select.value = data.configuredTimeZoneId || "SERVER_LOCAL";

    $("timeZoneEffective").textContent =
      `Effective: ${data.effectiveTimeZoneName || data.effectiveTimeZoneId}`;

    banner.textContent =
      `Times currently display using ${data.effectiveTimeZoneName || data.effectiveTimeZoneId}.`;
    banner.classList.add("ok");
    banner.classList.remove("bad");
  } catch (error) {
    banner.textContent = `Unable to load display settings: ${error.message}`;
    banner.classList.add("bad");
  }
}

async function saveDisplaySettings() {
  const banner = $("displaySettingsBanner");
  const timeZoneId = $("displayTimeZone").value;

  banner.textContent = "Saving display settings…";

  try {
    const response = await fetch("/api/settings/display", {
      method:"POST",
      headers:{"Content-Type":"application/json"},
      body:JSON.stringify({timeZoneId})
    });

    const body = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(body.message || `HTTP ${response.status}`);

    banner.textContent = `${body.message} New game times will use ${body.effectiveTimeZoneId}.`;
    banner.classList.add("ok");
    banner.classList.remove("bad");

    // JSON configuration reload is automatic; give the file watcher a moment, then refresh settings.
    setTimeout(loadDisplaySettings, 800);
  } catch (error) {
    banner.textContent = `Unable to save display settings: ${error.message}`;
    banner.classList.add("bad");
  }
}


function postgresFormBody() {
  return {
    host: $("pgHost").value.trim(),
    port: Number($("pgPort").value || 5432),
    database: $("pgDatabase").value.trim(),
    username: $("pgUsername").value.trim(),
    password: $("pgPassword").value
  };
}

async function loadPostgresSettings() {
  try {
    const response = await fetch("/api/settings/postgres", {cache:"no-store"});
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();

    $("pgHost").value = data.host || "localhost";
    $("pgPort").value = data.port || 5432;
    $("pgDatabase").value = data.database || "vitec_scoreboard";
    $("pgUsername").value = data.username || "postgres";
    $("pgPassword").value = "";
    $("pgPassword").placeholder = data.hasPassword
      ? "Saved password exists — leave blank to keep it"
      : "Enter PostgreSQL password";
  } catch (error) {
    $("dbBanner").textContent = `Unable to load PostgreSQL settings: ${error.message}`;
    $("dbBanner").classList.add("bad");
  }
}

async function testDbConnection() {
  $("dbBanner").textContent = "Testing PostgreSQL connection…";

  try {
    const response = await fetch("/api/settings/postgres/test", {
      method:"POST",
      headers:{"Content-Type":"application/json"},
      body:JSON.stringify(postgresFormBody())
    });

    const body = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(body.message || `HTTP ${response.status}`);

    $("dbBanner").textContent = body.message || "PostgreSQL connection succeeded.";
    $("dbBanner").classList.add("ok");
    $("dbBanner").classList.remove("bad");
  } catch (error) {
    $("dbBanner").textContent = `PostgreSQL test failed: ${error.message}`;
    $("dbBanner").classList.add("bad");
    $("dbBanner").classList.remove("ok");
  }
}

async function saveDbSettings() {
  $("dbBanner").textContent = "Saving PostgreSQL settings…";

  try {
    const response = await fetch("/api/settings/postgres", {
      method:"POST",
      headers:{"Content-Type":"application/json"},
      body:JSON.stringify(postgresFormBody())
    });

    const body = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(body.message || `HTTP ${response.status}`);

    $("dbBanner").textContent = body.message || "PostgreSQL settings saved.";
    $("dbBanner").classList.toggle("ok", Boolean(body.connected));
    $("dbBanner").classList.toggle("bad", !body.connected);

    $("pgPassword").value = "";
    await loadPostgresSettings();
    await loadStatus();
  } catch (error) {
    $("dbBanner").textContent = `Unable to save PostgreSQL settings: ${error.message}`;
    $("dbBanner").classList.add("bad");
  }
}

async function provisionPostgres(){const banner=$("provisionPostgresBanner"),appPassword=$("pgAppPassword").value;if(appPassword!==$("pgAppPasswordConfirm").value){banner.textContent="Application passwords do not match.";banner.className="setup-banner bad";return;}if(!validPassword(appPassword)){banner.textContent="The application password does not meet the displayed requirements.";banner.className="setup-banner bad";return;}banner.textContent="Configuring the local PostgreSQL account and database...";try{const response=await fetch("/api/settings/postgres/provision",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({host:$("pgHost").value.trim()||"localhost",port:Number($("pgPort").value||5432),database:$("pgDatabase").value.trim()||"vitec_scoreboard",adminUsername:$("pgAdminUsername").value.trim()||"postgres",adminPassword:$("pgAdminPassword").value,appUsername:$("pgAppUsername").value.trim()||"vsapp",appPassword})});const body=await response.json().catch(()=>({}));if(!response.ok)throw new Error(body.message||`HTTP ${response.status}`);banner.textContent=body.message;banner.className="setup-banner ok";$("pgAdminPassword").value=$("pgAppPassword").value=$("pgAppPasswordConfirm").value="";await loadPostgresSettings();await loadStatus();await loadDbUsers();}catch(error){banner.textContent=`Unable to configure PostgreSQL: ${error.message}`;banner.className="setup-banner bad";}}

async function loadStatus() {
  $("dbBanner").textContent = "Checking PostgreSQL…";

  try {
    const response = await fetch("/api/setup/status", {cache:"no-store"});
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const s = await response.json();

    $("appVersion").textContent = s.version || "—";
    $("serviceName").textContent = s.service || "—";
    $("listenUrl").textContent = s.listenUrl || "—";
    $("settingsFile").textContent = s.settingsFile || "—";

    const db = s.database || {};
    $("dbConfigured").textContent = yesNo(s.postgresConfigured);
    $("dbConnected").textContent = yesNo(db.canConnect);
    $("dbGames").textContent = db.games ?? 0;
    $("dbPitches").textContent = db.pitches ?? 0;
    $("dbLatest").textContent = db.latestGameDate
      ? new Date(db.latestGameDate).toLocaleString()
      : "—";

    $("dbBanner").textContent = db.message || "Database status loaded.";
    $("dbBanner").classList.toggle("ok", Boolean(db.canConnect));
    $("dbBanner").classList.toggle("bad", !db.canConnect);
  } catch (error) {
    $("dbBanner").textContent = `Unable to load system status: ${error.message}`;
    $("dbBanner").classList.add("bad");
  }
}

async function initializeDatabase() {
  $("dbBanner").textContent = "Initializing PostgreSQL schema…";

  try {
    const response = await fetch("/api/db/initialize", {method:"POST"});
    const body = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(body.message || `HTTP ${response.status}`);
    $("dbBanner").textContent = body.message || "Database initialized.";
    await loadStatus();
  } catch (error) {
    $("dbBanner").textContent = `Initialization failed: ${error.message}`;
    $("dbBanner").classList.add("bad");
  }
}

async function importDate() {
  const date = $("importDate").value;
  $("importDateButton").disabled = true;
  $("importResult").textContent = `Importing ${date}…`;

  try {
    const response = await fetch(`/api/history/import-date?date=${encodeURIComponent(date)}`, {
      method:"POST"
    });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(body.message || `HTTP ${response.status}`);

    const games = Array.isArray(body.games) ? body.games : [];
    const lines = games.map(g =>
      `${g.gamePk}  ${g.game || ""}  inserted=${g.inserted ?? 0}  ${g.result || ""}`
    );

    $("importResult").textContent =
      `Imported ${date}\n\n${lines.join("\n") || "No games returned."}`;

    await loadStatus();
  } catch (error) {
    $("importResult").textContent = `Import failed: ${error.message}`;
  } finally {
    $("importDateButton").disabled = false;
  }
}

function exportHistory(format) {
  const date = $("exportDate").value;
  if (!date) { $("exportResult").textContent = "Choose an export date."; return; }
  $("exportResult").textContent = `Preparing ${format.toUpperCase()} export for ${date}...`;
  location.href = `/api/history/export?date=${encodeURIComponent(date)}&format=${encodeURIComponent(format)}`;
  setTimeout(() => { $("exportResult").textContent = `${format.toUpperCase()} export requested for ${date}.`; }, 500);
}

function passwordRules(value) {
  return [
    [value.length >= 12 && value.length <= 128,"12â€“128 characters"],
    [/[A-Z]/.test(value),"One uppercase letter"],
    [/[a-z]/.test(value),"One lowercase letter"],
    [/\d/.test(value),"One number"],
    [/[!@#$%^&*\-_]/.test(value),"One special character: ! @ # $ % ^ & * - _"],
    [!/[\s]/.test(value),"No spaces"]
  ];
}
function validPassword(value) { return passwordRules(value).every(([ok])=>ok); }
function updatePasswordChecklist() { $("passwordChecklist").replaceChildren(...passwordRules($("newDbPassword").value).map(([ok,label])=>{const item=document.createElement("li");item.textContent=`${ok?"âœ“":"â—‹"} ${label}`;item.className=ok?"met":"";return item;})); }
updatePasswordChecklist();

async function changeDbPassword() {
  const currentPassword=$("currentDbPassword").value,newPassword=$("newDbPassword").value,confirmPassword=$("confirmDbPassword").value,banner=$("passwordChangeBanner");
  if(newPassword!==confirmPassword){banner.textContent="New passwords do not match.";banner.className="setup-banner bad";return;}
  if(!validPassword(newPassword)){banner.textContent="The new password does not meet the displayed requirements.";banner.className="setup-banner bad";return;}
  try{const response=await fetch("/api/settings/postgres/password",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({currentPassword,newPassword})});const body=await response.json().catch(()=>({}));if(!response.ok)throw new Error(body.message||`HTTP ${response.status}`);banner.textContent=body.message;banner.className="setup-banner ok";$("currentDbPassword").value=$("newDbPassword").value=$("confirmDbPassword").value="";updatePasswordChecklist();await loadPostgresSettings();}catch(error){banner.textContent=`Unable to change password: ${error.message}`;banner.className="setup-banner bad";}
}

async function loadDbUsers(){const banner=$("postgresUsersBanner"),list=$("postgresUsersList");try{const response=await fetch("/api/settings/postgres/users",{cache:"no-store"});const body=await response.json().catch(()=>({}));if(!response.ok)throw new Error(body.message||`HTTP ${response.status}`);list.replaceChildren(...(body.users||[]).map(user=>{const row=document.createElement("div");row.className="postgres-user-row";const name=document.createElement("strong");name.textContent=user.username+(user.isApplicationUser?" (VITEC application account)":"");const state=document.createElement("span");state.textContent=user.canLogin?"Enabled":"Disabled";const toggle=document.createElement("button");toggle.type="button";toggle.textContent=user.canLogin?"Disable":"Enable";toggle.disabled=user.isApplicationUser;toggle.addEventListener("click",()=>updateDbUser(user.username,{canLogin:!user.canLogin}));const reset=document.createElement("button");reset.type="button";reset.textContent="Change Password";reset.addEventListener("click",()=>{const password=prompt(`Enter a new password for ${user.username}.\n\n12â€“128 characters; uppercase, lowercase, number, special character; no spaces.`);if(password)updateDbUser(user.username,{password});});const remove=document.createElement("button");remove.type="button";remove.textContent="Remove";remove.disabled=user.isApplicationUser;remove.addEventListener("click",()=>{if(confirm(`Remove PostgreSQL user ${user.username}?`))deleteDbUser(user.username);});row.append(name,state,toggle,reset,remove);return row;}));banner.textContent="PostgreSQL users loaded.";banner.className="setup-banner ok";}catch(error){banner.textContent=`Unable to load users: ${error.message}`;banner.className="setup-banner bad";}}
async function addDbUser(){const username=$("newDbUsername").value.trim(),password=$("newDbUserPassword").value,confirmPassword=$("confirmDbUserPassword").value;if(password!==confirmPassword||!validPassword(password)){alert("The passwords must match and meet all displayed requirements.");return;}const response=await fetch("/api/settings/postgres/users",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({username,password})});const body=await response.json().catch(()=>({}));if(!response.ok){$("postgresUsersBanner").textContent=body.message||`HTTP ${response.status}`;$("postgresUsersBanner").className="setup-banner bad";return;}$("newDbUsername").value=$("newDbUserPassword").value=$("confirmDbUserPassword").value="";await loadDbUsers();}
async function updateDbUser(username,change){const response=await fetch(`/api/settings/postgres/users/${encodeURIComponent(username)}`,{method:"PATCH",headers:{"Content-Type":"application/json"},body:JSON.stringify(change)});const body=await response.json().catch(()=>({}));if(!response.ok){alert(body.message||`HTTP ${response.status}`);return;}await loadDbUsers();}
async function deleteDbUser(username){const response=await fetch(`/api/settings/postgres/users/${encodeURIComponent(username)}`,{method:"DELETE"});const body=await response.json().catch(()=>({}));if(!response.ok){alert(body.message||`HTTP ${response.status}`);return;}await loadDbUsers();}

loadStatus();
loadDisplaySettings();
loadPostgresSettings();
loadNetworkSettings();
loadDbUsers();

function updateIntegrationUrls() {
  const base = `${location.protocol}//${location.host}`;
  const gamePk = $("outputGamePk").value.trim();
  const selected = $("schemaAll").checked ? ["all"] : [...document.querySelectorAll(".schema-option:checked")].map(item => item.value);
  const query = new URLSearchParams();
  if (gamePk) query.set("gamePk", gamePk);
  query.set("schema", (selected.length ? selected : ["all"]).join(","));
  const jsonRelative = `/api/integrations/eztv/feed?${query}`;
  const xmlRelative = `/api/integrations/eztv/feed.xml?${query}`;
  $("eztvJsonUrl").value = `${base}${jsonRelative}`;
  $("eztvXmlUrl").value = `${base}${xmlRelative}`;
  $("jsonPreviewLink").href = jsonRelative;
  $("xmlPreviewLink").href = xmlRelative;
  $("workspaceEditorLink").href = `/output.html?scene=game-workspace&edit=1&gamePk=${encodeURIComponent(gamePk)}&template=${encodeURIComponent($("videoTemplate").value || "default")}`;
  $("gameCenterAppearanceLink").href = `/gamecenter.html?gamePk=${encodeURIComponent(gamePk)}&settings=appearance`;
  $("gameCenterLayoutLink").href = `/gamecenter.html?gamePk=${encodeURIComponent(gamePk)}&settings=layout`;
}

$("outputGamePk")?.addEventListener("input", updateIntegrationUrls);
$("schemaAll")?.addEventListener("change",()=>{document.querySelectorAll(".schema-option").forEach(item=>{item.disabled=$("schemaAll").checked;if($("schemaAll").checked)item.checked=false;});updateIntegrationUrls();});
document.querySelectorAll(".schema-option").forEach(item=>item.addEventListener("change",updateIntegrationUrls));
updateIntegrationUrls();

$("outputGameDate").value = $("importDate").value;

async function loadOutputGames() {
  const picker = $("outputGamePicker");
  picker.replaceChildren(new Option("Loading games...", ""));
  try {
    const response = await fetch(`/api/mlb/games?date=${encodeURIComponent($("outputGameDate").value)}`, {cache:"no-store"});
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const games = await response.json();
    picker.replaceChildren(new Option("Select a game", ""));
    for (const game of games || []) {
      const score = `${game.away?.score ?? 0}-${game.home?.score ?? 0}`;
      const label = `${game.displayStart || ""} · ${game.away?.name || "Away"} at ${game.home?.name || "Home"} · ${score} · ${game.detailedStatus || game.status || ""}`;
      picker.appendChild(new Option(label, String(game.gamePk)));
    }
    if (!games?.length) picker.replaceChildren(new Option("No games found for this date", ""));
  } catch (error) { picker.replaceChildren(new Option(`Unable to load games: ${error.message}`, "")); }
}

function selectOutputGame() {
  const gamePk = $("outputGamePicker").value;
  if (!gamePk) return;
  $("outputGamePk").value = gamePk;
  $("videoGamePk").value = gamePk;
  updateIntegrationUrls();
  updateVideoPreviewLink();
}

$("outputGameDate")?.addEventListener("change", loadOutputGames);
$("refreshOutputGames")?.addEventListener("click", loadOutputGames);
$("outputGamePicker")?.addEventListener("change", selectOutputGame);
loadOutputGames();

$("videoGameDate").value = $("importDate").value;

async function loadVideoGames() {
  const picker = $("videoGamePicker");
  const selectedGamePk = $("videoGamePk").value;
  picker.replaceChildren(new Option("Loading games...", ""));
  try {
    const response = await fetch(`/api/mlb/games?date=${encodeURIComponent($("videoGameDate").value)}`, {cache:"no-store"});
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const games = await response.json();
    picker.replaceChildren(new Option("Select a game", ""));
    for (const game of games || []) {
      const score = `${game.away?.score ?? 0}-${game.home?.score ?? 0}`;
      const label = `${game.displayStart || ""} · ${game.away?.name || "Away"} at ${game.home?.name || "Home"} · ${score} · ${game.detailedStatus || game.status || ""}`;
      picker.appendChild(new Option(label, String(game.gamePk)));
    }
    if (!games?.length) picker.replaceChildren(new Option("No games found for this date", ""));
    if (selectedGamePk && [...picker.options].some(option => option.value === selectedGamePk)) picker.value = selectedGamePk;
  } catch (error) { picker.replaceChildren(new Option(`Unable to load games: ${error.message}`, "")); }
}

function selectVideoGame() {
  const gamePk = $("videoGamePicker").value;
  if (!gamePk) return;
  $("videoGamePk").value = gamePk;
  updateVideoPreviewLink();
}

$("videoGameDate")?.addEventListener("change", loadVideoGames);
$("refreshVideoGames")?.addEventListener("click", loadVideoGames);
$("videoGamePicker")?.addEventListener("change", selectVideoGame);
loadVideoGames();

function videoSettingsBody() {
  const [width, height] = $("videoResolution").value.split("x").map(Number);
  const gamePk = Number($("videoGamePk").value);
  return {ffmpegPath:$("videoFfmpegPath").value.trim(), protocol:$("videoProtocol").value,
    destination:$("videoDestination").value.trim(), port:Number($("videoPort").value),
    scene:$("videoScene").value, templateId:$("videoTemplate").value || null, gamePk:gamePk > 0 ? gamePk : null, width, height,
    frameRate:Number($("videoFrameRate").value), videoBitrateKbps:Number($("videoBitrate").value),
    srtLatencyMs:Number($("videoSrtLatency").value)};
}

function applyVideoSettings(value) {
  if (!value) return;
  $("videoFfmpegPath").value=value.ffmpegPath||""; $("videoProtocol").value=value.protocol||"udp";
  $("videoDestination").value=value.destination||""; $("videoPort").value=value.port||5004;
  $("videoScene").value=value.scene||"gamecenter-standard"; $("videoGamePk").value=value.gamePk||"";
  if ($("videoGamePicker") && [...$("videoGamePicker").options].some(option => option.value === String(value.gamePk || ""))) {
    $("videoGamePicker").value = String(value.gamePk);
  }
  $("videoTemplate").value=value.templateId||"default";
  $("videoResolution").value=`${value.width||1920}x${value.height||1080}`; $("videoFrameRate").value=value.frameRate||30;
  $("videoBitrate").value=value.videoBitrateKbps||6000; $("videoSrtLatency").value=value.srtLatencyMs||120;
  updateVideoProtocolFields();
  updateVideoPreviewLink();
}

function updateVideoProtocolFields() { $("srtLatencyField").hidden=$("videoProtocol").value!=="srt"; $("videoTemplateField").hidden=$("videoScene").value!=="game-workspace"; }

function videoPreviewUrl() {
  const settings = videoSettingsBody();
  const query = new URLSearchParams({scene: settings.scene});
  if (settings.gamePk) query.set("gamePk", String(settings.gamePk));
  if (settings.scene === "game-workspace" && settings.templateId) query.set("template", settings.templateId);
  return `/output.html?${query}`;
}

function updateVideoPreviewLink() {
  const link = $("checkVideoOutput");
  if (link) link.href = videoPreviewUrl();
}

async function loadWorkspaceTemplates() {
  try {
    const response=await fetch("/api/workspace/templates",{cache:"no-store"}); const templates=await response.json();
    [$("videoTemplate")].forEach(select => {
      const selected=select.value; select.replaceChildren(...templates.map(item=>new Option(item.name,item.id))); select.value=selected || "default";
    });
    updateIntegrationUrls(); updateVideoProtocolFields();
  } catch { }
}

async function loadVideoStatus() {
  try {
    const response=await fetch("/api/video/status",{cache:"no-store"}); if(!response.ok) throw new Error(`HTTP ${response.status}`);
    const data=await response.json(); if(!window.videoSettingsLoaded){applyVideoSettings(data.settings);window.videoSettingsLoaded=true;}
    const worker=data.worker||{}; $("videoHelperConnected").textContent=yesNo(worker.connected); $("videoEncoderRunning").textContent=yesNo(worker.running);
    $("videoOutputUrl").textContent=worker.outputUrl||"—"; $("videoProcessId").textContent=worker.ffmpegProcessId||"—";
    $("videoOutputBanner").textContent=worker.message||"Video output status loaded.";
    $("videoOutputBanner").classList.toggle("ok",Boolean(worker.running)); $("videoOutputBanner").classList.toggle("bad",data.desiredRunning&&!worker.running);
  } catch(error) { $("videoOutputBanner").textContent=`Unable to load video output status: ${error.message}`; }
}

async function saveVideoSettings() {
  const response=await fetch("/api/video/settings",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(videoSettingsBody())});
  const body=await response.json().catch(()=>({})); if(!response.ok) throw new Error(body.message||`HTTP ${response.status}`);
  $("videoOutputBanner").textContent=body.message; await loadVideoStatus();
}

async function requestVideoOutput(action) {
  try {
    if(action==="start") await saveVideoSettings();
    const response=await fetch(`/api/video/${action}`,{method:"POST"}); const body=await response.json().catch(()=>({}));
    if(!response.ok) throw new Error(body.message||`HTTP ${response.status}`); $("videoOutputBanner").textContent=action==="start"?"Starting the dedicated GameCenter video output...":body.message;
    if(action==="start") {
      const deadline=Date.now()+20000;
      while(Date.now()<deadline){await new Promise(resolve=>setTimeout(resolve,1000));await loadVideoStatus();const status=await fetch("/api/video/status",{cache:"no-store"}).then(value=>value.json());if(status.worker?.running||!status.desiredRunning)break;}
    } else setTimeout(loadVideoStatus,500);
  } catch(error) { $("videoOutputBanner").textContent=`Video output request failed: ${error.message}`; $("videoOutputBanner").classList.add("bad"); }
}

$("videoProtocol")?.addEventListener("change",updateVideoProtocolFields);
$("videoScene")?.addEventListener("change",()=>{updateVideoProtocolFields();updateVideoPreviewLink();});
$("videoGamePk")?.addEventListener("input",updateVideoPreviewLink);
$("videoTemplate")?.addEventListener("change",updateVideoPreviewLink);
$("saveVideoSettings")?.addEventListener("click",()=>saveVideoSettings().catch(error=>{$("videoOutputBanner").textContent=error.message;}));
$("startVideoOutput")?.addEventListener("click",()=>requestVideoOutput("start"));
$("stopVideoOutput")?.addEventListener("click",()=>requestVideoOutput("stop"));
updateVideoProtocolFields(); updateVideoPreviewLink(); loadWorkspaceTemplates(); loadVideoStatus(); setInterval(loadVideoStatus,3000);

async function loadAdvertisingStatus() {
  try {
    const response=await fetch("/api/advertising/status",{cache:"no-store"}); if(!response.ok) throw new Error(`HTTP ${response.status}`);
    const data=await response.json();
    $("railAdStatus").textContent=data.rail ? `${data.rail.fileName} (${data.rail.mediaType}) · Checked and auto-fitted when displayed` : "No media uploaded.";
    $("bannerAdStatus").textContent=data.banner ? `${data.banner.fileName} (${data.banner.mediaType}) · Checked and auto-fitted when displayed` : "No media uploaded.";
    $("advertisingBanner").textContent="Advertising media is ready."; $("advertisingBanner").classList.add("ok");
  } catch(error) { $("advertisingBanner").textContent=`Unable to load advertising media: ${error.message}`; $("advertisingBanner").classList.add("bad"); }
}

async function uploadAdvertising(slot) {
  const input=$(slot === "rail" ? "railAdFile" : "bannerAdFile");
  if(!input.files?.length){$("advertisingBanner").textContent="Choose an image or video first.";return;}
  const form=new FormData(); form.append("media",input.files[0]);
  $("advertisingBanner").textContent=`Uploading ${slot === "rail" ? "left rail" : "bottom banner"} media...`;
  const response=await fetch(`/api/advertising/${slot}`,{method:"POST",body:form}); const body=await response.json().catch(()=>({}));
  if(!response.ok) throw new Error(body.message||`HTTP ${response.status}`); input.value=""; $("advertisingBanner").textContent=body.message; await loadAdvertisingStatus();
}

async function removeAdvertising(slot) {
  const response=await fetch(`/api/advertising/${slot}`,{method:"DELETE"}); const body=await response.json().catch(()=>({}));
  if(!response.ok) throw new Error(body.message||`HTTP ${response.status}`); $("advertisingBanner").textContent=body.message; await loadAdvertisingStatus();
}

$("uploadRailAd")?.addEventListener("click",()=>uploadAdvertising("rail").catch(error=>{$("advertisingBanner").textContent=error.message;}));
$("uploadBannerAd")?.addEventListener("click",()=>uploadAdvertising("banner").catch(error=>{$("advertisingBanner").textContent=error.message;}));
$("removeRailAd")?.addEventListener("click",()=>removeAdvertising("rail").catch(error=>{$("advertisingBanner").textContent=error.message;}));
$("removeBannerAd")?.addEventListener("click",()=>removeAdvertising("banner").catch(error=>{$("advertisingBanner").textContent=error.message;}));
loadAdvertisingStatus();
