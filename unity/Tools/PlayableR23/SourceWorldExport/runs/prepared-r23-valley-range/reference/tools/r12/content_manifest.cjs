const fs=require('fs'),p=require('path'),crypto=require('crypto');
const root=p.resolve(__dirname,'../..'),files=[];
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){const f=p.join(dir,e.name);if(e.isDirectory())walk(f);else if(!f.endsWith('.import')&&!f.endsWith('.uid')&&!f.endsWith('network_content1000.json'))files.push({path:p.relative(root,f).replaceAll('\\','/'),sha256:hash(f)});}}
for(const dir of ['assets','scripts','addons'])if(fs.existsSync(p.join(root,dir)))walk(p.join(root,dir));
for(const name of ['project.godot','main.tscn','openxr_action_map.tres'])files.push({path:name,sha256:hash(p.join(root,name))});files.sort((a,b)=>a.path.localeCompare(b.path));
const content_id=crypto.createHash('sha256').update(JSON.stringify(files)).digest('hex');
fs.writeFileSync(p.join(root,'assets/data/network_content1000.json'),JSON.stringify({protocol:1000,version:'1.0.0.6-r12',edition:'ASHFALL R12 - Team sky deployment',content_id,files},null,2));console.log({content_id,files:files.length});
