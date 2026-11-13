from pathlib import Path
import hashlib,json,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];LIVE=ROOT/'unity/Assets/Ashfall/Integration/Playable';STAGE=HERE/'Staged'
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
p=LIVE/'R23PlayableLoop.ArchiveGhostRecovery.cs';text=p.read_text();a=text.index('        Coroutine archiveSpawnRoutine;');b=text.index('        Vector3 ArchiveToWorld',a)
replacement='''        Coroutine archiveSpawnRoutine;string archiveRecoveryFailure;
        readonly R24ArchiveWorldContext archiveWorldContext=new R24ArchiveWorldContext();
        Transform archiveWorldGeometry;Matrix4x4 archiveWorldMatrix;
        public bool ArchiveWorldReady{get{return ArchiveWorldContextCurrent&&archiveSpawnPlan!=null&&archiveSpawnPlan.Ready;}}
        public long ArchiveWorldGeneration{get{return archiveWorldContext.Generation;}}
        public string ArchiveWorldMapId{get{return archiveWorldContext.Active?archiveWorldContext.MapId:null;}}
        public string ArchiveWorldFailure{get{return archiveWorldContext.Active?archiveRecoveryFailure??archiveRecoveryCatalog.Failure:null;}}
        public R24ArchiveSpawnPlan ArchiveWorldPlan{get{return ArchiveWorldReady?archiveSpawnPlan:null;}}
        public R24ArchiveRecoveryInfo ArchiveWorldInfo{get{return ArchiveWorldReady?archiveRecoveryInfo:null;}}
        public bool ArchiveGhostRecoveryReady{get{return ArchiveWorldReady;}}
        public string ArchiveGhostRecoveryFailure{get{return ArchiveWorldFailure;}}
        public R24ArchiveSpawnPlan ArchiveGhostSpawnPlan{get{return ArchiveWorldPlan;}}
        public R24ArchiveRecoveryInfo ArchiveGhostSourceInfo{get{return ArchiveWorldInfo;}}
        bool ArchiveWorldContextCurrent{get{return archiveWorldContext.Active&&Ready&&Map!=null&&Map.IsLoaded&&!Map.IsLoading&&Map.MapId==archiveWorldContext.MapId&&archiveWorldGeometry!=null&&archiveWorldGeometry.parent==Map.transform&&archiveWorldGeometry.gameObject.activeSelf&&System.Object.ReferenceEquals(archiveWorldGeometry,archiveWorldContext.GeometryToken)&&Map.transform.localToWorldMatrix.Equals(archiveWorldMatrix)&&(ActualMatchRules!=null)==archiveWorldContext.Match&&(!archiveWorldContext.Match||MatchEpoch==archiveWorldContext.MatchEpoch);}}
        Transform CurrentArchiveGeometry()
        {if(Map==null)return null;for(int i=Map.transform.childCount-1;i>=0;i--){var child=Map.transform.GetChild(i);if(child.gameObject.activeSelf&&child.name==Map.Title)return child;}return null;}
        void CancelArchiveWorldPlan()
        {if(archiveSpawnRoutine!=null)StopCoroutine(archiveSpawnRoutine);archiveSpawnRoutine=null;archiveSpawnPlan=null;archiveRecoveryInfo=null;archiveRecoveryFrame=null;archiveRecoveryFailure=null;}
        // Root calls on every update, including false during load/lobby. Menus,
        // focus and actor life do not change the actual active-world identity.
        public void PrepareArchiveWorld(bool actualWorldActive)
        {
            Transform geometry=Map==null?null:CurrentArchiveGeometry();
            bool active=actualWorldActive&&Ready&&Map!=null&&Map.IsLoaded&&!Map.IsLoading&&Map.MapId.StartsWith("archive:",StringComparison.Ordinal)&&geometry!=null;
            if(!active){if(archiveWorldContext.Update(false,null,null,null,false,0))CancelArchiveWorldPlan();archiveWorldGeometry=null;return;}
            R24LaunchMapInfo frame=null;if(LaunchMapCatalog!=null&&LaunchMapCatalog.Ready)LaunchMapCatalog.TryRead(Map.MapId,out frame);
            var matrix=Map.transform.localToWorldMatrix;bool moved=archiveWorldContext.Active&&!matrix.Equals(archiveWorldMatrix);
            if(archiveWorldContext.Update(true,Map.MapId,geometry,frame,ActualMatchRules!=null,MatchEpoch,moved))CancelArchiveWorldPlan();
            archiveWorldGeometry=geometry;archiveWorldMatrix=matrix;
            if(!archiveRecoveryCatalog.Ready){if(!archiveRecoveryCatalog.Loading&&archiveRecoveryCatalog.Failure==null)StartCoroutine(archiveRecoveryCatalog.Load());return;}
            if(frame==null||archiveSpawnPlan!=null||archiveRecoveryFailure!=null)return;
            R24ArchiveRecoveryInfo info;if(!archiveRecoveryCatalog.TryRead(Map.MapId,out info)){archiveRecoveryFailure="Actual archive source metadata missing";return;}
            if(info.SourceSha256!=frame.SourceSha256||info.ManifestSha256!=frame.ManifestSha256||info.CacheFingerprint!=frame.CacheFingerprint||info.AdapterSha256!=frame.AdapterSha256){archiveRecoveryFailure="Actual archive resource/catalog binding mismatch";return;}
            archiveRecoveryInfo=info;archiveRecoveryFrame=frame;archiveSpawnPlan=new R24ArchiveSpawnPlan(info,new ArchiveRecoveryWorld(this,info,frame));
            archiveSpawnRoutine=StartCoroutine(BuildArchiveWorldPlan(archiveSpawnPlan,ArchiveWorldGeneration,Map.MapId));
        }
        void PrepareArchiveGhostRecovery(){PrepareArchiveWorld(true);}
        IEnumerator BuildArchiveWorldPlan(R24ArchiveSpawnPlan plan,long generation,string mapId)
        {
            var work=plan.Build();
            try
            {
                while(ArchiveWorldContextCurrent&&ArchiveWorldGeneration==generation&&Map.MapId==mapId)
                {
                    bool moved;object current=null;try{moved=work.MoveNext();if(moved)current=work.Current;}catch(Exception error){archiveRecoveryFailure=error.ToString();yield break;}
                    if(!moved){archiveRecoveryFailure=plan.Failure;yield break;}yield return current;
                }
            }
            finally{var done=work as IDisposable;if(done!=null)done.Dispose();archiveSpawnRoutine=null;}
        }
        public bool TryArchiveSourceToWorld(Float3 source,out Vector3 world){world=Vector3.zero;if(!ArchiveWorldReady||!source.IsFinite)return false;world=ArchiveToWorld(source,archiveRecoveryFrame);return true;}
        public bool TryArchiveWorldToSource(Vector3 world,out Float3 source){source=new Float3();if(!ArchiveWorldReady||!new Float3(world.x,world.y,world.z).IsFinite)return false;source=ArchiveFromWorld(world,archiveRecoveryFrame);return true;}
        public bool ArchiveWorldFloor(Float3 from,float depth,out Float3 floor){floor=new Float3();return ArchiveWorldReady&&from.IsFinite&&!Single.IsNaN(depth)&&!Single.IsInfinity(depth)&&depth>0&&ArchiveFloor(from,depth,archiveRecoveryFrame,out floor);}
        public bool ArchiveWorldRoom(Float3 floor){return ArchiveWorldReady&&floor.IsFinite&&ArchiveRoom(floor,false,archiveRecoveryInfo,archiveRecoveryFrame);}
        public bool ArchiveWorldOutside(Float3 point){return !ArchiveWorldReady||ArchiveOutside(point,archiveRecoveryInfo,archiveRecoveryFrame);}
        public bool ArchiveWorldHazard(Float3 point){return ArchiveWorldReady&&point.IsFinite&&ArchiveHazard(point,archiveRecoveryInfo,archiveRecoveryFrame);}
        public bool ArchiveWorldSafeSpawn(Float3 point){return ArchiveWorldReady&&archiveSpawnPlan.Safe(point,false);}
'''
text=text[:a]+replacement+text[b:]
old='            return info.Outside(point,ceiling,exempt);';new='''            // Literal R6 warmup_contains: ALL native validated sites, not the
            // fallback map.WarmupSpawn. Actual match/results keep strict bounds.
            if(ActualMatchRules==null&&archiveSpawnPlan!=null&&archiveSpawnPlan.ContainsWarmup(point))return false;
            return info.Outside(point,ceiling,exempt);'''
assert text.count(old)==1;text=text.replace(old,new);(STAGE/p.name).write_text(text)
p=LIVE/'Match/R24ArchiveRecoveryState.cs';text=p.read_text();old='readonly List<Float3> spawns=new List<Float3>(),loot=new List<Float3>();';new='readonly List<Float3> spawns=new List<Float3>(),loot=new List<Float3>(),warmupSites=new List<Float3>();';assert text.count(old)==1;text=text.replace(old,new)
old='        public Float3[] Spawns{get{return spawns.ToArray();}}public Float3[] Loot{get{return loot.ToArray();}}';new=old+'''
        public Float3[] WarmupSites{get{return warmupSites.ToArray();}}
        public bool ContainsWarmup(Float3 point)
        {if(!Ready||!point.IsFinite)return false;foreach(var site in warmupSites){float x=point.X-site.X,z=point.Z-site.Z;if(Math.Abs(point.Y-site.Y)<=4&&x*x+z*z<=8*8)return true;}return false;}''';assert text.count(old)==1;text=text.replace(old,new)
old='            foreach(var hint in info.Warmup)if(world.Floor(Add(hint,0,1.5f,0),8,out floor)&&floor.Y>=hint.Y-6&&floor.Y<=hint.Y+1&&world.Room(floor,true)){SourceWarmup=Add(floor,0,.12f,0);break;}'
new='''            foreach(var hint in info.Warmup)
            {
                if(world.Floor(Add(hint,0,1.5f,0),8,out floor)&&floor.Y>=hint.Y-6&&floor.Y<=hint.Y+1&&world.Room(floor,true)){var point=Add(floor,0,.12f,0);warmupSites.Add(point);if(warmupSites.Count==1)SourceWarmup=point;}
                if(++work%12==0)yield return null;
            }''';assert text.count(old)==1;text=text.replace(old,new);(STAGE/'Match/R24ArchiveRecoveryState.cs').write_text(text)
rows=[]
for p in STAGE.rglob('*.cs'):
 rel=p.relative_to(STAGE).as_posix();dst=LIVE/rel;rows.append(dict(path=rel,baselineSha256=sha(dst)if dst.exists()else None,stagedSha256=sha(p)))
 meta=p.with_name(p.name+'.meta');existing=dst.with_name(dst.name+'.meta')
 if existing.exists():meta.write_bytes(existing.read_bytes())
 else:meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall/R6/archive-world/'+rel).hex+'\nMonoImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
(HERE/'candidate.json').write_text(json.dumps(dict(root=str(LIVE),files=rows),indent=2)+'\n');print('Staged exact shared Explore/match archive planner:',len(rows),'source files; no Assets edits.')
