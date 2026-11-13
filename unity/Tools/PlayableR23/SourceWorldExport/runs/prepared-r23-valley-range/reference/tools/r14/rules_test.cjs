const assert=require('assert'),{normalize}=require('./rules_audit.cjs');
const make=settings=>normalize({rules:{settings}});
let r=make([['Game type','DeathMatch'],['On death','Eliminate'],['Revive teammates','on'],['Win at score',1],['Player speed','150%'],['Damage','650%'],['Gravity','75%']]);
assert.deepStrictEqual(r.patch,{mode:'revive',walk:1.5,damage:3,score_limit:0});
assert(r.clamped.some(x=>x.requested===6.5&&x.applied===3));assert(r.ignored.some(x=>x.label==='Gravity'));assert(r.ignored.some(x=>x.label==='Win at score'));
r=make([['Game type','BRArena'],['On death','AutoRespawn'],['Win at score',600],['Time limit','9.2 min'],['Auto respawn after','17 s']]);assert.strictEqual(r.patch.mode,'respawn');assert.strictEqual(r.patch.score_limit,500);assert.strictEqual(r.patch.time_limit,552);assert.strictEqual(r.patch.respawn_delay,17);
assert.deepStrictEqual(make([]).patch,{});
const data=require('../../assets/data/map_rules_r14.json');assert.strictEqual(data.maps.length,105);assert.strictEqual(data.maps.filter(m=>m.raw_settings.length===0).length,26);for(const m of data.maps){assert(!('fling'in m.patch));assert(!('flight'in m.patch));assert.strictEqual(m.unsupported_scripts.executed,false);}
console.log('RULES_R14 generator mappings, precedence, unsupported fields and clamps pass for105 maps.');
