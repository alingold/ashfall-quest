from pathlib import Path
ROOT=Path('/home/al/ashfall/unity/Assets/Ashfall/Integration/Playable')
def edit(name,fn):
 p=ROOT/name;b=p.read_bytes();crlf=b'\r\n' in b;s=b.decode().replace('\r\n','\n');s=fn(s);p.write_bytes(s.replace('\n','\r\n').encode() if crlf else s.encode())
def policy(s):
 s=s.replace('using System.Collections.Generic;','using System.Collections.Generic;\nusing Ashfall.Core;')
 s=s.replace('[Serializable]sealed class Row{public string id="",source="",evidence="";public bool pods=true;}','[Serializable]sealed class Point{public float x=0,y=0,z=0;public Float3 V(){return new Float3(x,y,z);}}\n        [Serializable]sealed class Row{public string id="",source="",evidence="",verdict="",sourceSha256="";public bool pods=true;public Point[] spawns=null;}')
 s=s.replace('static Dictionary<string,bool> rows;','static Dictionary<string,bool> rows;static readonly Dictionary<string,Row> interiors=new Dictionary<string,Row>(StringComparer.Ordinal);\n        // BOXES rows use source-local coordinates (Godot Z), never world/map-top rays.\n        public static bool InteriorSpawn(string mapId,string sourceSha256)\n        {Rows();Row row;return mapId!=null&&interiors.TryGetValue(mapId,out row)&&row.sourceSha256==sourceSha256;}\n        public static Float3[] InteriorHints(string mapId,string sourceSha256)\n        {if(!InteriorSpawn(mapId,sourceSha256))return new Float3[0];var row=interiors[mapId];var result=new Float3[row.spawns.Length];for(int i=0;i<result.Length;i++)result[i]=row.spawns[i].V();return result;}')
 s=s.replace('result[row.id]=row.pods;','{result[row.id]=row.verdict=="BOXED"?false:row.pods;if(row.verdict=="BOXED"&&row.spawns!=null&&row.spawns.Length>0)interiors[row.id]=row;}')
 return s
edit('TeamLaunch/R26MapPodPolicy.cs',policy)
def planner(s):
 s=s.replace('Float3 floor;int work=0;','Float3 floor;int work=0;bool boxed=Ashfall.Integration.Playable.TeamLaunch.R26MapPodPolicy.InteriorSpawn(info.Id,info.SourceSha256);\n            if(boxed)foreach(var hint in Ashfall.Integration.Playable.TeamLaunch.R26MapPodPolicy.InteriorHints(info.Id,info.SourceSha256)){\n                if(world.Floor(Add(hint,0,.6f,0),3,out floor)&&Math.Abs(floor.Y-hint.Y)<2&&!world.Outside(floor)&&world.Room(floor,true))Unique(spawns,Add(floor,0,.12f,0),1);if(++work%12==0)yield return null;\n            }')
 s=s.replace('foreach(var p in info.Recorded){','if(!boxed)foreach(var p in info.Recorded){')
 s=s.replace('if(!recorded)foreach(var p in Sorted(loot,','if(!boxed&&!recorded)foreach(var p in Sorted(loot,')
 s=s.replace('if(native.Count>0){','if(!boxed&&native.Count>0){')
 # Boxed planner must never fall back to roof sites, even if every interior is obstructed.
 return s
edit('Match/R24ArchiveRecoveryState.cs',planner)
def selection(s):
 s=s.replace('bool forcefield=R24ArchiveRoundPolicy.Forcefield(input.Id);','bool boxed=Ashfall.Integration.Playable.TeamLaunch.R26MapPodPolicy.InteriorSpawn(input.Id,input.SourceSha256);bool forcefield=!boxed&&R24ArchiveRoundPolicy.Forcefield(input.Id);')
 s=s.replace('if(initial&&!R24ArchiveRoundPolicy.FiveLevels(input.Id))','if(!boxed&&initial&&!R24ArchiveRoundPolicy.FiveLevels(input.Id))')
 s=s.replace('if(candidates.Count==0){grid=true;','if(!boxed&&candidates.Count==0){grid=true;')
 return s
edit('Networking/R24LanSourceSpawnSelection.cs',selection)
def solo(s):
 s=s.replace('var candidates=new List<Float3>(plan.WarmupSites);candidates.Add(plan.SourceWarmup);','bool boxed=R26MapPodPolicy.InteriorSpawn(info.Id,info.SourceSha256);\n                    var candidates=new List<Float3>(boxed?plan.Spawns:plan.WarmupSites);candidates.Add(boxed?plan.SourceSpawn:plan.SourceWarmup);')
 return s
edit('R23PlayableLoop.SourceArchiveEntry.cs',solo)
def ghost(s):
 s=s.replace('if(Gameplay.LocalMatchHealth.Deaths==0){','bool boxed=R26MapPodPolicy.InteriorSpawn(info.Id,info.SourceSha256);\n            if(!boxed&&Gameplay.LocalMatchHealth.Deaths==0){')
 s=s.replace('if(candidates.Count==0){zoneGrid=true;','if(!boxed&&candidates.Count==0){zoneGrid=true;')
 return s
edit('R23PlayableLoop.ArchiveGhostRecovery.cs',ghost)
