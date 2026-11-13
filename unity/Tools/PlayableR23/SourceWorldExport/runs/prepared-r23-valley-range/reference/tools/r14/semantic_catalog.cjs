// Read-only recovery of recognizable baked assemblies. No source gameplay tags are claimed.
const fs=require('fs'),p=require('path'),S=require('./semantic_scan.cjs');
const {add,sub,mul,dot,length,norm}=S;
const ROOT=p.resolve(__dirname,'../..');
const rounded=v=>v.map(x=>Math.round(x*1e5)/1e5);
const average=points=>mul(points.reduce(add,[0,0,0]),1/points.length);
function plane(points){
 const center=average(points),a=Array.from({length:3},()=>[0,0,0]),v=[[1,0,0],[0,1,0],[0,0,1]];
 for(const pt of points){const d=sub(pt,center);for(let i=0;i<3;i++)for(let j=0;j<3;j++)a[i][j]+=d[i]*d[j];}
 for(let k=0;k<24;k++){let i=0,j=1;for(const [u,w] of [[0,2],[1,2]])if(Math.abs(a[u][w])>Math.abs(a[i][j])){i=u;j=w;}if(Math.abs(a[i][j])<1e-12)break;
  const t=.5*Math.atan2(2*a[i][j],a[j][j]-a[i][i]),c=Math.cos(t),s=Math.sin(t),rotation=[[1,0,0],[0,1,0],[0,0,1]];rotation[i][i]=rotation[j][j]=c;rotation[i][j]=s;rotation[j][i]=-s;
  const mm=(b,c)=>b.map(row=>c[0].map((_,j)=>row.reduce((sum,x,l)=>sum+x*c[l][j],0))),tr=b=>b[0].map((_,j)=>b.map(row=>row[j]));
  const b=mm(mm(tr(rotation),a),rotation),w=mm(v,rotation);for(let x=0;x<3;x++)for(let y=0;y<3;y++){a[x][y]=b[x][y];v[x][y]=w[x][y];}
 }
 let id=0;for(let i=1;i<3;i++)if(a[i][i]<a[id][id])id=i;let normal=norm(v.map(row=>row[id]));if(normal[1]<0)normal=mul(normal,-1);
 return {center,normal,thickness:Math.max(...points.map(pt=>dot(sub(pt,center),normal)))-Math.min(...points.map(pt=>dot(sub(pt,center),normal)))};
}
function joinChunks(rows){
 const parent=rows.map((_,i)=>i),seen=new Map(),root=i=>{while(parent[i]!==i){parent[i]=parent[parent[i]];i=parent[i];}return i;};
 for(let i=0;i<rows.length;i++)for(const v of rows[i].vertices){const k=rows[i].key+':'+v.map(x=>Math.round(x*1000)).join(',');if(seen.has(k)){const j=seen.get(k);if(rows[i].mesh!==rows[j].mesh)parent[root(i)]=root(j);}else seen.set(k,i);}
 const groups=new Map();for(let i=0;i<rows.length;i++){const id=root(i);if(!groups.has(id))groups.set(id,[]);groups.get(id).push(rows[i]);}
 return [...groups.values()].map(members=>{if(members.length===1)return members[0];const min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity],points=new Map();let triangles=0,color=[0,0,0],weights=0;for(const r of members){triangles+=r.triangles;if(r.color){color=add(color,mul(r.color,r.triangles));weights+=r.triangles;}for(let i=0;i<3;i++){min[i]=Math.min(min[i],r.min[i]);max[i]=Math.max(max[i],r.max[i]);}for(const v of r.vertices)points.set(v.map(x=>Math.round(x*1000)).join(','),v);}
  return {key:members[0].key,mesh:'joined source chunks',min,max,size:sub(max,min),center:mul(add(min,max),.5),triangles,vertices:[...points.values()],color:weights?mul(color,1/weights):null,members};});
}
function recover(slug){
 const result=S.scan(slug,(key,row)=>key==='vc'||/jumppad|gloss_01_(?:white|green)|gb_tower_panel_01|metal_gb_(?:tile|trim)_black_01/i.test(JSON.stringify(row||{})));
 const rows=joinChunks(result.rows),materials=result.provenance.materials,texture=r=>JSON.stringify(materials[r.key]||{}),near=(a,b,d)=>length(sub(a.center,b.center))<d;
 const gold=r=>r.key==='vc'&&r.color&&r.color[0]>.99&&Math.abs(r.color[1]-.77647)<.012&&r.color[2]<.012;
 const rings=rows.filter(r=>r.triangles===104&&r.vertices.length===52&&r.size[1]<.1&&r.size[0]>.95&&r.size[0]<1.5&&r.size[2]>.95&&r.size[2]<1.5);
 const warmup=[],deathmatch=[],selectors=[],decorative=[];
 const addDecor=r=>{for(const member of r.members||[r])decorative.push({key:member.key,triangles:member.triangles,center:rounded(member.center),min:rounded(member.min),max:rounded(member.max)});};
 for(const ring of rings){
  const white=/gloss_01_white/i.test(texture(ring)),black=/gb_tower_panel_01/i.test(texture(ring));if(!white&&!black)continue;
  const parts=rows.filter(r=>r.key===ring.key&&r.min[1]>=ring.min[1]-.07&&r.max[1]<ring.center[1]+2.15&&Math.max(...r.size)<2.15&&Math.hypot(r.center[0]-ring.center[0],r.center[2]-ring.center[2])<1.25);
  const arrows=parts.filter(r=>r.triangles===6&&r.size[1]<.1&&near(r,ring,1.1)&&length(sub(r.center,ring.center))>.4);
  const head=parts.some(r=>r.triangles===26&&r.size[1]>.15&&r.size[1]<.35&&r.center[1]-ring.center[1]>1.3&&r.center[1]-ring.center[1]<1.95);
  if(!arrows.length||black&&!head)continue;
  const delta=sub(arrows[0].center,ring.center);delta[1]=0;
  const row={center:rounded(ring.center),normal:[0,1,0],forward:rounded(norm(delta)),evidence:white?'104-triangle white annulus +6-triangle direction arrow':'104-triangle dark annulus +direction arrow +26-triangle humanoid head'};
  (white?warmup:deathmatch).push(row);
  for(const r of parts){if(white&&r!==ring&&!arrows.includes(r))continue;for(const member of r.members||[r])selectors.push({key:member.key,triangles:member.triangles,center:rounded(member.center),min:rounded(member.min),max:rounded(member.max)});}
 }
 const jump=[],health=[],arrows=rows.filter(r=>gold(r)&&r.triangles===52&&r.vertices.length===15&&Math.max(...r.size)<2),jumpParts=rows.filter(r=>/jumppad/i.test(texture(r)));
 const consumed=new Set();
 for(let i=0;i<arrows.length;i++){
  if(consumed.has(i))continue;const a=arrows[i],pa=plane(a.vertices);if(pa.thickness>.16)continue;
  let pair=-1,best=Infinity;for(let j=i+1;j<arrows.length;j++){if(consumed.has(j))continue;const b=arrows[j],d=length(sub(a.center,b.center));if(d<.35||d>.85)continue;const pb=plane(b.vertices);if(Math.abs(dot(pa.normal,pb.normal))<.985||Math.abs(dot(sub(b.center,a.center),pa.normal))>.08)continue;if(d<best){best=d;pair=j;}}
  if(pair<0||!jumpParts.some(r=>near(r,a,2)))continue;
  const b=arrows[pair];let center=average([pa.center,plane(b.vertices).center]),normal=pa.normal,face='chevron-plane fallback';
  const frames=jumpParts.filter(r=>r.triangles===12&&r.vertices.length===12&&length(sub(r.center,center))<1.7).map(r=>({row:r,p:plane(r.vertices)}));
  const rim=frames.filter(f=>f.p.thickness<.008&&Math.max(...f.row.size)>1.2).sort((a,b)=>length(sub(a.row.center,center))-length(sub(b.row.center,center)))[0];
  if(rim){center=rim.p.center;normal=rim.p.normal;face='thin hexagonal launch rim';}
  else {const supports=frames.filter(f=>f.p.thickness>.04&&f.p.thickness<.09);if(supports.length===3){const pl=plane(supports.map(f=>f.p.center));center=pl.center;normal=pl.normal;face='three triangular launch-frame supports';}}
  // Exactly vertical frames have no upward hemisphere. The chevron pair's box
  // centers sit in front of the frame; use that authored offset to choose facing.
  const frontPoint=normal[1]<.001?average([a.center,b.center]):null;
  if(frontPoint&&dot(normal,sub(frontPoint,center))<0){normal=mul(normal,-1);normal[1]=Math.abs(normal[1]);face+='; chevron-front facing';}
  // Holographic chevrons can differ from the physical launch face. Prefer the orange frame plane.
  const site={center:rounded(center),normal:rounded(normal),radius:1.3,speed:45,evidence:'paired gold52-triangle chevrons +jumppad material; '+face,tilted:normal[1]<.985};if(frontPoint)site.front_point=rounded(frontPoint);
  jump.push(site);addDecor(a);addDecor(b);consumed.add(i);consumed.add(pair);
 }
 const greens=rows.filter(r=>/gloss_01_green/i.test(texture(r))&&r.triangles===6&&r.vertices.length===7&&Math.max(...r.size)>1&&Math.max(...r.size)<2.7);
 const goldBoxes=rows.filter(r=>gold(r)&&r.triangles===12&&Math.max(...r.size)<1);
 for(const green of greens){
  const crosses=goldBoxes.filter(r=>r.center[1]>green.center[1]+.25&&r.center[1]<green.center[1]+1.5&&Math.hypot(r.center[0]-green.center[0],r.center[2]-green.center[2])<.22);
  const crossPair=crosses.some((a,i)=>crosses.some((b,j)=>j>i&&near(a,b,.08)&&Math.abs(a.size[1]-b.size[1])>.25));
  const blackBase=rows.some(r=>/metal_gb_(?:tile|trim)_black/i.test(texture(r))&&[108,126].includes(r.triangles)&&near(r,green,.35));
  if(!crossPair||!blackBase)continue;
  const pl=plane(green.vertices);health.push({center:rounded(pl.center),normal:rounded(pl.normal),radius:1.12,health_per_second:10,evidence:'green6-triangle hexagonal face +black108/126-triangle base +two gold12-triangle cross bars'});for(const r of crosses)addDecor(r);
 }
 const manifest=JSON.parse(fs.readFileSync(p.join(S.source,'maps',slug,'converted','manifest.json')));
 return {source_sha256:manifest.source_sha256,cache_fingerprint:manifest.cache_fingerprint,warmup,deathmatch,jump,health,marker_parts:selectors,decorative_collision_parts:decorative,source_tags_present:false,coverage:{scanned_components:rows.length,warmup:warmup.length,deathmatch:deathmatch.length,jump:jump.length,tilted_jump:jump.filter(r=>r.tilted).length,health:health.length,marker_parts:selectors.length}};
}
if(require.main===module){const catalog=JSON.parse(fs.readFileSync(p.join(S.source,'catalog.json'))),output={version:'r16-geometry-roofs-v1',source_tags_present:false,description:'Hash-bound recognition of native baked authoring assemblies; no source gameplay tags were available. Unsupported or absent assemblies retain existing validated fallbacks.',maps:{}};
 for(const item of catalog.maps){const slug=item.slug;if(process.argv[2]&&slug!==process.argv[2])continue;const at=Date.now();output.maps[slug]=recover(slug);console.log(slug,JSON.stringify(output.maps[slug].coverage),Date.now()-at+'ms');}
 const dst=p.join(ROOT,'assets/data/map_semantics_r14.json');fs.writeFileSync(dst,JSON.stringify(output));console.log('WROTE',dst,Object.keys(output.maps).length);
}
module.exports={plane,recover,joinChunks};
