// No source scripts execute. Normalize only explicitly supported, named settings.
const fs=require('fs'),p=require('path'),crypto=require('crypto');
const root=p.resolve(__dirname,'../..'),cache='C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1004-audit/candidate/local_archive';
const limits={damage:[0,3],walk:[.25,3],run:[.25,3],starting_health:[1,100],starting_shield:[0,100],starting_builds:[0,100000],respawn_delay:[1,30],downed_seconds:[10,180],time_limit:[0,3600],score_limit:[0,500]};
const percent=v=>typeof v==='string'&&/^\d+(\.\d+)?%$/.test(v)?parseFloat(v)/100:null;
const seconds=v=>v==='none'?0:typeof v==='string'&&/^\d+(\.\d+)? (s|min)$/.test(v)?parseFloat(v)*(v.endsWith('min')?60:1):null;
const toggle=v=>v==='on'?true:v==='off'?false:null;
function normalize(source){const raw=source.rules?.settings||[],r=Object.fromEntries(raw),patch={},mapped=[],ignored=[],clamped=[];let mode;
 if(r['On death']==='AutoRespawn')mode='respawn';
 else if(r['On death']==='Eliminate'&&r['Revive teammates']==='on')mode='revive';
 else if(r['Game type']==='BRArena'||r['Game type']==='BRRules')mode='revive';
 if(mode)patch.mode=mode;
 const put=(key,value,label,original)=>{if(value===null||!Number.isFinite(value)&&typeof value==='number')return false;let result=value;if(limits[key])result=Math.max(limits[key][0],Math.min(limits[key][1],value));if(result!==value)clamped.push({label,key,raw:original,requested:value,applied:result,reason:'ASHFALL net_rules1000 supported limit'});patch[key]=result;mapped.push({label,key,raw:original,applied:result});return true;};
 const unsupported=(label,value,reason)=>ignored.push({label,raw:value,reason});
 for(const[label,value]of raw){switch(label){
 case'Damage':if(!put('damage',percent(value),label,value))unsupported(label,value,'Unrecognized percent');break;
 case'Player speed':if(!put('walk',percent(value),label,value))unsupported(label,value,'Unrecognized percent');break;
 case'Friendly fire':if(!put('friendly_fire',toggle(value),label,value))unsupported(label,value,'Unrecognized toggle');break;
 case'Unlimited ammo':if(!put('infinite_ammo',toggle(value),label,value))unsupported(label,value,'Unrecognized toggle');break;
 case'Starting builds':if(typeof value==='number')put('starting_builds',value,label,value);else unsupported(label,value,'Unrecognized quantity');break;
 case'Max health / shield':{const m=typeof value==='string'&&value.match(/^(\d+) \/ (\d+)$/);if(m){put('starting_health',+m[1],label,value);put('starting_shield',+m[2],label,value);}else unsupported(label,value,'Unrecognized health/shield pair');break;}
 case'Time limit':if(!put('time_limit',seconds(value),label,value))unsupported(label,value,'Unrecognized duration');break;
 case'Auto respawn after':if(!put('respawn_delay',seconds(value),label,value))unsupported(label,value,'Unrecognized duration');break;
 case'Bleed-out time':if(!put('downed_seconds',seconds(value),label,value))unsupported(label,value,'Unrecognized duration');break;
 case'Team mode':if(value==='Halves')put('teams',2,label,value);else if(value==='Solo')put('teams',0,label,value);else unsupported(label,value,'Squad size does not specify number of teams');break;
 case'Win at score':if(mode==='respawn'){if(value==='none')put('score_limit',0,label,value);else if(typeof value==='number')put('score_limit',value,label,value);else unsupported(label,value,'Unrecognized score');}else unsupported(label,value,'Elimination/team round score is not an individual/team kill cap');break;
 case'On death':if(value==='AutoRespawn'||value==='Eliminate')mapped.push({label,key:'mode',raw:value,applied:mode||null});else unsupported(label,value,'Unsupported death behavior');break;
 case'Revive teammates':if(value==='on'&&r['On death']==='Eliminate')mapped.push({label,key:'mode',raw:value,applied:'revive'});else unsupported(label,value,'No independent revive-teammates toggle in supported schema');break;
 case'Game type':unsupported(label,value,mode?'Death/teammate revival semantics take precedence over generic source game-type label':'No supported exact mode mapping');break;
 default:unsupported(label,value,'No equivalent supported net_rules1000 setting; source value retained for audit');
 }}
 if(mode==='revive')patch.score_limit=0;
 const custom=source.rules?.custom||{};
 return {patch,mapped,ignored,clamped,raw_settings:raw,unsupported_scripts:{executed:false,variables:custom.variables||[],events:custom.events||[],timed_buffs:custom.timed_buffs||0,shop_categories:custom.shop_categories||[],player_classes:custom.player_classes||[]}};
}
function run(){const maps=[];for(const item of JSON.parse(fs.readFileSync(p.join(cache,'catalog.json'))).maps){const input=fs.readFileSync(p.join(cache,'maps',item.slug,'source/map.json')),source=JSON.parse(input);maps.push({id:item.id,slug:item.slug,source_map_json_sha256:crypto.createHash('sha256').update(input).digest('hex'),...normalize(source)});}const result={schema:1,scope:'All105 local archive map rule exports. No donor script execution. Patch only explicitly named supported settings; absent labels retain session defaults.',priority:'net_rules defaults < map patch < explicit host overrides. READY patch is temporary and must not persist personal settings.',mode_precedence:'On death AutoRespawn => respawn; On death Eliminate + Revive teammates on => revive; BRArena/BRRules fallback => revive. Generic DeathMatch label never overrides elimination semantics.',notes:['Player speed maps only walk: ASHFALL walk base already scales both normal and sprint speed, while run multiplies sprint gain. Mapping both would multiply the authored factor twice. Flight/fling/gravity are not inferred.','Source elimination Win at score is never mapped to kill cap; revive patch disables kill cap.','Unknown settings and custom scripts retained verbatim as unsupported.','Damage above300% and other values exceeding supported net_rules1000 limits are explicitly clamped and recorded.','No source setting labels available for26maps; their patches are empty.'],maps};fs.writeFileSync(p.join(root,'assets/data/map_rules_r14.json'),JSON.stringify(result,null,2));console.log({maps:maps.length,withRules:maps.filter(m=>m.raw_settings.length).length,patches:maps.filter(m=>Object.keys(m.patch).length).length,clamped:maps.reduce((s,m)=>s+m.clamped.length,0),scripted:maps.filter(m=>m.unsupported_scripts.events.length).length});}
if(require.main===module)run();module.exports={normalize};
