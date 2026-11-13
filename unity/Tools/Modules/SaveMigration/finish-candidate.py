from pathlib import Path
import json,hashlib
r=Path.cwd();p=r/'unity/Assets/Ashfall/Modules/SaveMigration/SemanticMigration.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('if (bytes == null) return s;','originals[s.Path] = "null";\n                if (bytes == null) return s;');s=s.replace('new[]{"",".backup",".lock"}','new[]{"",".pending",".backup",".backup.pending",".lock"}');p.write_text(s,encoding='utf-8')
p=r/'unity/Assets/Ashfall/Modules/SaveMigration/PersonalSettingsPolicy.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('public static string ForSave(','''private static readonly string[] SavedRuleKeys = { "walk_speed_multiplier", "fling_power", "sprint_slow_gain", "sprint_fast_gain", "sprint_stamina_seconds", "player_damage_multiplier", "enemy_damage_multiplier" };
        public static string ForSourceSave(string currentJson, IEnumerable<PersonalOverride> mapEntries, string cachedLocalJson, bool liveNetwork, SourceProfile profile)
        {
            if (!liveNetwork) return profile == SourceProfile.Quest ? ForSave(currentJson,mapEntries) : RawJson.Encode(RawJson.Object(currentJson));
            var result=RawJson.Object(currentJson);
            var cached=RawJson.Object(cachedLocalJson);
            foreach(string key in SavedRuleKeys) { string value; if(cached.TryGetValue(key,out value))result[key]=value; }
            return RawJson.Encode(result);
        }
        public static string ForSave(''');s=s.replace('string current; if(!values.TryGetValue(e.Key,out current))continue;','string current; if(Array.IndexOf(SavedRuleKeys,e.Key)<0 || !values.TryGetValue(e.Key,out current))continue;');p.write_text(s,encoding='utf-8')
p=r/'unity/Tools/Modules/SaveMigration/MigrationTests.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('Convert.FromBase64String(RawJson.String(Value(v,P("payload")))).Length==original.Length','RawJson.String(Value(v,P("payload")))==Convert.ToBase64String(original)');marker='var fail=new FailingFiles{FailCommit=true};';s=s.replace(marker,'''Check(!SemanticMigration.Commit(legacy,new AtomicSettingsStore(P("legacy"))).Accepted,"missing expanded source destination alias rejected");
        Check(Value(PersonalSettingsPolicy.ForSourceSave("{\\"walk_speed_multiplier\\":3}",entries,"{}",false,SourceProfile.SkinLibrary),"walk_speed_multiplier")=="3","skin library has no offline map save policy");
        Check(Value(PersonalSettingsPolicy.ForSourceSave("{\\"walk_speed_multiplier\\":4}",entries,"{\\"walk_speed_multiplier\\":2}",true,SourceProfile.Quest),"walk_speed_multiplier")=="2","live cached personal overrides effective and deliberate edits");
        '''+marker);p.write_text(s,encoding='utf-8')
# Additional actual source symbols: pin exact small function excerpts without duplicating large runtime scripts.
e=r/'docs/agent-runs/20261104-three-chats/unity/SaveMigration/phase03'; records=json.loads((e/'inputs.json').read_text());out=r/'campaign-staging/20261104-three-chats/unity/SaveMigration/phase03/inputs'
for tree in ['quest-r22/source','game']:
 for file,start,end in [('net_bridge1000.gd','func personal_settings','func _changed'),('main.gd','func _save_settings','func _build_radio')]:
  path=tree+'/scripts/'+file;data=(r/path).read_bytes();text=data.decode('utf-8-sig');excerpt=text[text.index(start):text.index(end,text.index(start))]
  dest=out/(path+'.excerpt.txt');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(excerpt,encoding='utf-8')
  records.append(dict(path=path,sha256=hashlib.sha256(data).hexdigest().upper(),snapshot=str(dest.relative_to(r)),snapshotSha256=hashlib.sha256(dest.read_bytes()).hexdigest().upper(),excerpt=True))
(e/'inputs.json').write_text(json.dumps(records,indent=2))
p=r/'unity/Tools/Modules/SaveMigration/run-phase03.py';s=p.read_text(encoding='utf-8-sig').replace("==row['sha256'],row['path']","==row.get('snapshotSha256',row['sha256']),row['path']");p.write_text(s,encoding='utf-8')
p=r/'unity/Tools/Modules/SaveMigration/freeze-inputs.py';s=p.read_text(encoding='utf-8-sig').replace('paths=[',"assert not (r/'docs/agent-runs/20261104-three-chats/unity/SaveMigration/phase03/inputs.json').exists(), 'Frozen inputs already exist; use a new version, never overwrite'\npaths=[",1).replace("for entry in accepted['checkedFiles'] if 'checkedFiles' in accepted else []: pass","for row in records:\n if '/Modules/Persistence/' in row['path']:\n  assert any(x['expected']==row['sha256'] and x['actual']==row['sha256'] for x in accepted['sourceChecks'])");p.write_text(s,encoding='utf-8')
