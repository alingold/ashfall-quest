// Read-only source audit. Writes only isolated R14 data; never mutates donor archives.
const fs=require('fs'),p=require('path'),a=require('./material_audit.cjs');
const root=p.resolve(__dirname,'../..'),cache='C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1004-audit/candidate/local_archive';
const target=p.join(root,'assets/data/map_art_audit_r14.json'),data=JSON.parse(fs.readFileSync(target));
const runtime=JSON.parse(fs.readFileSync(p.join(root,'../ASHFALL-R14-MapSystems-Evidence/MATERIAL-RUNTIME-R14.json'))).maps;
function axes(points){let mean=[0,0,0];for(const v of points)for(let i=0;i<3;i++)mean[i]+=v[i]/points.length;let xx=0,zz=0,xz=0;for(const v of points){let x=v[0]-mean[0],z=v[2]-mean[2];xx+=x*x;zz+=z*z;xz+=x*z;}const angle=.5*Math.atan2(2*xz,xx-zz),x=[Math.cos(angle),Math.sin(angle)],lo=[Infinity,Infinity,Infinity],hi=[-Infinity,-Infinity,-Infinity];let q=points.map(v=>[v[0]*x[0]+v[2]*x[1],v[1],v[0]*x[1]-v[2]*x[0]]);for(const v of q)for(let i=0;i<3;i++){lo[i]=Math.min(lo[i],v[i]);hi[i]=Math.max(hi[i],v[i]);}const size=hi.map((v,i)=>v-lo[i]);return {size,points:q.map(v=>v.map((t,i)=>(t-lo[i])/Math.max(.0001,size[i])))};}
let profile;
function each(slug,visit){const g=a.glb(p.join(cache,'maps',slug,'source/map.glb'));for(const mesh of g.j.meshes){if(!mesh.name.endsWith('_vc'))continue;for(const pr of mesh.primitives){const v=a.accessor(g,pr.attributes.POSITION),ix=a.accessor(g,pr.indices),color=pr.attributes.COLOR_0===undefined?[]:a.accessor(g,pr.attributes.COLOR_0);for(const row of a.components(v,ix,color))visit(row,row.indices.map(i=>v[i]),row.indices.map(i=>color[i]));}}}
each('alfa-aglae-souza',(row,points)=>{if(row.triangles===28&&Math.hypot(row.center[0]+257.08,row.center[1]-21.67,row.center[2]+98.47)<.05)profile={kind:'wood_panels',triangles:28,reference_map:'archive:alfa-aglae-souza',reference_center:row.center,original_color:[101,73,51],...axes(points)};});
if(!profile)throw Error('Missing verified brown panel reference');
data.repair_profiles=[profile];
for(const map of data.maps){let panels=0,wood=0;const sample=[];each(map.slug,(r,points,colors)=>{
  let kind;
  if(r.triangles===2&&Math.abs(r.size[1]-2.99868)<.004&&Math.abs(Math.hypot(r.size[0],r.size[2])-2.63269)<.005){panels++;kind='container_end_panels';}
  if(r.triangles===28&&r.colors['101,73,51']){const n=axes(points);if(Math.hypot(...n.size.map((v,i)=>v-profile.size[i]))<.03){let valid=false;for(const sx of [false,true])for(const sz of [false,true]){if(n.points.every(q=>profile.points.some(k=>Math.hypot((sx?1-q[0]:q[0])-k[0],q[1]-k[1],(sz?1-q[2]:q[2])-k[2])<.01)))valid=true;}if(valid){wood++;kind='wood_panels';}}}
  if(kind&&sample.length<8)sample.push({kind,center:r.center,triangles:r.triangles,colors:Object.keys(r.colors)});
});const rt=runtime.find(r=>r.id===map.id);if(!rt)throw Error('Missing runtime map '+map.id);
const mats=Object.entries(rt.materials),textured=mats.filter(([,v])=>v.texture),bad=textured.filter(([,v])=>!v.image_size),vc=mats.find(([k])=>k==='vc');
map.runtime_bindings={material_count:mats.length,textured_materials:textured.length,missing_or_undecodable_images:bad.map(([k])=>k),source_vertex_color_material:!!vc,intentional_pink_gloss:mats.filter(([k,v])=>k==='t56'&&v.pink_fraction>.9).map(([k,v])=>({key:k,source_name:'gloss_01_pink',texture:v.texture}))};
map.original_procedural_candidates={container_end_panels:panels,wood_panels:wood,samples:sample};
map.unresolved=map.stats.vc_components>1?'Other source vertex-color/no-UV props retain supplied flat color. Original sign/vehicle/fence/barrel art is not present in those components and has not been invented or globally replaced.':'No broadly missing texture binding detected; authored flat surfaces retained.';
console.log(map.slug,panels,wood);
}
data.audit_summary={maps:data.maps.length,textured_per_map_bindings:data.maps.reduce((s,m)=>s+m.runtime_bindings.textured_materials,0),missing_or_undecodable_images:data.maps.reduce((s,m)=>s+m.runtime_bindings.missing_or_undecodable_images.length,0),vertex_color_components:data.maps.reduce((s,m)=>s+m.stats.vc_components,0),saturated_magenta_vertex_components:data.maps.reduce((s,m)=>s+m.stats.pink_components,0),container_end_panel_candidates:data.maps.reduce((s,m)=>s+m.original_procedural_candidates.container_end_panels,0),wood_panel_candidates:data.maps.reduce((s,m)=>s+m.original_procedural_candidates.wood_panels,0),textured_exact_van_prototypes_found:0};
data.repair_policy='Original procedural detail only on exact physical container-end quads and verified brown panel point clouds. Existing image bindings, intentional pink gloss, flat map artwork, geometry, colliders, and gameplay remain unchanged. Colors retained. Two shared analytic shaders, no added texture files/dynamic lights.';
data.provenance='Donor geometry/material provenance and local-only reuse restriction retained. R14 analytic surface detail is original code; source prop artwork is unavailable and not claimed to have been recovered.';
fs.writeFileSync(target,JSON.stringify(data,null,2));console.log(data.audit_summary);
