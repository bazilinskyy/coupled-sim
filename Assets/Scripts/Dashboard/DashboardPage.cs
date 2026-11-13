/// <summary>The experimenter dashboard web page (self-contained: no internet needed in the lab).</summary>
public static class DashboardPage
{
    public const string Html = @"<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>Coupled sim dashboard</title>
<style>
:root{--bg:#f4f5f7;--panel:#fff;--ink:#1d2330;--muted:#677084;--line:#dde1e8;--accent:#2563eb;--ok:#15803d;--warn:#b45309;--bad:#b91c1c}
@media (prefers-color-scheme:dark){:root{--bg:#111418;--panel:#1a1f26;--ink:#e6e9ef;--muted:#98a2b3;--line:#2b323c;--accent:#60a5fa;--ok:#4ade80;--warn:#fbbf24;--bad:#f87171}}
*{box-sizing:border-box}[hidden]{display:none!important}body{margin:0;background:var(--bg);color:var(--ink);font:14px/1.4 system-ui,-apple-system,Segoe UI,sans-serif}
header{display:flex;gap:16px;align-items:center;padding:10px 16px;border-bottom:1px solid var(--line);background:var(--panel)}
header h1{font-size:16px;margin:0}.pill{padding:2px 8px;border-radius:999px;border:1px solid var(--line);color:var(--muted);font-size:12px}
main{display:grid;grid-template-columns:minmax(300px,380px) 1fr;gap:12px;padding:12px}
@media (max-width:820px){main{grid-template-columns:1fr}}
section{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:12px;margin-bottom:12px}
section h2{font-size:13px;text-transform:uppercase;letter-spacing:.04em;color:var(--muted);margin:0 0 8px}
button{font:inherit;padding:6px 10px;border-radius:6px;border:1px solid var(--line);background:var(--panel);color:var(--ink);cursor:pointer}
button.primary{background:var(--accent);border-color:var(--accent);color:#fff}button:disabled{opacity:.45;cursor:default}
select,input{font:inherit;padding:5px 8px;border-radius:6px;border:1px solid var(--line);background:var(--panel);color:var(--ink)}
.row{display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin:6px 0}table{width:100%;border-collapse:collapse}
td,th{padding:4px 6px;border-bottom:1px solid var(--line);text-align:left;font-size:13px}th{color:var(--muted);font-weight:500}
.ok{color:var(--ok)}.bad{color:var(--bad)}.muted{color:var(--muted)}#msg{min-height:1.4em;font-size:13px}
#mapwrap{position:relative;background:var(--panel);border:1px solid var(--line);border-radius:8px;overflow:hidden}
canvas{display:block;width:100%;height:auto;aspect-ratio:1}
.legend{display:flex;gap:12px;flex-wrap:wrap;padding:8px 12px;font-size:12px;color:var(--muted)}.dot{display:inline-block;width:10px;height:10px;border-radius:50%;margin-right:4px;vertical-align:-1px}
</style>
</head>
<body>
<header><h1>Coupled simulator</h1><span class=""pill"" id=""mode"">connecting…</span><span class=""pill"" id=""scene""></span><span class=""pill"" id=""fps""></span></header>
<main>
<div>
<section><h2>Session</h2>
<div class=""row""><button id=""startHost"" class=""primary"">Start host</button><span class=""muted"" id=""stateTxt""></span></div>
<div class=""row""><label>Experiment <select id=""experiment""></select></label></div>
<div class=""row"" id=""mapRow"" hidden><label>Map <select id=""mapSelect""></select></label><input id=""location"" placeholder=""or lat,lon[,radius]"" size=""18""><button id=""setLocation"">Use location</button></div>
<div class=""row""><label>Host mode <select id=""inputMode""><option>Flat</option><option>VR</option><option>Suite</option><option>None</option></select></label></div>
<table id=""players""><thead><tr><th>Player</th><th>Connected</th><th>Role</th><th>Ready</th></tr></thead><tbody></tbody></table>
<div class=""row""><button id=""initialise"">Initialise experiment</button><button id=""start"" class=""primary"">Start simulation</button><button id=""next"">Next trial</button></div>
<div class=""row muted"" id=""trial""></div>
</section>
<section><h2>eHMI</h2><div class=""row"" id=""hmis""><span class=""muted"">none in this experiment</span></div>
<div class=""row""><button data-ehmi=""DISABLED"">All off</button><button data-ehmi=""STOP"">All stop</button><button data-ehmi=""WALK"">All walk</button></div></section>
<section><h2>Conditions</h2>
<div class=""row""><label>Time <input id=""time"" type=""time"" value=""14:00""></label>
<label>Weather <select id=""weather""><option>Clear</option><option>PartlyCloudy</option><option>Overcast</option><option>LightRain</option><option>HeavyRain</option><option>Fog</option></select></label>
<button id=""applyEnv"">Apply</button></div>
<div class=""row""><button id=""sync"">Visual syncing</button></div></section>
<div id=""msg""></div>
</div>
<div>
<div id=""mapwrap""><canvas id=""map"" width=""1024"" height=""1024""></canvas>
<div class=""legend""><span><span class=""dot"" style=""background:#2563eb""></span>participant</span><span><span class=""dot"" style=""background:#f59e0b""></span>AV</span><span><span class=""dot"" style=""background:#9ca3af""></span>AI car</span><span><span class=""dot"" style=""background:#a855f7""></span>SUMO car</span><span><span class=""dot"" style=""background:#14b8a6""></span>two-wheeler</span><span><span class=""dot"" style=""background:#ef4444""></span>pedestrian</span></div></div>
<section style=""margin-top:12px""><h2>Agents</h2><table id=""agents""><thead><tr><th>Name</th><th>Kind</th><th>x</th><th>z</th></tr></thead><tbody></tbody></table></section>
</div>
</main>
<script>
const $=id=>document.getElementById(id);let state=null,mapImg=null,mapVersion=null,busyExp=false;
async function api(path){const r=await fetch(path);const t=await r.text();$('msg').textContent=r.ok?'':t;$('msg').className=r.ok?'':'bad';return r.ok}
$('startHost').onclick=()=>api('/api/start-host');$('initialise').onclick=()=>api('/api/initialise');$('start').onclick=()=>api('/api/start');
$('next').onclick=()=>{if(confirm('End this trial and go to the next one?'))api('/api/next-trial')};$('sync').onclick=()=>api('/api/visual-sync');
$('experiment').onchange=e=>api('/api/experiment?index='+e.target.value);
$('mapSelect').onchange=e=>api('/api/map?index='+e.target.value);$('setLocation').onclick=()=>api('/api/map?index=-1&location='+encodeURIComponent($('location').value));$('inputMode').onchange=e=>api('/api/mode?mode='+e.target.value);
document.querySelectorAll('[data-ehmi]').forEach(b=>b.onclick=()=>api('/api/ehmi?state='+b.dataset.ehmi));
$('applyEnv').onclick=()=>api('/api/environment?time='+$('time').value+'&weather='+$('weather').value);
function fillSelect(sel,items,value){if(sel.options.length!==items.length||[...sel.options].some((o,i)=>o.text!==items[i])){sel.innerHTML=items.map((n,i)=>`<option value=""${i}"">${n}</option>`).join('')}if(value!==undefined&&document.activeElement!==sel)sel.value=value}
function render(){if(!state)return;const s=state;
$('mode').textContent=s.mode==='host'?'Host · '+(s.state||''):s.mode;$('scene').textContent=s.scene||'';$('fps').textContent=Math.round(s.fps||0)+' fps';
$('startHost').disabled=s.mode!=='menu';const lobby=s.mode==='host'&&s.state==='Lobby';$('initialise').disabled=!lobby;$('start').disabled=!(s.mode==='host'&&s.state==='Ready to start');
$('mapRow').hidden=!s.maps;if(s.maps){fillSelect($('mapSelect'),s.maps,s.currentMapIndex);$('mapSelect').disabled=!lobby;$('setLocation').disabled=!lobby}
if(s.experiments)fillSelect($('experiment'),s.experiments,s.selectedExperiment);$('experiment').disabled=!lobby;
if(s.inputMode&&document.activeElement!==$('inputMode'))$('inputMode').value=s.inputMode;
$('trial').textContent=s.trial?`${s.trial.participant?'Participant '+s.trial.participant+', t':'T'}rial ${s.trial.index+1} of ${s.trial.count}${s.trial.automatic?' (automatic)':''}`:'';
const tb=$('players').tBodies[0];tb.innerHTML='';(s.players||[]).forEach(p=>{const tr=document.createElement('tr');const roles=['(none)'].concat(s.roles||[]);
tr.innerHTML=`<td>${p.id===0?'Host':'Player '+p.id}</td><td class=""${p.connected?'ok':'muted'}"">${p.connected?'yes':'no'}</td><td></td><td class=""${p.ready?'ok':'muted'}"">${p.ready?'yes':'–'}</td>`;
const sel=document.createElement('select');roles.forEach((r,i)=>{const o=document.createElement('option');o.value=i-1;o.text=r;sel.add(o)});sel.value=p.role;sel.disabled=!lobby||!p.connected;sel.onchange=()=>api(`/api/role?player=${p.id}&role=${sel.value}`);tr.cells[2].appendChild(sel);tb.appendChild(tr)});
const h=$('hmis');if(s.hmis&&s.hmis.length){h.innerHTML='';s.hmis.forEach((st,i)=>{const d=document.createElement('span');d.innerHTML=`eHMI ${i}: <b>${st}</b> `;['DISABLED','STOP','WALK'].forEach(x=>{const b=document.createElement('button');b.textContent=x.toLowerCase();b.onclick=()=>api(`/api/ehmi?index=${i}&state=${x}`);d.appendChild(b)});h.appendChild(d)})}
if(s.environment&&document.activeElement!==$('weather'))$('weather').value=s.environment.weather;
const ab=$('agents').tBodies[0];ab.innerHTML=(s.agents||[]).map(a=>`<tr><td>${a.name}</td><td>${a.kind}${a.player?' (participant)':''}</td><td>${a.x.toFixed(1)}</td><td>${a.z.toFixed(1)}</td></tr>`).join('');
if(s.map&&s.map.version!==mapVersion){mapVersion=s.map.version;const img=new Image();img.onload=()=>{mapImg=img};img.src='/map.jpg?v='+mapVersion}
draw()}
const colors={car:'#2563eb',av:'#f59e0b','ai-car':'#9ca3af','sumo-car':'#a855f7','two-wheeler':'#14b8a6',pedestrian:'#ef4444','ai-pedestrian':'#f97316'};
function draw(){const c=$('map'),g=c.getContext('2d'),s=state;g.clearRect(0,0,c.width,c.height);if(!s)return;
let b=s.map;if(!b){const xs=(s.agents||[]).map(a=>a.x),zs=(s.agents||[]).map(a=>a.z);if(!xs.length){g.fillStyle='#888';g.font='28px system-ui';g.fillText('Waiting for the experiment…',40,60);return}
const cx=(Math.min(...xs)+Math.max(...xs))/2,cz=(Math.min(...zs)+Math.max(...zs))/2,h=Math.max(50,(Math.max(...xs)-Math.min(...xs))/2+20,(Math.max(...zs)-Math.min(...zs))/2+20);b={minX:cx-h,maxX:cx+h,minZ:cz-h,maxZ:cz+h}}
if(mapImg&&s.map)g.drawImage(mapImg,0,0,c.width,c.height);else{g.fillStyle='#20252c';g.fillRect(0,0,c.width,c.height)}
const X=x=>(x-b.minX)/(b.maxX-b.minX)*c.width,Y=z=>(1-(z-b.minZ)/(b.maxZ-b.minZ))*c.height,scale=c.width/(b.maxX-b.minX);
(s.lights||[]).forEach(l=>{g.fillStyle=l.state==='GREEN'||l.state==='BLINK_GREEN'?'#22c55e':l.state==='RED'?'#ef4444':'#eab308';g.beginPath();g.arc(X(l.x),Y(l.z),Math.max(3,scale*0.8),0,7);g.fill()});
(s.agents||[]).forEach(a=>{g.save();g.translate(X(a.x),Y(a.z));g.rotate(a.yaw*Math.PI/180);g.fillStyle=colors[a.kind]||'#fff';g.strokeStyle='#000';g.lineWidth=1.5;
if(a.kind.includes('pedestrian')){g.beginPath();g.arc(0,0,Math.max(4,scale*0.5),0,7);g.fill();g.stroke()}else{const w=a.kind==='two-wheeler'?0.7:1.8,l=a.kind==='two-wheeler'?1.8:4.4;
const W=Math.max(5,w*scale),L=Math.max(10,l*scale);g.fillRect(-W/2,-L/2,W,L);g.strokeRect(-W/2,-L/2,W,L);g.fillStyle='#fff';g.fillRect(-W/2,-L/2,W,Math.max(2,L*0.15))}
if(a.player){g.strokeStyle='#fff';g.lineWidth=3;g.beginPath();g.arc(0,0,Math.max(12,scale*3),0,7);g.stroke()}g.restore()})}
async function poll(){try{const r=await fetch('/state.json');state=await r.json();render()}catch(e){$('mode').textContent='disconnected'}setTimeout(poll,250)}poll();
</script>
</body>
</html>";
}
