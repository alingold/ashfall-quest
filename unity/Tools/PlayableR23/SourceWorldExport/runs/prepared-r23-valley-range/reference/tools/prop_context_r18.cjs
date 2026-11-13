// CPU geometry-context evidence. Textured source surfaces use diagnostic gray, not recovered art.
const S=require('./r14/semantic_scan_r17.cjs'),fs=require('fs'),crypto=require('crypto');
const [slug,x,y,z,radiusText,prefix]=process.argv.slice(2),target=[x,y,z].map(Number),radius=Number(radiusText);
if(!slug||!target.every(Number.isFinite)||!Number.isFinite(radius)||!/^[a-z0-9_-]+$/.test(prefix||''))throw Error('usage: node tools/prop_context_r18.cjs SLUG X Y Z RADIUS PREFIX');
const file=S.source+'/maps/'+slug+'/source/map.glb',g=S.glb(file),triangles=[];
for(const node of g.json.nodes){if(node.mesh===undefined||!/^(world|roof)_/.test(node.name))continue;
 for(const p of g.json.meshes[node.mesh].primitives){const vs=S.accessor(g,p.attributes.POSITION),ix=p.indices===undefined?vs.map((_,i)=>i):S.accessor(g,p.indices),cs=p.attributes.COLOR_0===undefined?null:S.accessor(g,p.attributes.COLOR_0);
  for(let i=0;i<ix.length;i+=3){const ids=ix.slice(i,i+3),v=ids.map(i=>vs[i]);if(!v.some(v=>Math.hypot(...v.map((a,i)=>a-target[i]))<radius))continue;triangles.push({v,c:cs?cs[ids[0]].slice(0,3):[.4,.44,.47],node:node.name});}
 }
}
fs.writeFileSync('evidence/'+prefix+'-triangles.json',JSON.stringify({slug,target,radius,source_sha256:crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'),description:'CPU geometry context only; textured surfaces shown diagnostic gray.',triangles}));
console.log(prefix,triangles.length);
