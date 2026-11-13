const fs=require('fs'),p=require('path'),crypto=require('crypto');
if(process.argv.length!==4)throw Error('Usage: node tools/r9/index_adapters.cjs <private-prepared-directory> <file-list.json>');
const root=p.resolve(__dirname,'../..'),dir=p.resolve(process.argv[2]);
const expected=Object.keys(JSON.parse(fs.readFileSync(p.join(root,'assets/data/archive_semantics_r9.json'))).maps).map(x=>x.slice(8)+'.res').sort();
const names=fs.readdirSync(dir).filter(x=>x.endsWith('.res')).sort();
if(names.length!==105||JSON.stringify(names)!==JSON.stringify(expected))throw Error('Expected the exact 105-map prepared set');
const files={},rows=[];
for(const name of names){const source=p.join(dir,name),bytes=fs.readFileSync(source);files[name.slice(0,-4)]=crypto.createHash('sha256').update(bytes).digest('hex');rows.push({path:'local_archive_r9/'+name,source,bytes:bytes.length});}
fs.writeFileSync(p.resolve(process.argv[3]),JSON.stringify(rows,null,2));
fs.writeFileSync(p.join(root,'assets/data/archive_adapters_r9.json'),JSON.stringify({version:'r9-v4',maps:105,private_local_data:true,files},null,2));
console.log('Indexed the complete private 105-map R9 adapter set');
