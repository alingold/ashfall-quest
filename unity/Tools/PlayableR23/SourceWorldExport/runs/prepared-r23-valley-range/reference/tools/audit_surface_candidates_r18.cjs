// Evidence inventory only. Neutral/white/pink vertex colors do not prove missing artwork.
const S=require('./r14/semantic_scan_r17.cjs'),fs=require('fs'),crypto=require('crypto');
const catalog=JSON.parse(fs.readFileSync(S.source+'/catalog.json')),maps={};
for(const map of catalog.maps){
 const scan=S.scan(map.slug,k=>k==='vc');
 const rows=scan.rows.filter(r=>r.color&&r.triangles<=256&&Math.max(...r.size)<60&&Math.max(...r.size)>.5&&((Math.max(...r.color)-Math.min(...r.color)<.07&&r.color[0]>.25)||(r.color[0]>.6&&r.color[2]>.4&&r.color[1]<.5))).map(({vertices,normals,...r})=>r);
 maps[map.slug]={fingerprint:map.cache_fingerprint,source_glb_sha256:crypto.createHash('sha256').update(fs.readFileSync(S.source+'/maps/'+map.slug+'/source/map.glb')).digest('hex'),candidates:rows};
 console.log(map.slug,rows.length);
}
fs.writeFileSync('evidence/surface-candidate-inventory-r18.json',JSON.stringify({description:'Untextured vertex-colored geometry candidates; intentional source colors are included. No missing-art identity or replacement permission inferred.',maps}));
