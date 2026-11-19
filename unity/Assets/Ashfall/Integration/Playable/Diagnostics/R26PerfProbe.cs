#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable.Diagnostics
{
    // Allocation-free interval marks for rig runs. Every call is one static bool
    // test unless an explicit R25NetBot probe enabled them; never gameplay state.
    public static class R26PerfMarks
    {
        public const int Frame=0,Updates=1,Lan=2,Avatars=3,Cull=4,BotTick=5,Fixed=6,Count=256;
        public static readonly string[] Names=new string[Count];static int named=Fixed;
        static readonly System.Collections.Generic.Dictionary<string,int> slots=new System.Collections.Generic.Dictionary<string,int>(StringComparer.Ordinal);
        static R26PerfMarks(){Names[Frame]="scriptFrame";Names[Updates]="scriptUpdates";Names[Lan]="lanSession";Names[Avatars]="avatars";Names[Cull]="pickupCull";Names[BotTick]="botTick";}
        // Named slots for diagnostic builds (tools/mp-rig/diag); the product code uses the fixed ids above.
        static int Slot(string name){int id;if(slots.TryGetValue(name,out id))return id;if(named>=Count)return -1;id=named++;Names[id]=name;slots.Add(name,id);return id;}
        public static void Begin(string name){if(!Enabled)return;int id=Slot(name);if(id>=0)Begin(id);}
        public static void End(string name){if(!Enabled)return;int id=Slot(name);if(id>=0)End(id);}
        public static bool Enabled;
        static readonly long[] began=new long[Count],frame=new long[Count],sum=new long[Count],peak=new long[Count];static readonly int[] frameCalls=new int[Count],calls=new int[Count];
        public static void Begin(int id){if(Enabled)began[id]=Stopwatch.GetTimestamp();}
        public static void End(int id){if(!Enabled||began[id]==0)return;frame[id]+=Stopwatch.GetTimestamp()-began[id];began[id]=0;frameCalls[id]++;}
        // Folds the finished frame into the open window.
        internal static void Roll(){for(int i=0;i<Count;i++){sum[i]+=frame[i];if(frame[i]>peak[i])peak[i]=frame[i];calls[i]+=frameCalls[i];frame[i]=0;frameCalls[i]=0;}}
        internal static double FrameMs(int id){return frame[id]*1000.0/Stopwatch.Frequency;}
        internal static void Window(int id,int frames,out double averageMs,out double peakMs,out double callsPerFrame){double scale=1000.0/Stopwatch.Frequency;averageMs=frames<1?0:sum[id]*scale/frames;peakMs=peak[id]*scale;callsPerFrame=frames<1?0:calls[id]/(double)frames;sum[id]=0;peak[id]=0;calls[id]=0;}
        internal static void Reset(){for(int i=0;i<Count;i++){began[i]=frame[i]=sum[i]=peak[i]=0;frameCalls[i]=calls[i]=0;}}
    }
    // Opt-in rig probe owned by R25NetBot: frame cost, engine counters, the
    // heaviest profiler markers and world pickup audits, one JSON object per line.
    public sealed class R26PerfProbe:IDisposable
    {
        sealed class Track {internal string Name;internal ProfilerRecorder Recorder;internal long Sum,Peak,Calls;internal bool Time;}
        readonly R23PlayableLoop root;readonly StreamWriter output,audit;readonly string role;readonly float interval;readonly string[] filters;
        readonly List<ProfilerRecorderHandle> handles=new List<ProfilerRecorderHandle>();readonly List<Track> tracks=new List<Track>();readonly HashSet<string> known=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,Track> named=new Dictionary<string,Track>(StringComparer.Ordinal);
        readonly float[] wall=new float[8192],busy=new float[8192],script=new float[8192],lan=new float[8192],alloc=new float[8192];
        readonly StringBuilder text=new StringBuilder(8192);readonly List<Track> ranked=new List<Track>();
        int frames,scanned,gcBase;long lastStamp,inBase,outBase;float windowBegan,nextScan;bool disposed;string dumpPath;
        public float PlayBegan=-1;
        static readonly string[] Engine={"PlayerLoop","WaitForTargetFPS","Gfx.WaitForPresentOnGfxThread","Gfx.WaitForPresent","GC.Collect","GC.Alloc","Physics.Processing","Physics.Simulate","Physics.SyncColliderTransform","FixedUpdate.PhysicsFixedUpdate","Update.ScriptRunBehaviourUpdate","Update.ScriptRunDelayedDynamicFrameRate","PreLateUpdate.ScriptRunBehaviourLateUpdate","FixedUpdate.ScriptRunBehaviourFixedUpdate","PostLateUpdate.FinishFrameRendering","Camera.Render","Culling","RenderLoop.Draw","Inl_UniversalRenderPipeline.RenderSingleCameraInternal","UniversalRenderPipeline.RenderSingleCameraInternal","Instantiate","Destroy","GameObject.SetActive","GameObject.Activate","GameObject.Deactivate","Loading.ReadObject","Animators.Update","PreLateUpdate.DirectorUpdateAnimationEnd","CoroutinesDelayedCalls","Update.ScriptRunDelayedTasks","EarlyUpdate.UpdateMainGameViewRect","PostLateUpdate.UpdateAllRenderers","PostLateUpdate.UpdateAllSkinnedMeshes","Main Thread","GC Allocated In Frame","GC Used Memory","GC Reserved Memory","System Used Memory","Total Used Memory","Game Object Count","Object Count","Mesh Count","Material Count","Draw Calls Count","Batches Count","SetPass Calls Count","Triangles Count","Vertices Count","Visible Skinned Meshes Count","Used Buffers Count","Render Textures Count"};
        public R26PerfProbe(R23PlayableLoop root,string path,string role,float interval,string filter,string dump)
        {
            if(root==null||String.IsNullOrEmpty(path))throw new ArgumentException("Actual root and perf log path required");this.root=root;this.role=role;this.interval=Mathf.Clamp(interval,.25f,60);dumpPath=dump;
            filters=String.IsNullOrEmpty(filter)?new string[0]:filter.Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries);
            output=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read));output.AutoFlush=true;
            audit=new StreamWriter(new FileStream(Path.ChangeExtension(path,null)+".audit.jsonl",FileMode.CreateNew,FileAccess.Write,FileShare.Read));audit.AutoFlush=true;
            R26PerfMarks.Reset();R26PerfMarks.Enabled=true;lastStamp=Stopwatch.GetTimestamp();windowBegan=Time.realtimeSinceStartup;gcBase=GC.CollectionCount(0);Scan();
        }
        static bool Wanted(string name,string category,string[] filters)
        {
            // Deep-profiled method markers ("Assembly.dll!Namespace::Type.Method()") are recorded only when a filter names them.
            if(name.IndexOf(".dll!",StringComparison.Ordinal)<0&&category=="Scripts")return true;if(Array.IndexOf(Engine,name)>=0)return true;
            foreach(string filter in filters)if(name.IndexOf(filter,StringComparison.Ordinal)>=0)return true;return false;
        }
        // Markers register lazily on first use; only the unseen tail is described.
        void Scan()
        {
            handles.Clear();ProfilerRecorderHandle.GetAvailable(handles);if(handles.Count<scanned)scanned=0;
            for(int i=scanned;i<handles.Count&&tracks.Count<(filters.Length>0?6000:900);i++)
            {
                var description=ProfilerRecorderHandle.GetDescription(handles[i]);string name=description.Name;if(name==null||!Wanted(name,description.Category.Name,filters)||!known.Add(name))continue;
                var recorder=new ProfilerRecorder(handles[i],1,ProfilerRecorderOptions.Default);recorder.Start();if(!recorder.Valid){recorder.Dispose();continue;}
                var track=new Track{Name=name,Recorder=recorder,Time=description.UnitType==ProfilerMarkerDataUnit.TimeNanoseconds};tracks.Add(track);named[name]=track;
            }
            scanned=handles.Count;
        }
        long Last(string name){Track track;return named.TryGetValue(name,out track)&&track.Recorder.Valid?track.Recorder.LastValue:0;}
        // Call once per frame from the earliest script: closes the previous frame.
        public void Frame()
        {
            if(disposed)return;long stamp=Stopwatch.GetTimestamp();float now=Time.realtimeSinceStartup;double wallMs=(stamp-lastStamp)*1000.0/Stopwatch.Frequency;lastStamp=stamp;
            double loop=Last("PlayerLoop")/1e6,wait=(Last("WaitForTargetFPS")+Last("Gfx.WaitForPresentOnGfxThread"))/1e6,busyMs=loop>0?Math.Max(0,loop-wait):wallMs;
            if(frames<wall.Length){wall[frames]=(float)wallMs;busy[frames]=(float)busyMs;script[frames]=(float)R26PerfMarks.FrameMs(R26PerfMarks.Frame);lan[frames]=(float)R26PerfMarks.FrameMs(R26PerfMarks.Lan);alloc[frames]=Last("GC Allocated In Frame")/1024f;frames++;}
            R26PerfMarks.Roll();
            for(int i=0;i<tracks.Count;i++){var track=tracks[i];if(!track.Time||!track.Recorder.Valid||track.Recorder.Count<1)continue;var sample=track.Recorder.GetSample(0);track.Sum+=sample.Value;if(sample.Value>track.Peak)track.Peak=sample.Value;track.Calls+=sample.Count;}
            if(now>=nextScan){nextScan=now+(now-windowBegan<1&&tracks.Count<200?2:5);Scan();}
            if(now-windowBegan>=interval||frames>=wall.Length)Flush(now);
        }
        static void Stats(float[] values,int count,out double average,out double p50,out double p95,out double p99,out double peak)
        {
            average=p50=p95=p99=peak=0;if(count<1)return;double sum=0;for(int i=0;i<count;i++)sum+=values[i];average=sum/count;Array.Sort(values,0,count);
            p50=values[count/2];p95=values[Math.Min(count-1,(int)(count*.95))];p99=values[Math.Min(count-1,(int)(count*.99))];peak=values[count-1];
        }
        static string N(double value){return value.ToString("0.###",CultureInfo.InvariantCulture);}
        void Series(string name,float[] values)
        {double average,p50,p95,p99,peak;Stats(values,frames,out average,out p50,out p95,out p99,out peak);text.Append(",\"").Append(name).Append("\":{\"avg\":").Append(N(average)).Append(",\"p50\":").Append(N(p50)).Append(",\"p95\":").Append(N(p95)).Append(",\"p99\":").Append(N(p99)).Append(",\"max\":").Append(N(peak)).Append('}');}
        static void Quote(StringBuilder target,string value){target.Append('"');foreach(char c in value??""){if(c=='"'||c=='\\')target.Append('\\').Append(c);else if(c<32)target.Append(' ');else target.Append(c);}target.Append('"');}
        void Flush(float now)
        {
            if(frames<1){windowBegan=now;return;}float seconds=Mathf.Max(.0001f,now-windowBegan);var lobby=root.LanSession==null?null:root.LanSession.Lobby;
            text.Length=0;text.Append("{\"t\":").Append(N(now)).Append(",\"play\":").Append(N(PlayBegan<0?-1:now-PlayBegan)).Append(",\"role\":");Quote(text,role);text.Append(",\"map\":");Quote(text,root.Map==null?"":root.Map.MapId);
            text.Append(",\"state\":");Quote(text,lobby==null?"Offline":lobby.State.ToString());text.Append(",\"members\":").Append(lobby==null?0:lobby.Members.Length).Append(",\"frames\":").Append(frames).Append(",\"fps\":").Append(N(frames/seconds));
            int count=frames;Series("wallMs",wall);Series("busyMs",busy);Series("scriptMs",script);Series("lanMs",lan);Series("allocKB",alloc);
            int gc=GC.CollectionCount(0);text.Append(",\"gc\":").Append(gc-gcBase);gcBase=gc;
            long messagesIn=root.LanSession==null?0:root.LanSession.MessagesIn,messagesOut=root.LanSession==null?0:root.LanSession.MessagesOut;text.Append(",\"netIn\":").Append(messagesIn-inBase).Append(",\"netOut\":").Append(messagesOut-outBase);inBase=messagesIn;outBase=messagesOut;
            text.Append(",\"heapMB\":").Append(N(Last("GC Used Memory")/1048576.0)).Append(",\"sysMB\":").Append(N(Last("System Used Memory")/1048576.0)).Append(",\"gameObjects\":").Append(Last("Game Object Count")).Append(",\"objects\":").Append(Last("Object Count")).Append(",\"drawCalls\":").Append(Last("Draw Calls Count")).Append(",\"batches\":").Append(Last("Batches Count")).Append(",\"setPass\":").Append(Last("SetPass Calls Count")).Append(",\"tris\":").Append(Last("Triangles Count"));
            text.Append(",\"marks\":{");bool firstMark=true;for(int i=0;i<R26PerfMarks.Count;i++){if(R26PerfMarks.Names[i]==null)continue;double average,peak,calls;R26PerfMarks.Window(i,count,out average,out peak,out calls);if(!firstMark)text.Append(',');firstMark=false;text.Append('"').Append(R26PerfMarks.Names[i]).Append("\":[").Append(N(average)).Append(',').Append(N(peak)).Append(',').Append(N(calls)).Append(']');}text.Append('}');
            World(lobby);
            ranked.Clear();foreach(var track in tracks)if(track.Time&&track.Sum>0)ranked.Add(track);ranked.Sort(delegate(Track a,Track b){return b.Sum.CompareTo(a.Sum);});
            text.Append(",\"top\":[");for(int i=0;i<ranked.Count&&i<(filters.Length>0?160:28);i++){var track=ranked[i];if(i>0)text.Append(',');text.Append("{\"n\":");Quote(text,track.Name);text.Append(",\"ms\":").Append(N(track.Sum/1e6/count)).Append(",\"max\":").Append(N(track.Peak/1e6)).Append(",\"calls\":").Append(N(track.Calls/(double)count)).Append('}');}text.Append("]}");
            foreach(var track in tracks){track.Sum=0;track.Peak=0;track.Calls=0;}
            output.WriteLine(text.ToString());frames=0;windowBegan=now;
        }
        void World(R24LanLobby lobby)
        {
            text.Append(",\"world\":{");
            try
            {
                var gameplay=root.Gameplay;int weapons=0,shown=0;if(gameplay!=null&&gameplay.Ready){var ids=gameplay.WorldWeaponActionIds;weapons=ids.Length;foreach(string id in ids)if(gameplay.IsWorldWeaponVisible(id))shown++;}
                text.Append("\"weapons\":").Append(weapons).Append(",\"weaponsShown\":").Append(shown);
                if(root.Supplies!=null){var ids=root.Supplies.WorldItemIds;int visible=0;foreach(string id in ids){bool active;if(root.Supplies.TryReadWorldVisibility(id,out active)&&active)visible++;}text.Append(",\"supplies\":").Append(ids.Length).Append(",\"suppliesShown\":").Append(visible);}
                if(root.Consumables!=null)text.Append(",\"foods\":").Append(root.Consumables.WorldItemIds.Length).Append(",\"foodsShown\":").Append(FoodsShown(null));
                if(root.Explosives!=null){var ids=root.Explosives.WorldItemIds;int visible=0;foreach(string id in ids)if(root.Explosives.IsWorldItemVisible(id))visible++;text.Append(",\"grenades\":").Append(ids.Length).Append(",\"grenadesShown\":").Append(visible);}
                if(root.WorldLoot!=null)text.Append(",\"sites\":").Append(root.WorldLoot.ValidatedSites).Append(",\"sitesActive\":").Append(root.WorldLoot.ActivatedSites).Append(",\"spawned\":").Append(root.WorldLoot.SpawnedCount);
                if(lobby!=null)text.Append(",\"netLoot\":").Append(lobby.LootStates.Length).Append(",\"netSupplies\":").Append(lobby.SharedSupplies.Length).Append(",\"netFoods\":").Append(lobby.SharedFoods.Length).Append(",\"netGrenades\":").Append(lobby.SharedGrenades.Length).Append(",\"netPanels\":").Append(lobby.SharedPanels.Length);
                if(root.Rig!=null){var at=root.Rig.transform.position;text.Append(",\"at\":[").Append(N(at.x)).Append(',').Append(N(at.y)).Append(',').Append(N(at.z)).Append(']');}
            }
            catch(Exception error){text.Append("\"error\":");Quote(text,error.Message);}
            text.Append('}');
        }
        // Food props are named by id under one content root; activity there is the actual presentation.
        Transform foodRoot;readonly HashSet<string> foodShown=new HashSet<string>(StringComparer.Ordinal);
        int FoodsShown(HashSet<string> into){if(foodRoot==null){var found=GameObject.Find("R23 source consumable world props");if(found==null)return 0;foodRoot=found.transform;}int shown=0;for(int i=0;i<foodRoot.childCount;i++){var child=foodRoot.GetChild(i);if(!child.gameObject.activeInHierarchy)continue;shown++;if(into!=null)into.Add(child.name);}return shown;}
        static float Ground(Vector3 at){RaycastHit hit;return Physics.Raycast(at+Vector3.up*.25f,Vector3.down,out hit,2000,1,QueryTriggerInteraction.Ignore)?hit.distance-.25f:-1;}
        void Item(StringBuilder row,string kind,string id,Vector3 at,bool visible,Vector3 eye)
        {row.Append("{\"k\":\"").Append(kind).Append("\",\"id\":");Quote(row,id);row.Append(",\"p\":[").Append(N(at.x)).Append(',').Append(N(at.y)).Append(',').Append(N(at.z)).Append("],\"shown\":").Append(visible?1:0).Append(",\"ground\":").Append(N(Ground(at))).Append(",\"far\":").Append(N(Vector3.Distance(at,eye))).Append("},");}
        // Occasional full inventory of world pickups and of every active renderer by owner and distance.
        public void Audit(string label)
        {
            if(disposed)return;var row=new StringBuilder(65536);Vector3 eye=root.Rig.Head.transform.position;Physics.SyncTransforms();
            row.Append("{\"label\":");Quote(row,label);row.Append(",\"t\":").Append(N(Time.realtimeSinceStartup)).Append(",\"role\":");Quote(row,role);row.Append(",\"map\":");Quote(row,root.Map.MapId);row.Append(",\"eye\":[").Append(N(eye.x)).Append(',').Append(N(eye.y)).Append(',').Append(N(eye.z)).Append("],\"eyeGround\":").Append(N(Ground(eye))).Append(",\"items\":[");
            try
            {
                foodShown.Clear();FoodsShown(foodShown);
                var gameplay=root.Gameplay;if(gameplay!=null&&gameplay.Ready)foreach(string id in gameplay.WorldWeaponActionIds){string instance;Vector3 center,at;Quaternion rotation;if(gameplay.TryReadWorldWeapon(id,out instance,out center,out at,out rotation))Item(row,"weapon",id,at,gameplay.IsWorldWeaponVisible(id),eye);}
                if(root.Supplies!=null)foreach(string id in root.Supplies.WorldItemIds){Vector3 at;bool collected;if(root.Supplies.TryReadWorldItem(id,out at,out collected)&&!collected){bool shown;Item(row,"supply",id,at,root.Supplies.TryReadWorldVisibility(id,out shown)&&shown,eye);}}
                if(root.Consumables!=null)foreach(string id in root.Consumables.WorldItemIds){Vector3 at;Quaternion rotation;bool owned;if(root.Consumables.TryReadWorldItem(id,out at,out rotation,out owned)&&!owned)Item(row,"food",id,at,foodShown.Contains(id),eye);}
                if(root.Explosives!=null)foreach(string id in root.Explosives.WorldItemIds){Vector3 at;Quaternion rotation;bool owned;if(root.Explosives.TryReadWorldItem(id,out at,out rotation,out owned)&&!owned)Item(row,"grenade",id,at,root.Explosives.IsWorldItemVisible(id),eye);}
            }
            catch(Exception error){row.Append("{\"error\":");Quote(row,error.ToString());row.Append("},");}
            if(row[row.Length-1]==',')row.Length--;row.Append("],\"renderers\":{");
            // Owner = the two outermost ancestors, enough to tell map, pickups and avatars apart.
            var groups=new Dictionary<string,int[]>(StringComparer.Ordinal);
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if(!renderer.enabled)continue;Transform top=renderer.transform,second=null;while(top.parent!=null){second=top;top=top.parent;}
                string key=top.name+(second==null?"":"/"+Trim(second.name));int[] bucket;if(!groups.TryGetValue(key,out bucket)){bucket=new int[5];groups.Add(key,bucket);}
                float distance=Vector3.Distance(renderer.bounds.center,eye);bucket[0]++;if(distance>40)bucket[1]++;if(distance>100)bucket[2]++;if(renderer.isVisible)bucket[3]++;if(renderer.isVisible&&distance>40)bucket[4]++;
            }
            bool first=true;foreach(var pair in groups){if(!first)row.Append(',');first=false;Quote(row,pair.Key);row.Append(":[").Append(pair.Value[0]).Append(',').Append(pair.Value[1]).Append(',').Append(pair.Value[2]).Append(',').Append(pair.Value[3]).Append(',').Append(pair.Value[4]).Append(']');}
            row.Append("}}");audit.WriteLine(row.ToString());
        }
        // Per-instance ids follow the first slash; one owner bucket per family.
        static string Trim(string name){int cut=name.IndexOf('/');if(cut>0)name=name.Substring(0,cut);int end=name.Length;while(end>0&&(Char.IsDigit(name[end-1])||name[end-1]=='-'||name[end-1]==' '))end--;return end==0?name:name.Substring(0,end);}
        public void Dispose()
        {
            if(disposed)return;try{Flush(Time.realtimeSinceStartup);}catch(Exception){}disposed=true;R26PerfMarks.Enabled=false;
            if(!String.IsNullOrEmpty(dumpPath))try{handles.Clear();ProfilerRecorderHandle.GetAvailable(handles);using(var dump=new StreamWriter(dumpPath,false)){foreach(var handle in handles){var d=ProfilerRecorderHandle.GetDescription(handle);dump.WriteLine(d.Category.Name+"\t"+d.UnitType+"\t"+d.Name);}}}catch(Exception){}
            foreach(var track in tracks)track.Recorder.Dispose();tracks.Clear();named.Clear();output.Dispose();audit.Dispose();
        }
    }
}
#endif
