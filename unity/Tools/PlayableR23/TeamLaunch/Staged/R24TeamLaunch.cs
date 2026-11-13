#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
using UnityEngine.Networking;

namespace Ashfall.Integration.Playable.TeamLaunch
{
    // Owns source hardware/pod visuals only. Root owns player life/session rules,
    // input routing and every accepted CharacterController displacement.
    public sealed class R24TeamLaunch:MonoBehaviour,IDisposable
    {
        [Serializable]sealed class Hardware{public int version=0;public string[] palette=null;public float metallic=0,roughness=1,emissionEnergy=0;public int emissionIndex=-1;public Part[] cabin=null;}
        [Serializable]sealed class Part{public float[] at=null,basis=null,size=null,vertices=null;public bool solid=false;}
        sealed class Pod{public Transform Root,Controls;public TextMesh ControlLabel;public Collider[] Colliders;public AudioSource Launch,Engine,Land;public bool Landed;}
        readonly List<UnityEngine.Object> assets=new List<UnityEngine.Object>();readonly Dictionary<string,Pod> docks=new Dictionary<string,Pod>(StringComparer.Ordinal),flights=new Dictionary<string,Pod>(StringComparer.Ordinal);
        readonly HashSet<string> pendingExit=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>(StringComparer.Ordinal);readonly Dictionary<string,byte[]> loaded=new Dictionary<string,byte[]>();
        public R24TeamLaunchRuntime Runtime{get;private set;}public bool Ready{get;private set;}public bool Loading{get;private set;}public string Failure{get;private set;}
        public bool Eligible{get{return Runtime!=null&&Runtime.Layout!=null;}}public bool Visible{get{return structure!=null&&structure.activeInHierarchy;}}
        public int DockCount{get{return docks.Count;}}public int FlightCount{get{return flights.Count;}}
        public event Action<R24LaunchIntent> ControlRequested;
        Hardware hardware;Mesh deck,cabin;Mesh[] podCollision;Material[] materials;Transform mapRoot;GameObject structure;R24LaunchLayout built;AudioSource exitVoice;Func<Vector3,bool> safeSpawn;float volume=1,refreshAge;bool enabledWorld=true,disposed,xrMode=true;double clock;string directory;
        public void Initialize(R24LaunchMap map,R24LaunchRules rules,Transform actualMap,Func<Vector3,bool> actualSafeSpawn=null,int? randomSeed=null)
        {
            if(Runtime!=null||actualMap==null)throw new InvalidOperationException("One actual launch map/session host required");mapRoot=actualMap;safeSpawn=actualSafeSpawn;Runtime=new R24TeamLaunchRuntime(randomSeed);Runtime.Configure(map,rules);directory=Application.streamingAssetsPath+"/PlayableR23/TeamLaunch/";Loading=true;StartCoroutine(Guard(Load()));
        }
        public void Configure(R24LaunchMap map,R24LaunchRules rules,Transform actualMap,Func<Vector3,bool> actualSafeSpawn=null)
        {if(Runtime==null||actualMap==null)throw new InvalidOperationException("Initialize launch host first");if(mapRoot!=actualMap)Runtime.Clear();mapRoot=actualMap;safeSpawn=actualSafeSpawn;Runtime.Configure(map,rules);if(Ready)EnsureStructure();}
        public R24LaunchAssignment AssignDeckSpawn(string playerId,int team,IEnumerable<R24LaunchAssignment> actualRoster)
        {return Eligible?Runtime.Layout.Assign(playerId,team,actualRoster):null;}
        public bool TryLaunch(R24LaunchPlayer player,string podKey,double rulesClock,IList<Float3> actualSpawnPoints,IList<Float3> actualLootPoints,Float3 actualArenaFallback,out R24LaunchRide ride)
        {ride=null;if(!Ready||!enabledWorld)return false;bool accepted=Runtime.TryLaunch(player,podKey,rulesClock,actualSpawnPoints,actualLootPoints,actualArenaFallback,new PhysicsWorld(this),out ride);clock=rulesClock;if(accepted)RefreshVisuals(0);return accepted;}
        public R24LaunchIntent SampleInput(R24LaunchPlayer player,double rulesClock,R24LaunchInput actualInput)
        {if(Runtime==null)return new R24LaunchIntent();SetXRMode(actualInput.XR);actualInput.Enabled&=Ready&&enabledWorld;var intent=Runtime.SampleInput(player,rulesClock,actualInput);if(intent.Kind!=R24LaunchIntentKind.None&&ControlRequested!=null)ControlRequested(intent);return intent;}
        public bool TryCarrier(R24LaunchPlayer player,double rulesClock,bool acceptedEjectRequest,out R24LaunchCarrier motion)
        {motion=new R24LaunchCarrier();if(!Ready||!enabledWorld)return false;var before=Runtime.ReadRide(player.Id);var old=before==null?R24LaunchRideState.Released:before.State;bool result=Runtime.TryCarrier(player,rulesClock,acceptedEjectRequest,out motion);clock=rulesClock;if(result&&old==R24LaunchRideState.Flying&&motion.State==R24LaunchRideState.Released)pendingExit.Add(player.Id);RefreshVisuals(0);return result;}
        public void AcceptCarrierMotion(string playerId,Vector3 actualAcceptedRootPosition){if(Runtime==null)return;Runtime.AcceptCarrierMotion(playerId,F(actualAcceptedRootPosition));var ride=Runtime.ReadRide(playerId);if(ride!=null&&ride.TerminalAccepted&&pendingExit.Remove(playerId))PlayExit(actualAcceptedRootPosition);}
        public Collider[] ReadCarrierColliders(string playerId){Pod pod;return flights.TryGetValue(playerId,out pod)?(Collider[])pod.Colliders.Clone():new Collider[0];}
        public void RemovePlayer(string playerId){if(playerId!=null)pendingExit.Remove(playerId);if(Runtime!=null)Runtime.RemovePlayer(playerId);if(Ready)RefreshVisuals(0);}
        public void Refresh(double rulesClock,float deltaSeconds){if(!R24LaunchMath.Finite(rulesClock)||float.IsNaN(deltaSeconds)||float.IsInfinity(deltaSeconds)||deltaSeconds<0)throw new ArgumentException("actual finite rules clock/delta");clock=rulesClock;if(Ready)RefreshVisuals(deltaSeconds);}
        public bool Staging(Vector3 point){return Eligible&&Runtime.Layout.Staging(F(point));}public float Ceiling{get{return Eligible?Runtime.Layout.Ceiling:float.NegativeInfinity;}}
        public bool AuthorizedFlight(Vector3 point,double rulesClock){return Eligible&&Runtime.AuthorizedFlight(F(point),rulesClock);}
        public void SetWorldEnabled(bool value){enabledWorld=value;if(structure!=null)structure.SetActive(value&&Eligible);if(!value)StopAudio();else foreach(var row in flights){var ride=Runtime.ReadRide(row.Key);if(ride!=null&&ride.State==R24LaunchRideState.Flying&&row.Value.Engine!=null&&!row.Value.Engine.isPlaying){var voice=row.Value.Engine;if(voice.clip.length>0)voice.time=(float)Math.Max(0,clock-ride.Started)%voice.clip.length;voice.Play();}}}
        public bool OwnsCollider(Collider collider){return collider!=null&&structure!=null&&collider.transform.IsChildOf(structure.transform);}
        public void SetXRMode(bool xr){if(xrMode==xr)return;xrMode=xr;foreach(var pod in docks.Values)if(pod.ControlLabel!=null)pod.ControlLabel.text="LAUNCH\n"+(xr?"TRIGGER / GRIP":"E");foreach(var pod in flights.Values)if(pod.ControlLabel!=null)pod.ControlLabel.text="EJECT\n"+(xr?"TRIGGER / GRIP":"E");}
        public void SetSfxVolume(float value){if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentOutOfRangeException("value");volume=Mathf.Clamp01(value);foreach(var pod in flights.Values)ApplyVolume(pod);if(exitVoice!=null)exitVoice.volume=Mathf.Pow(10,-7f/20)*volume;}
        IEnumerator Guard(IEnumerator work)
        {var stack=new Stack<IEnumerator>();stack.Push(work);while(stack.Count>0){bool more;object value;try{more=stack.Peek().MoveNext();value=more?stack.Peek().Current:null;}catch(Exception error){Failure=error.ToString();Loading=false;ClearStructure();yield break;}if(!more){stack.Pop();continue;}var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;}Loading=false;Ready=true;EnsureStructure();}
        IEnumerator Read(string file)
        {string url=directory+file;if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;using(var req=UnityWebRequest.Get(url)){yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success)throw new IOException(file+": "+req.error);loaded[file]=req.downloadHandler.data;}}
        IEnumerator Load()
        {
            yield return Read("launch-hardware.json");yield return Read("launch-deck.meshbin");yield return Read("launch-cabin.meshbin");hardware=JsonUtility.FromJson<Hardware>(System.Text.Encoding.UTF8.GetString(loaded["launch-hardware.json"]));if(hardware.version!=1||hardware.palette.Length!=8)throw new IOException("source launch hardware schema");
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("Source launch requires installed URP Lit");materials=new Material[8];for(int i=0;i<8;i++){Color color;if(!ColorUtility.TryParseHtmlString("#"+hardware.palette[i],out color))throw new IOException("source launch palette");var material=new Material(shader);assets.Add(material);materials[i]=material;material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",hardware.metallic);material.SetFloat("_Smoothness",1-hardware.roughness);if(i==hardware.emissionIndex){material.SetColor("_EmissionColor",color*hardware.emissionEnergy);material.EnableKeyword("_EMISSION");}}
            deck=Decode(loaded["launch-deck.meshbin"],"Original R6 deck");cabin=Decode(loaded["launch-cabin.meshbin"],"Original R6 cabin");podCollision=new Mesh[hardware.cabin.Length];
            for(int i=0;i<hardware.cabin.Length;i++){var part=hardware.cabin[i];if(!part.solid||part.vertices==null||part.vertices.Length==0)continue;var cloud=new Float3[part.vertices.Length/3];for(int j=0;j<cloud.Length;j++)cloud[j]=new Float3(part.vertices[j*3],part.vertices[j*3+1],part.vertices[j*3+2]);Float3[] vertices;int[] faces;if(!R24KnifeHull.TryBuild(cloud,out vertices,out faces))throw new IOException("actual source pod convex profile "+i);var mesh=new Mesh();mesh.name="Original pod profile "+i;var v=new Vector3[vertices.Length];for(int j=0;j<v.Length;j++)v[j]=V(vertices[j]);mesh.vertices=v;mesh.triangles=faces;mesh.RecalculateBounds();podCollision[i]=mesh;assets.Add(mesh);}
            loaded.Clear();foreach(string cue in new[]{"pod_launch","pod_engine","pod_land","pod_exit"}){string path=directory+cue+".wav";if(!path.Contains("://"))path=new Uri(path).AbsoluteUri;using(var req=UnityWebRequestMultimedia.GetAudioClip(path,AudioType.WAV)){yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success)throw new IOException(cue+": "+req.error);var clip=DownloadHandlerAudioClip.GetContent(req);clip.name=cue;clips.Add(cue,clip);assets.Add(clip);}}
        }
        Mesh Decode(byte[] bytes,string name)
        {
            using(var reader=new BinaryReader(new MemoryStream(bytes))){if(reader.ReadInt32()!=0x32314c54)throw new IOException("source launch mesh header");int surfaces=reader.ReadInt32();var vertices=new List<Vector3>();var normals=new List<Vector3>();var faces=new List<int>[8];for(int i=0;i<8;i++)faces[i]=new List<int>();for(int surface=0;surface<surfaces;surface++){int material=reader.ReadInt32(),count=reader.ReadInt32();if(material<0||material>=8||count<3||count%3!=0||count>100000)throw new IOException("source launch mesh bounds");for(int j=0;j<count;j++){faces[material].Add(vertices.Count);vertices.Add(new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()));normals.Add(new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()));}}if(reader.BaseStream.Position!=bytes.Length)throw new IOException("source launch mesh trailing bytes");var mesh=new Mesh();mesh.name=name;mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.subMeshCount=8;for(int i=0;i<8;i++)mesh.SetTriangles(faces[i],i);mesh.RecalculateBounds();assets.Add(mesh);return mesh;}
        }
        void EnsureStructure()
        {
            if(!Eligible){ClearStructure();return;}if(structure!=null&&built==Runtime.Layout){structure.SetActive(enabledWorld);return;}ClearStructure();built=Runtime.Layout;structure=new GameObject("Original team launch decks R12");structure.transform.SetParent(transform,false);structure.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);structure.transform.localScale=Vector3.one;
            for(int team=0;team<built.TeamCount;team++)
            {
                var station=new GameObject("TEAM "+(team+1)+" original sky deck").transform;station.SetParent(structure.transform,false);Pose(station,built.Station(team));Render(station,deck);var floor=station.gameObject.AddComponent<BoxCollider>();floor.center=new Vector3(0,-.35f,0);floor.size=new Vector3(49,.7f,18);var frame=station.gameObject.AddComponent<MeshCollider>();frame.sharedMesh=deck;
                Label(station,"TEAM "+(team+1)+"  /  SKY DECK",new Vector3(0,3.7f,-5.8f),.014f);var instructions=Label(station,"TAKE ANY AVAILABLE POD\nOR JUMP / GLIDE OFF THE PLATFORM",new Vector3(0,2.6f,-5.8f),.0058f);instructions.localRotation=Quaternion.Euler(0,180,0);
                for(int slot=0;slot<12;slot++){string key=built.PodId(team,slot);var pod=CreatePod("Source dock "+key,false);Pose(pod.Root,built.Pod(team,slot));docks.Add(key,pod);}
            }
            exitVoice=Voice(structure.transform,"pod_exit",false,-7);structure.SetActive(enabledWorld);RefreshVisuals(.1f);
        }
        void Render(Transform parent,Mesh mesh){parent.gameObject.layer=0;parent.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;parent.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials;}
        Pod CreatePod(string name,bool moving)
        {
            var root=new GameObject(name).transform;root.SetParent(structure.transform,false);Render(root,cabin);if(moving){var body=root.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;}
            var pod=new Pod{Root=root};for(int i=0;i<hardware.cabin.Length;i++){var part=hardware.cabin[i];if(!part.solid)continue;var child=new GameObject("Original solid cabin part "+i).transform;child.SetParent(root,false);child.localPosition=ArrayV(part.at);child.localRotation=Quaternion.LookRotation(new Vector3(part.basis[2],part.basis[5],part.basis[8]),new Vector3(part.basis[1],part.basis[4],part.basis[7]));if(part.size!=null&&part.size.Length==3){var collider=child.gameObject.AddComponent<BoxCollider>();collider.size=ArrayV(part.size);}else{var collider=child.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=podCollision[i];collider.convex=true;}}
            pod.Colliders=root.GetComponentsInChildren<Collider>();pod.Controls=Controls(root,moving);pod.ControlLabel=pod.Controls.GetComponentInChildren<TextMesh>();if(moving){pod.Launch=Voice(root,"pod_launch",false,-7);pod.Engine=Voice(root,"pod_engine",true,-11);pod.Land=Voice(root,"pod_land",false,-7);}return pod;
        }
        Transform Controls(Transform root,bool flying)
        {var controls=new GameObject("PodControlsR18").transform;controls.SetParent(root,false);Box(controls,new Vector3(0,.78f,.92f),new Vector3(1.5f,.56f,.075f),4);Box(controls,new Vector3(0,.56f,.8f),new Vector3(.4f,.16f,.12f),flying?1:5);for(int sign=-1;sign<=1;sign+=2)Box(controls,new Vector3(sign*.78f,.78f,.92f),new Vector3(.055f,.50f,.08f),2);Label(controls,(flying?"EJECT":"LAUNCH")+"\n"+(xrMode?"TRIGGER / GRIP":"E"),new Vector3(0,.83f,.875f),.0025f);return controls;}
        void Box(Transform parent,Vector3 at,Vector3 size,int color)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Original launch console art";go.layer=0;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);go.GetComponent<MeshRenderer>().sharedMaterial=materials[color];}
        Transform Label(Transform parent,string text,Vector3 at,float pixelSize)
        {var go=new GameObject(text.Replace('\n',' '));go.layer=0;go.transform.SetParent(parent,false);go.transform.localPosition=at;var label=go.AddComponent<TextMesh>();label.font=Resources.Load<Font>("R23GodotUI");if(label.font==null)label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=48;label.characterSize=48*pixelSize;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color(216f/255,228f/255,222f/255);label.text=text;go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;return go.transform;}
        AudioSource Voice(Transform parent,string cue,bool loop,float db)
        {var go=new GameObject("Original R18 "+cue);go.transform.SetParent(parent,false);var voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;voice.clip=clips[cue];voice.loop=loop;voice.spatialBlend=1;voice.minDistance=3;voice.maxDistance=50;voice.rolloffMode=AudioRolloffMode.Logarithmic;voice.volume=Mathf.Pow(10,db/20)*volume;return voice;}
        void ApplyVolume(Pod pod){if(pod.Launch!=null)pod.Launch.volume=Mathf.Pow(10,-7f/20)*volume;if(pod.Land!=null)pod.Land.volume=Mathf.Pow(10,-7f/20)*volume;if(pod.Engine!=null)pod.Engine.volume=Mathf.Pow(10,-11f/20)*volume;}
        void PlayExit(Vector3 at){if(exitVoice!=null&&enabledWorld){exitVoice.transform.position=at;exitVoice.PlayOneShot(clips["pod_exit"]);}}
        void RefreshVisuals(float dt)
        {
            if(!Ready||structure==null||!Eligible)return;refreshAge+=dt;if(refreshAge>=.1f||dt==0){refreshAge=0;foreach(var row in docks)row.Value.Root.gameObject.SetActive(!Runtime.Used(row.Key));}
            var keep=new HashSet<string>(StringComparer.Ordinal);foreach(var ride in Runtime.ReadRides())
            {
                if(ride.State==R24LaunchRideState.Released)continue;keep.Add(ride.PlayerId);Pod pod;if(!flights.TryGetValue(ride.PlayerId,out pod)){pod=CreatePod("Source transit pod "+ride.PlayerId,true);flights.Add(ride.PlayerId,pod);if(enabledWorld&&ride.State==R24LaunchRideState.Flying){pod.Launch.Play();pod.Engine.Play();}}
                var frame=ride.Frame;frame.Origin=R24LaunchMath.Add(ride.Position(clock),new Float3(0,-.12f,0));Pose(pod.Root,frame);
                if(ride.State==R24LaunchRideState.Landed&&!pod.Landed){pod.Landed=true;pod.Launch.Stop();pod.Engine.Stop();pod.Controls.gameObject.SetActive(false);if(enabledWorld&&clock<ride.Started+ride.Plan.Duration+1)pod.Land.Play();}
            }
            var remove=new List<string>();foreach(var row in flights)if(!keep.Contains(row.Key)){if(row.Value.Engine!=null)row.Value.Engine.Stop();Destroy(row.Value.Root.gameObject);remove.Add(row.Key);}foreach(var key in remove)flights.Remove(key);
        }
        void ClearStructure(){StopAudio();pendingExit.Clear();if(structure!=null){structure.SetActive(false);Destroy(structure);}structure=null;built=null;docks.Clear();flights.Clear();exitVoice=null;refreshAge=0;}
        void StopAudio(){foreach(var pod in flights.Values){if(pod.Engine!=null)pod.Engine.Stop();if(pod.Launch!=null)pod.Launch.Stop();if(pod.Land!=null)pod.Land.Stop();}if(exitVoice!=null)exitVoice.Stop();}
        public void Dispose(){if(disposed)return;disposed=true;StopAllCoroutines();ClearStructure();foreach(var asset in assets)if(asset!=null)Destroy(asset);assets.Clear();clips.Clear();loaded.Clear();if(Runtime!=null)Runtime.Clear();Ready=Loading=false;}
        void OnDisable(){StopAudio();}void OnDestroy(){Dispose();}
        static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}static Vector3 ArrayV(float[] p){return new Vector3(p[0],p[1],p[2]);}
        static void Pose(Transform node,R24LaunchFrame pose){node.SetPositionAndRotation(V(pose.Origin),Quaternion.LookRotation(V(pose.Forward),Vector3.up));}
        sealed class PhysicsWorld:IR24LaunchWorld
        {
            readonly R24TeamLaunch host;internal PhysicsWorld(R24TeamLaunch owner){host=owner;}public bool HasSafeSpawn{get{return host.safeSpawn!=null;}}public bool SafeSpawn(Float3 point){return host.safeSpawn==null||host.safeSpawn(V(point));}
            public bool RaycastDown(Float3 from,float endY,out R24LaunchFloor floor){floor=new R24LaunchFloor();if(host.mapRoot==null||from.Y<=endY)return false;RaycastHit nearest=new RaycastHit();float distance=float.PositiveInfinity;foreach(var hit in Physics.RaycastAll(V(from),Vector3.down,from.Y-endY,1<<0,QueryTriggerInteraction.Ignore))if(hit.collider!=null&&hit.distance<distance){nearest=hit;distance=hit.distance;}if(float.IsPositiveInfinity(distance))return false;floor=new R24LaunchFloor{Point=F(nearest.point),NormalY=nearest.normal.y};return true;}
        }
    }
}
#endif
