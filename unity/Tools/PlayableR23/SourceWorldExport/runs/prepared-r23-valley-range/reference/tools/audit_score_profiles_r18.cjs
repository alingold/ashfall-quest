const S=require('./r14/semantic_scan_r17.cjs'),fs=require('fs');
const catalog=JSON.parse(fs.readFileSync(S.source+'/catalog.json'));
const reference=S.scan('melipop-gods-poplightning',k=>k==='t52').rows.find(r=>S.length(S.sub(r.center,[-34.2385,216.251,56.477]))<.2);
if(!reference)throw Error('reference missing');
const rows=[];
for(const map of catalog.maps){
 const scan=S.scan(map.slug,(k,m)=>k==='t52'||/flat_01_black/.test(JSON.stringify(m||{})));
 for(const r of scan.rows){if(r.triangles!==reference.triangles)continue;
 if(r.size.every((v,i)=>Math.abs(v-reference.size[i])<.02))rows.push({map:map.slug,key:r.key,center:r.center,size:r.size,triangles:r.triangles,fingerprint:map.cache_fingerprint});}
}
fs.writeFileSync('evidence/score-profile-matches-r18.json',JSON.stringify({reference:{center:reference.center,size:reference.size,triangles:reference.triangles},rows},null,2));console.log(rows);
