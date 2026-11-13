// Full frame family audit across all materials/layers. Geometry signature only, not a gameplay tag.
const S=require('./r14/semantic_scan_r17.cjs'),fs=require('fs');
const catalog=JSON.parse(fs.readFileSync(S.source+'/catalog.json'));
function axes(row){let xx=0,zz=0,xz=0;for(const p of row.vertices){let x=p[0]-row.center[0],z=p[2]-row.center[2];xx+=x*x;zz+=z*z;xz+=x*z;}let angle=.5*Math.atan2(2*xz,xx-zz),width=[Math.cos(angle),0,Math.sin(angle)];return {width,normal:S.cross([0,1,0],width)};}
function projected(row,origin,basis,sx=1,sz=1){return row.vertices.map(p=>{let d=S.sub(p,origin);return [S.dot(d,basis.width)*sx,d[1],S.dot(d,basis.normal)*sz]});}
function matches(row,reference,origin,basis,refOrigin,refBasis,sx,sz){if(row.triangles!==reference.triangles||row.vertices.length!==reference.vertices.length)return false;let a=projected(row,origin,basis,sx,sz),b=projected(reference,refOrigin,refBasis);return a.every(p=>b.some(q=>S.length(S.sub(p,q))<.003));}
const refRows=S.scan('battlefield-paintball-gods-pitdevil').rows;
const inner=refRows.find(r=>r.key==='p8ad59c41'&&r.triangles===12&&S.length(S.sub(r.center,[-3.960614,78.7428,26.2366]))<.01);
if(!inner)throw Error('Reference inner frame missing');
const reference=refRows.filter(r=>['p8ad59c41','pd959969c'].includes(r.key)&&S.length(S.sub(r.center,inner.center))<.13);
if(reference.length!==4||reference.reduce((a,r)=>a+r.triangles,0)!==64)throw Error('Reference complete64triangle frame missing');
const refBasis=axes(inner),complete=[],rejected=[],perMap={};
for(const map of catalog.maps){const rows=S.scan(map.slug).rows;let candidates=rows.filter(r=>r.triangles===12&&r.vertices.length===12&&Math.abs(r.size[1]-inner.size[1])<.02);let matched=0;
 for(const candidate of candidates){let basis=axes(candidate),found=null;
  for(const sx of [-1,1])for(const sz of [-1,1]){if(found)continue;if(!matches(candidate,inner,candidate.center,basis,inner.center,refBasis,sx,sz))continue;
   let nearby=rows.filter(r=>r.triangles<=32&&S.length(S.sub(r.center,candidate.center))<.14);let used=new Set(),parts=[];
   for(const expected of reference){let id=nearby.findIndex((r,i)=>!used.has(i)&&matches(r,expected,candidate.center,basis,inner.center,refBasis,sx,sz));if(id<0){parts=[];break;}used.add(id);parts.push(nearby[id]);}
   if(parts.length===4)found=parts;
  }
  if(found){matched++;complete.push({map:map.slug,fingerprint:map.cache_fingerprint,center:candidate.center,triangles:64,parts:found.map(r=>({key:r.key,mesh:r.mesh,triangles:r.triangles,vertices:r.vertices.length,center:r.center,size:r.size})),status:map.slug==='battlefield-paintball-gods-pitdevil'?'Native-verified Welcome-area hollow frame; exact aperture recovery approved':'Geometry match only; context not yet sufficient for production enablement'});}
  else rejected.push({map:map.slug,center:candidate.center,key:candidate.key,reason:'12-triangle candidate fails complete four-part64triangle point/shape signature'});
 }
 perMap[map.slug]={scanned_components:rows.length,dimension_candidates:candidates.length,complete_matches:matched};console.log(map.slug,matched);
}
const output={scope:'All105maps, all source materials and world/roof layers; complete four-component64triangle frame signature. Not a scan for arbitrary rectangles or a claim that every frame is a scoreboard.',reference:reference.map(r=>({key:r.key,triangles:r.triangles,vertices:r.vertices.length})),complete,rejected,per_map:perMap,unsupported:['Scaled or tilted variants','Frames fragmented differently by export chunking','Other authored screen/widget families without this complete signature','Omitted dynamic widgets without exported authored frame/placement evidence']};
fs.writeFileSync('evidence/complete-score-frame-profile-audit-r18.json',JSON.stringify(output,null,2));console.log(JSON.stringify({maps:catalog.maps.length,complete:complete.length,rejected:rejected.length}));
