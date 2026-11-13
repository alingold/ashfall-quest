const fs=require('fs'),p=require('path'),S=require('./r14/semantic_scan.cjs'),J=require('./r14/semantic_catalog.cjs');
const maps={};const catalog=JSON.parse(fs.readFileSync(p.join(S.source,'catalog.json')));
for(const map of catalog.maps){
 const scan=S.scan(map.slug,k=>k==='p2a61c272');const rows=J.joinChunks(scan.rows);
 const rings=rows.filter(r=>r.triangles>=150&&r.triangles<=220&&r.size[1]<.08&&Math.min(r.size[0],r.size[2])>2.8&&Math.max(r.size[0],r.size[2])<3.7);
 const parts=[];const seen=new Set();
 for(const ring of rings){
  const members=rows.filter(r=>r===ring||(r.size[1]<.5&&Math.max(r.size[0],r.size[2])<.4&&Math.hypot(r.center[0]-ring.center[0],r.center[2]-ring.center[2])<2&&Math.abs(r.center[1]-ring.center[1])<.6));
  for(const row of members)for(const m of row.members||[row]){const key=m.mesh+':'+m.center.join(',');if(seen.has(key))continue;seen.add(key);const size=m.size,n=m.triangles,thin=Math.min(size[0],size[2]),wide=Math.max(size[0],size[2]);
if((n>=150&&n<=192&&size[1]<.08&&thin>2.8&&wide<3.6)||(([107,112].includes(n)&&Math.abs(size[1]-.39)<.015&&thin<.07&&wide<.30)||(n===28&&Math.abs(size[1]-.1)<.015&&thin<.07&&wide<.12)))continue;
parts.push({key:m.key,triangles:m.triangles,center:m.center,min:m.min,max:m.max});}
 }
 const manifest=JSON.parse(fs.readFileSync(p.join(S.source,'maps',map.slug,'converted/manifest.json')));
 maps[map.slug]={cache_fingerprint:manifest.cache_fingerprint,sites:rings.map(r=>r.center),marker_parts:parts};console.log(map.slug,rings.length,'rings',parts.length,'parts');
}
fs.writeFileSync(p.resolve(__dirname,'../assets/data/map_caches_r17.json'),JSON.stringify({schema:1,source:'read-only cross-chunk annulus/hexagon recovery; no source tags invented',maps}));
