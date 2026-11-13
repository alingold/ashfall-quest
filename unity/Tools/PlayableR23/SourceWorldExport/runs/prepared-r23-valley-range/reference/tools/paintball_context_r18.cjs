const S=require('./r14/semantic_scan_r17.cjs'),fs=require('fs');const g=S.glb(S.source+'/maps/battlefield-paintball-gods-pitdevil/source/map.glb');let out=[];const target=process.argv.slice(2).map(Number);if(target.length!==3)target.push(-161.8,55.8,-.45);
for(const node of g.json.nodes){if(node.mesh===undefined||!/^(world|roof)_/.test(node.name))continue;
for(const p of g.json.meshes[node.mesh].primitives){let vs=S.accessor(g,p.attributes.POSITION),ix=p.indices===undefined?vs.map((_,i)=>i):S.accessor(g,p.indices),cs=p.attributes.COLOR_0===undefined?null:S.accessor(g,p.attributes.COLOR_0);for(let i=0;i<ix.length;i+=3){let ids=ix.slice(i,i+3),v=ids.map(i=>vs[i]);if(!v.some(v=>Math.hypot(v[0]-target[0],v[1]-target[1],v[2]-target[2])<45))continue;out.push({v,c:cs?cs[ids[0]].slice(0,3):node.name.endsWith('t52')?[.04,.04,.04]:[.5,.55,.58]});}}}
fs.writeFileSync('evidence/paintball-context-triangles-r18.json',JSON.stringify(out));console.log(out.length);

