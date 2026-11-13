// Diagnostic only. No production selectors or display placement generated.
const S=require('./r14/semantic_scan_r17.cjs'),fs=require('fs'),crypto=require('crypto');
const slug='alfa-aglae-souza',rows=S.scan(slug).rows;
const neighborhoods={warmup:[57.63441,46.0832,82.864],game_spawn:[-512.912,58.2731,64.641],town_recorded:[60.59,25.52,82.91]};
function basis(r){let xx=0,xz=0,zz=0;for(let p of r.vertices){let x=p[0]-r.center[0],z=p[2]-r.center[2];xx+=x*x;xz+=x*z;zz+=z*z}let a=.5*Math.atan2(2*xz,xx-zz),right=[Math.cos(a),0,Math.sin(a)],normal=S.cross(right,[0,1,0]);let q=r.vertices.map(p=>[S.dot(S.sub(p,r.center),right),p[1]-r.center[1],S.dot(S.sub(p,r.center),normal)]);let size=[0,1,2].map(i=>Math.max(...q.map(v=>v[i]))-Math.min(...q.map(v=>v[i])));return {right,normal,size,points:q};}
let result={scope:'All source materials and world/roof components, near actual current READY coordinates. Candidates are not approved scoreboards.',source_sha256:crypto.createHash('sha256').update(fs.readFileSync(S.source+'/maps/'+slug+'/source/map.glb')).digest('hex'),neighborhoods:{}};
for(let [name,at]of Object.entries(neighborhoods)){
 let nearby=rows.filter(r=>Math.hypot(r.center[0]-at[0],r.center[2]-at[2])<65&&Math.abs(r.center[1]-at[1])<40),candidates=[];
 for(let r of nearby){if(r.triangles<2||r.triangles>160||r.vertices.length<4)continue;let b=basis(r);if(b.size[0]<1||b.size[0]>28||b.size[1]<1.5||b.size[1]>18||b.size[2]>.8)continue;
  let edgePoints=b.points.filter(p=>Math.abs(p[0])>b.size[0]*.3||Math.abs(p[1])>b.size[1]*.3).length;
  let centerTriangles=r.normals.filter(t=>{let d=S.sub(t.center,r.center);return Math.abs(S.dot(d,b.right))<b.size[0]*.2&&Math.abs(d[1])<b.size[1]*.2}).length;
  let neighbors=nearby.filter(o=>o!==r&&S.length(S.sub(o.center,r.center))<1.2).map(o=>({key:o.key,triangles:o.triangles,center:o.center,size:o.size}));
  candidates.push({key:r.key,mesh:r.mesh,triangles:r.triangles,center:r.center,size:b.size,basis:{right:b.right,normal:b.normal},vertices:r.vertices,edge_fraction:edgePoints/b.points.length,center_triangle_centroids:centerTriangles,neighbors,classification:r.triangles===2?'solid plate: inspect frame/context':centerTriangles===0?'possible hollow/edge assembly: inspect context':'panel/structure: inspect context'});
 }
 result.neighborhoods[name]={origin:at,all_material_components:nearby.length,candidates};
 console.log(name,'components',nearby.length,'candidates',candidates.length,candidates.map(r=>({k:r.key,n:r.triangles,c:r.center.map(n=>+n.toFixed(2)),size:r.size.map(n=>+n.toFixed(2)),center:r.center_triangle_centroids})));}
fs.writeFileSync('evidence/alpha-score-neighborhoods-r19.json',JSON.stringify(result,null,2));
