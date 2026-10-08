// Skill-lane map for the discovery web. Reads the JSON injected into #web-data by build-spec.py.
const WEB=JSON.parse(document.getElementById('web-data').textContent);
const S={},T={};
WEB.tiles.forEach(t=>{T[t.id]=t;t.stages.forEach(s=>{s.tile=t;S[s.id]=s;});});

// Depth = steps from the start. "any" rows count their easiest option; "Helps" rows don't count.
const depthMemo={};
function depth(id){
  if(id in depthMemo)return depthMemo[id];
  const s=S[id];let m=0;
  if(!s.known)(s.needs||[]).forEach(n=>{if(!n.refs||n.t==='Helps')return;
    const v=n.any?Math.min(...n.refs.map(depth)):Math.max(...n.refs.map(depth));m=Math.max(m,v);});
  return depthMemo[id]=s.known?0:m+1;
}
const EDGES=[];
Object.values(S).forEach(s=>(s.needs||[]).forEach(n=>(n.refs||[]).forEach(r=>EDGES.push({a:r,b:s.id,help:n.t==='Helps'}))));

const svg=document.getElementById('websvg'),NS='http://www.w3.org/2000/svg';
const LBL=150,CW=112,NW=100,NH=24,VG=6,PAD=8,HEAD=30;
const maxD=Math.max(...Object.keys(S).map(depth));
let y=HEAD;
WEB.tiles.forEach(t=>{
  const byD={};t.stages.forEach(s=>(byD[depth(s.id)]=byD[depth(s.id)]||[]).push(s));
  const stack=Math.max(...Object.values(byD).map(l=>l.length));
  t.y=y;t.h=stack*(NH+VG)-VG+PAD*2;
  Object.entries(byD).forEach(([d,l])=>l.forEach((s,i)=>{s.x=LBL+d*CW;s.y=y+PAD+i*(NH+VG)+(stack-l.length)*(NH+VG)/2;}));
  y+=t.h;
});
const W_=LBL+(maxD+1)*CW;svg.setAttribute('viewBox',`0 0 ${W_} ${y+4}`);svg.style.minWidth=W_+'px';
function el(n,a,p){const e=document.createElementNS(NS,n);for(const k in a)e.setAttribute(k,a[k]);(p||svg).appendChild(e);return e;}
for(let d=0;d<=maxD;d++){const t=el('text',{x:LBL+d*CW,y:18,class:'colhead'});t.textContent=d===0?'Start':`Step ${d}`;}
const laneEls={};
WEB.tiles.forEach((t,i)=>{
  el('rect',{x:0,y:t.y,width:W_,height:t.h,class:'lane'+(i%2?' odd':'')});
  const g=el('g',{class:'lanelabel'+(t.belief?' belief':''),tabindex:0,role:'button','aria-label':'Skill: '+t.name});
  const tx=el('text',{x:10,y:t.y+t.h/2+4.5},g);tx.textContent=t.name;
  g.addEventListener('click',()=>selectTile(t.id));
  g.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();selectTile(t.id);}});
  laneEls[t.id]=g;
});
const edgeLayer=el('g',{});
EDGES.forEach(e=>{const s=S[e.a],d=S[e.b];const x1=s.x+NW,y1=s.y+NH/2,x2=d.x,y2=d.y+NH/2;
  const dx=Math.max(30,Math.abs(x2-x1)/2);
  e.p=el('path',{d:`M${x1},${y1} C${x1+dx},${y1} ${x2-dx},${y2} ${x2},${y2}`,class:'edge'+(e.help?' help':'')},edgeLayer);});
const nodeEls={};
Object.values(S).forEach(s=>{
  const g=el('g',{class:'node'+(s.known?' start':'')+(s.variant?' variant':''),tabindex:0,role:'button','aria-label':s.name});
  el('rect',{x:s.x,y:s.y,width:NW,height:NH,rx:4},g);
  const tx=el('text',{x:s.x+8,y:s.y+NH/2+4},g);tx.textContent=s.short;
  if(s.accident)el('circle',{cx:s.x+NW-7,cy:s.y+7,r:3.2,class:'spark'},g);
  g.addEventListener('click',()=>selectStage(s.id));
  g.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();selectStage(s.id);}});
  nodeEls[s.id]=g;
});

function walk(id,dir,acc=new Set()){EDGES.forEach(e=>{if(e.help)return;const f=dir>0?e.a:e.b,to=dir>0?e.b:e.a;
  if(f===id&&!acc.has(to)){acc.add(to);walk(to,dir,acc);}});return acc;}
function paint(keep,hot,sel,lane){
  Object.entries(nodeEls).forEach(([k,g])=>{g.classList.toggle('sel',k===sel);g.classList.toggle('dim',!keep.has(k));});
  EDGES.forEach(e=>{const on=hot(e);e.p.classList.toggle('hot',on);e.p.classList.toggle('dim',!on);});
  Object.entries(laneEls).forEach(([k,g])=>g.classList.toggle('sel',k===lane));
}
const esc=s=>String(s).replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const opensOf=id=>EDGES.filter(e=>e.a===id).map(e=>S[e.b]);
function stageLink(s){return `<button type="button" class="link" data-stage="${s.id}">${esc(s.short)}</button>`;}
function needsHTML(s){
  if(s.known)return '<p class="note">Known from the start.</p>';
  return '<div class="cond">'+(s.needs||[]).map(n=>`<div><div class="t">${esc(n.t)}${n.any?' · any one':''}</div>${esc(n.text)}${n.refs?`<div class="refs">${n.refs.map(r=>stageLink(S[r])).join(n.any?'<span class="or">or</span>':'')}</div>`:''}</div>`).join('')+'</div>';
}
const detail=document.getElementById('detail');
function selectStage(id){
  const s=S[id],up=walk(id,-1),down=walk(id,1);
  paint(new Set([id,...up,...down]),e=>!e.help&&((up.has(e.a)&&(up.has(e.b)||e.b===id))||((e.a===id||down.has(e.a))&&down.has(e.b))),id,null);
  const opens=opensOf(id);
  detail.innerHTML=`<div class="kind">${esc(s.tile.name)} · step ${depth(id)}${s.variant?' · '+esc(s.variant):''}</div>
    <h3>${esc(s.name)}</h3>
    <h4>Needs</h4>${needsHTML(s)}
    <h4>What it does</h4><p class="note">${esc(s.does)}</p>
    ${s.accident?`<h4 class="acc">Accident</h4><p class="note">${esc(s.accident)}</p>`:''}
    ${opens.length?`<h4>Opens</h4><div class="refs">${opens.map(stageLink).join('')}</div>`:''}
    <p class="note"><button type="button" class="link" data-tile="${s.tile.id}">See the whole ${esc(s.tile.name)} skill</button></p>`;
}
function selectTile(id){
  const t=T[id],ids=new Set(t.stages.map(s=>s.id));
  const near=new Set(ids);EDGES.forEach(e=>{if(ids.has(e.b))near.add(e.a);if(ids.has(e.a))near.add(e.b);});
  paint(near,e=>ids.has(e.a)||ids.has(e.b),null,id);
  detail.innerHTML=`<div class="kind">${esc(t.kind)}</div><h3>${esc(t.name)}</h3><p class="note">${esc(t.sum)}</p>
    <h4>Stages</h4><ol class="stages">${t.stages.map(s=>`<li>${stageLink(s)} <span class="muted">step ${depth(s.id)}${s.variant?' · '+esc(s.variant):''}${s.accident?' · accident-prone':''}</span></li>`).join('')}</ol>`;
}
detail.addEventListener('click',e=>{const b=e.target.closest('button.link');if(!b)return;
  if(b.dataset.stage)selectStage(b.dataset.stage);else if(b.dataset.tile)selectTile(b.dataset.tile);});
document.getElementById('web-count').textContent=`${WEB.tiles.length} skills, ${Object.keys(S).length} stages`;
selectStage('pot.fired');
