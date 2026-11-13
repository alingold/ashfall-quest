using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using Ashfall.Integration.Playable.World.Destructibles;
using Ashfall.Integration.Playable.World.SourceArt;

namespace Ashfall.Integration.Playable.World
{
    /// <summary>Actual R23 local map payloads; frame-yielding load on desktop and Android.</summary>
    public sealed class R23PlayableMap : MonoBehaviour
    {
        public Vector3 Spawn { get { return transform.TransformPoint(localSpawn); } }
        public Vector3 WarmupSpawn { get { return transform.TransformPoint(localWarmupSpawn); } }
        public Vector3 GameSpawn { get { return transform.TransformPoint(localGameSpawn); } }
        public Vector3 RecordedSpawn { get { return transform.TransformPoint(localRecordedSpawn); } }
        public static R23MapChoice[] AvailableMaps {get{return R23MapCatalog.AvailableMaps;}}
        public string MapId {get;private set;}
        public string Title {get;private set;}
        public float CameraFarDistance {get;private set;}
        public int DestructibleCount {get{return destructibles==null?0:destructibles.Count;}}
        public bool SourceCutApplied {get{return IsLoaded&&sourceCuts!=null;}}
        public int SourceCutOwnerCount {get{return sourceCuts==null?0:sourceCuts.NewOwners;}}
        public int SourceCutMeshCount {get;private set;}public int SourceCutColliderCount {get;private set;}public int SourceCutOrientedMeshCount {get;private set;}public int SourceCutTriangleCount {get;private set;}
        public int BrokenDestructibleCount {get{return destructibles==null?0:destructibles.BrokenCount;}}
        public R23ArchivePanelDefinition[] DestructibleDefinitions {get{return destructibles==null?new R23ArchivePanelDefinition[0]:destructibles.Definitions;}}
        public bool TryDamageCollider(Collider collider,float amount)
        {
            R23ArchivePanelDefinition row;if(!IsLoaded||!presentationEnabled||destructibles==null||!destructibles.TryReadDefinition(collider,out row)||!destructibles.TryDamageCollider(collider,amount))return false;
            PlayBreakSound(row);return true;
        }
        public void ProcessPlayerTouch(Vector3 rootWorld,Vector3 headWorld){if(IsLoaded&&presentationEnabled&&destructibles!=null)destructibles.Touch(transform.InverseTransformPoint(rootWorld),transform.InverseTransformPoint(headWorld),breakAccepted);}
        void PlayBreakSound(R23ArchivePanelDefinition row){if(mapAudio!=null)mapAudio.Play(row,transform.TransformPoint(Vec(row.center)));}
        public void SetSfxVolume(float value){sfxVolume=Single.IsNaN(value)?0:Mathf.Clamp01(value);if(mapAudio!=null)mapAudio.SetSfxVolume(sfxVolume);}
        public bool TryReadDestructible(Collider collider,out string id,out bool broken){if(destructibles!=null)return destructibles.TryRead(collider,out id,out broken);id=null;broken=false;return false;}
        public void ResetDestructibles(){if(destructibles!=null)destructibles.Reset();}
        public Bounds Bounds
        {
            get
            {
                var b = new Bounds(transform.TransformPoint(localBounds.center), Vector3.zero);
                var min = localBounds.min; var max = localBounds.max;
                for (int i = 0; i < 8; i++) b.Encapsulate(transform.TransformPoint(new Vector3(
                    (i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z)));
                return b;
            }
        }
        public bool IsLoaded { get; private set; }
        public bool IsLoading { get; private set; }
        public string LoadError { get; private set; }
        private Vector3 localSpawn = new Vector3(-7.37f, 35.336512f, 149.36f);
        bool presentationEnabled=true;
        public void SetWorldVisible(bool visible){presentationEnabled=visible;if(geometryRoot!=null){geometryRoot.SetActive(visible);Physics.SyncTransforms();}if(environment!=null)environment.SetActiveEnvironment(visible&&IsLoaded);if(archiveEnvironment!=null)archiveEnvironment.SetActive(visible&&IsLoaded);if(cyberEnvironment!=null)cyberEnvironment.SetActive(visible&&IsLoaded);}
        private Vector3 localWarmupSpawn,localGameSpawn,localRecordedSpawn;
        private Bounds localBounds = new Bounds(new Vector3(-1.457184f, 83.87786f, -10.89897f), new Vector3(499.63702f, 168.51604f, 488.35675f));
        private GameObject geometryRoot;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private Coroutine pending;
        private UnityWebRequest request;
        private string folder;
        private Manifest manifest;
        private Material[] materials;
        private BinaryReader geometryReader;
        private R23ValleyEnvironment environment;
        private R23ArchiveDestructibles destructibles;
        private R24TowerFixtures towerFixtures;
        private R24SourceCuts sourceCuts;private string sourceCutTowerHash,sourceCutArchitectureHash;
        private R24MapImpactAudio mapAudio;private float sfxVolume=1;
        private Action<R23ArchivePanelDefinition> breakAccepted;
        private Material clearGlass;
        private R24ArchitectureMaterials architecture;
        private R24SourceArtMaterials sourceArt;
        private R24ArchiveEnvironment archiveEnvironment;private Camera viewCamera;
        private R24CyberEnvironment cyberEnvironment;
        public void BindViewCamera(Camera camera){viewCamera=camera;if(archiveEnvironment!=null)archiveEnvironment.BindCamera(camera);if(cyberEnvironment!=null)cyberEnvironment.BindCamera(camera);}
        public void BindCaptureCamera(Camera camera){if(archiveEnvironment!=null)archiveEnvironment.BindCamera(camera);if(cyberEnvironment!=null)cyberEnvironment.BindCamera(camera);}
        void Update(){if(cyberEnvironment!=null)cyberEnvironment.Tick();}
        sealed class ArtDraw {public Mesh Mesh;public Material Material;}
        public const int BallisticOnlyLayer=19;
        public const int PlayerOnlyLayer=20;
        public const int FootProbeOnlyLayer=22;

        [Serializable] private sealed class Manifest
        {
            public int version = 0;
            public string title = "";
            public string mapId = "";
            public float[] spawn = null;
            public float[] warmupSpawn = null,gameSpawn = null,recordedSpawn = null;
            public float[] boundsMin = null;
            public float[] boundsMax = null;
            public MaterialRow[] materials = null;
            public BoxRow[] boxes = null;
            public int chunks = 0;
            public int[] chunkSourceLayers=null;
            public bool[] chunkBackfaceCollision=null;
            public bool[] chunkCastShadows=null;
            public float[] chunkVisibilityRanges=null;
            public string environmentFile="";
            public string towerFixturesFile="";
            public string sourceCutsFile="",sourceCutsSha256="",sourceCutsBaseMapSha256="";
            public string architectureFile="";
            public string cyberEnvironmentFile="";
            public float cameraFarDistance=0;
            public R23ArchivePanelDefinition[] destructibles=null;
            public int[] chunkPanelIds=null;
            public bool[] chunkClearGlass=null;
        }
        // Procedural source maps use real BoxShape3D collision independently of rounded render art.
        [Serializable] private sealed class BoxRow
        {public string name="";public float[] center=null,size=null,rotation=null;public int sourceLayer=1;}
        [Serializable] private sealed class MaterialRow
        {
            public string name = "";
            public string color = "";
            public string normal = "";
            public float metallic = 0;
            public float roughness = 0;
            public bool alpha = false;
            public string profile="standard",roughnessMap="",metallicMap="",occlusionMap="",emissionMap="";
            public float[] tint=null,emissionColor=null,uvScale=null,uvOffset=null,bounceColor=null;
            public float floorFinish=0;
            public float normalScale=1,emissionEnergy=0,uvTriplanarSharpness=1,alphaCutoff=.5f,snowHeight=55;
            public int roughnessChannel=0,metallicChannel=0,occlusionChannel=0,transparency=0,cull=0,emissionOperator=0;
            public bool emissionEnabled=false,uvTriplanar=false,uvWorldTriplanar=false,vertexColor=false,vertexColorSrgb=false,unshaded=false;
        }

        /// <summary>Preserves the default map; all platforms wait for IsLoaded before movement.</summary>
        public void Load(){Load(R23MapCatalog.PaintballId);}
        public void Load(string mapId)
        {
            DisposeMap();
            IsLoading = true; LoadError = null;
            var choice=R23MapCatalog.Find(mapId);
            if(choice==null){Fail(new ArgumentException("Unsupported map: "+mapId));return;}
            MapId=choice.Id;Title=choice.Label;
            if(breakAccepted==null)breakAccepted=PlayBreakSound;
            mapAudio=gameObject.AddComponent<R24MapImpactAudio>();mapAudio.SetSfxVolume(sfxVolume);
            folder = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/PlayableR23/"+choice.PayloadFolder+"/";
            pending=StartCoroutine(LoadPackaged());
        }
        private IEnumerator Fetch(string name)
        {
            string url=folder+name;if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;
            request = UnityWebRequest.Get(url);request.timeout=120;
            yield return request.SendWebRequest();
        }
        private byte[] TakeResponse()
        {
            try
            {
                if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error + " at " + request.url);
                return request.downloadHandler.data;
            }
            finally { request.Dispose(); request = null; }
        }
        private IEnumerator LoadPackaged()
        {
            // Give the root's opaque LOADING MAP presentation a rendered frame before I/O.
            yield return null;
            yield return Fetch("map.json");
            try { Begin(System.Text.Encoding.UTF8.GetString(TakeResponse())); }
            catch (Exception e) { Fail(e); yield break; }
            if(!String.IsNullOrEmpty(manifest.towerFixturesFile))
            {
                yield return Fetch(manifest.towerFixturesFile);
                try
                {
                    if(manifest.version!=1)throw new InvalidDataException("Tower source partition requires archive v1 geometry");
                    var towerBytes=TakeResponse();sourceCutTowerHash=R24SourceCuts.Hash(towerBytes);var data=JsonUtility.FromJson<R24TowerManifest>(System.Text.Encoding.UTF8.GetString(towerBytes));var keys=new string[manifest.materials.Length];for(int i=0;i<keys.Length;i++)keys[i]=manifest.materials[i].name;
                    towerFixtures=new R24TowerFixtures(data,manifest.chunks,manifest.destructibles,manifest.chunkPanelIds,keys);
                    var owners=manifest.chunkPanelIds;if(owners==null){owners=new int[manifest.chunks];for(int i=0;i<owners.Length;i++)owners[i]=-1;}
                    destructibles=new R23ArchiveDestructibles(towerFixtures.Definitions(manifest.destructibles),owners,manifest.chunks);
                }
                catch(Exception e){Fail(e);yield break;}
            }
            if(!String.IsNullOrEmpty(manifest.architectureFile))
            {
                if(manifest.version!=1||manifest.architectureFile!="architecture.json"){Fail(new InvalidDataException("Unsupported architecture binding"));yield break;}
                yield return Fetch(manifest.architectureFile);
                try{var architectureBytes=TakeResponse();sourceCutArchitectureHash=R24SourceCuts.Hash(architectureBytes);architecture=new R24ArchitectureMaterials(System.Text.Encoding.UTF8.GetString(architectureBytes),MapId,manifest.chunks);}
                catch(Exception e){Fail(e);yield break;}
                foreach(var name in architecture.Textures)
                {
                    yield return Fetch("architecture/"+name);
                    try{DecodeTexture("architecture/"+name,TakeResponse(),false);}
                    catch(Exception e){Fail(e);yield break;}
                    yield return null;
                }
            }
            foreach (var row in manifest.materials)
            {
                var names = new[] { row.color, row.normal,row.roughnessMap,row.metallicMap,row.occlusionMap,row.emissionMap };
                for (int i = 0; i < names.Length; i++)
                {
                    if (String.IsNullOrEmpty(names[i]) || textures.ContainsKey(names[i])) continue;
                    yield return Fetch(names[i]);
                    try { DecodeTexture(names[i], TakeResponse(), i>=1&&i<=4); }
                    catch (Exception e) { Fail(e); yield break; }
                    yield return null;
                }
            }
            sourceArt=new R24SourceArtMaterials();yield return sourceArt.Load(MapId,manifest.chunks);
            if(sourceArt.Failure!=null){Fail(new InvalidDataException(sourceArt.Failure));yield break;}
            if(!String.IsNullOrEmpty(manifest.sourceCutsFile))
            {
                if(manifest.version!=1||MapId!="archive:classic-bigbox-vr"||manifest.sourceCutsFile!="classic-source-cuts-v003.bytes"||towerFixtures==null||destructibles==null){Fail(new InvalidDataException("Source cut manifest binding"));yield break;}
                yield return Fetch(manifest.sourceCutsFile);
                try
                {
                    var cutBytes=TakeResponse();if(R24SourceCuts.Hash(cutBytes)!=manifest.sourceCutsSha256)throw new InvalidDataException("Source cut payload checksum");
                    var keys=new string[manifest.materials.Length];for(int i=0;i<keys.Length;i++)keys[i]=manifest.materials[i].name;
                    sourceCuts=new R24SourceCuts(cutBytes,MapId,manifest.sourceCutsBaseMapSha256,sourceCutTowerHash,sourceCutArchitectureHash,sourceArt.SourceBindingSha256,keys,destructibles.Count,manifest.chunks);
                    var old=destructibles.Definitions;var owners=manifest.chunkPanelIds;if(owners==null){owners=new int[manifest.chunks];for(int i=0;i<owners.Length;i++)owners[i]=-1;}
                    destructibles=new R23ArchiveDestructibles(sourceCuts.Definitions(old),owners,manifest.chunks);
                }
                catch(Exception e){Fail(e);yield break;}
            }
            if(!String.IsNullOrEmpty(manifest.environmentFile))
            {
                if(manifest.environmentFile!="environment.json"){Fail(new InvalidDataException("Unsupported environment file"));yield break;}
                environment=gameObject.AddComponent<R23ValleyEnvironment>();environment.Initialize(folder);
                while(environment.Loading)yield return null;
                if(!environment.Ready){Fail(new InvalidDataException("Source environment: "+environment.Failure));yield break;}
            }
            if(!String.IsNullOrEmpty(manifest.cyberEnvironmentFile))
            {
                if(manifest.version!=2||MapId!=R23MapCatalog.CyberBirdId||manifest.cyberEnvironmentFile!="environment.json"||!String.IsNullOrEmpty(manifest.environmentFile)){Fail(new InvalidDataException("Unsupported Cyber Bird environment binding"));yield break;}
                yield return Fetch(manifest.cyberEnvironmentFile);
                try{cyberEnvironment=new R24CyberEnvironment(transform,System.Text.Encoding.UTF8.GetString(TakeResponse()));cyberEnvironment.BindCamera(viewCamera);}
                catch(Exception e){Fail(e);yield break;}
            }
            byte[] bytes=null;
            if(folder.Contains("://"))
            {
                yield return Fetch("geometry.bytes");
                try{bytes=TakeResponse();}catch(Exception e){Fail(e);yield break;}
            }
            try
            {
                FinishMaterials();
                geometryReader=new BinaryReader(bytes==null?(Stream)File.OpenRead(folder+"geometry.bytes"):new MemoryStream(bytes,false));
                if(architecture!=null)architecture.VerifyGeometry(geometryReader.BaseStream);
                if(sourceCuts!=null)sourceCuts.VerifyGeometry(geometryReader.BaseStream);
                if(sourceArt!=null&&sourceArt.Plan!=null)sourceArt.Plan.VerifyGeometry(geometryReader.BaseStream);
                if(geometryReader.ReadInt32()!=0x52323357||geometryReader.ReadInt32()!=manifest.chunks)throw new InvalidDataException("Invalid R23 geometry header");
            }
            catch (Exception e) { Fail(e); }
            if(!IsLoading)yield break;
            yield return null;
            for(int chunk=0;chunk<manifest.chunks;chunk++)
            {
                try{BuildChunk(geometryReader,chunk);}catch(Exception e){Fail(e);}
                if(!IsLoading)yield break;
                yield return null;
            }
            try
            {
                if(geometryReader.BaseStream.Position!=geometryReader.BaseStream.Length)throw new InvalidDataException("Trailing R23 geometry bytes");
                geometryReader.Dispose();geometryReader=null;Complete();
            }catch(Exception e){Fail(e);}
            pending = null;
        }
        private void Begin(string json)
        {
            manifest = JsonUtility.FromJson<Manifest>(json);
            if (manifest == null || (manifest.version != 1&&manifest.version!=2) || manifest.mapId!=MapId || manifest.materials == null || manifest.chunks < 1 || manifest.chunks > 4096)
                throw new InvalidDataException("Invalid R23 map manifest");
            if(manifest.version==2&&(manifest.chunkSourceLayers==null||manifest.chunkSourceLayers.Length!=manifest.chunks||manifest.chunkBackfaceCollision==null||manifest.chunkBackfaceCollision.Length!=manifest.chunks))throw new InvalidDataException("Missing source collision roles");
            if(manifest.chunkCastShadows!=null&&manifest.chunkCastShadows.Length!=manifest.chunks||manifest.chunkVisibilityRanges!=null&&manifest.chunkVisibilityRanges.Length!=manifest.chunks)throw new InvalidDataException("Source visibility/shadow membership changed");
            localSpawn = Vec(manifest.spawn);
            localWarmupSpawn=manifest.warmupSpawn==null?localSpawn:Vec(manifest.warmupSpawn);
            localGameSpawn=manifest.gameSpawn==null?localSpawn:Vec(manifest.gameSpawn);
            localRecordedSpawn=manifest.recordedSpawn==null?localSpawn:Vec(manifest.recordedSpawn);
            Title=manifest.title;
            if(Single.IsNaN(manifest.cameraFarDistance)||Single.IsInfinity(manifest.cameraFarDistance)||manifest.cameraFarDistance<0||manifest.cameraFarDistance>20000)throw new InvalidDataException("Invalid map camera distance");
            CameraFarDistance=manifest.cameraFarDistance>0?manifest.cameraFarDistance:2500;
            localBounds.SetMinMax(Vec(manifest.boundsMin), Vec(manifest.boundsMax));
            if(manifest.version==1&&MapId.StartsWith("archive:",StringComparison.Ordinal)){archiveEnvironment=new R24ArchiveEnvironment(transform);archiveEnvironment.BindCamera(viewCamera);}
            geometryRoot = new GameObject(manifest.title);
            geometryRoot.transform.SetParent(transform, false);
            geometryRoot.SetActive(false);
            if(manifest.destructibles!=null)
            {
                if(manifest.chunkClearGlass==null||manifest.chunkClearGlass.Length!=manifest.chunks)throw new InvalidDataException("Missing source glass membership");
                destructibles=new R23ArchiveDestructibles(manifest.destructibles,manifest.chunkPanelIds,manifest.chunks);
                var glassShader=Resources.Load<Shader>("R23ArchiveGlass");if(glassShader==null)throw new InvalidDataException("Original archive glass shader missing");
                clearGlass=new Material(glassShader);clearGlass.name="Original clear_glass_r10";owned.Add(clearGlass);
            }
        }
        private static Vector3 Vec(float[] a)
        {
            if (a == null || a.Length != 3) throw new InvalidDataException("Invalid map vector");
            foreach(float value in a)if(Single.IsNaN(value)||Single.IsInfinity(value))throw new InvalidDataException("Nonfinite map vector");
            return new Vector3(a[0], a[1], a[2]);
        }
        private void DecodeTexture(string name, byte[] bytes, bool linear)
        {
            if(name.Contains("..")||name.Contains(":")||name.StartsWith("/")||name.StartsWith("\\"))throw new InvalidDataException("Texture path must stay in map payload");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
            owned.Add(tex);
            tex.name = name; tex.wrapMode = TextureWrapMode.Repeat; tex.filterMode = FilterMode.Trilinear; tex.anisoLevel = 2;
            if (!ImageConversion.LoadImage(tex, bytes, true)) throw new InvalidDataException("Cannot decode " + name);
            textures.Add(name, tex);
        }
        private void FinishMaterials()
        {
            var shader = Resources.Load<Shader>("R23PlayableWorld");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("R23PlayableWorld shader missing or unsupported; use installed URP");
            materials = new Material[manifest.materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var row = manifest.materials[i];
                bool architectureMaterial=architecture!=null&&architecture.UsesMaterial(row.name);
                var selected=architectureMaterial?Resources.Load<Shader>("R24Architecture"):manifest.version==1?shader:Resources.Load<Shader>(row.profile=="alpine"?"R23AlpineTerrain":row.profile=="cyberWeathered"?"R23CyberWeathered":"R23ValleyPbr");
                if(selected==null||!selected.isSupported)throw new InvalidOperationException("Source map shader unavailable: "+row.profile);
                var mat = new Material(selected); owned.Add(mat); materials[i] = mat;
                mat.name = row.name;
                if(architectureMaterial){architecture.Configure(mat,row.name,textures);continue;}
                if(manifest.version==2){ConfigureSourceMaterial(mat,row);continue;}
                mat.SetFloat("_Metallic", row.metallic); mat.SetFloat("_Roughness", row.roughness);
                mat.SetFloat("_Cutoff", row.alpha ? .4f : 0f);
                if (!String.IsNullOrEmpty(row.color)) mat.SetTexture("_BaseMap", textures[row.color]);
                if (!String.IsNullOrEmpty(row.normal)) { mat.SetTexture("_NormalMap", textures[row.normal]); mat.SetFloat("_NormalStrength", .6f); }
            }
        }
        static Color Color4(float[] a,Color fallback)
        {if(a==null)return fallback;if(a.Length!=4)throw new InvalidDataException("Invalid source material color");foreach(float x in a)if(Single.IsNaN(x)||Single.IsInfinity(x))throw new InvalidDataException("Nonfinite source color");return new Color(a[0],a[1],a[2],a[3]);}
        void ConfigureSourceMaterial(Material mat,MaterialRow row)
        {
            if(row.profile=="alpine"){mat.SetFloat("_SnowHeight",row.snowHeight);return;}
            if(row.uvTriplanar&&!row.uvWorldTriplanar&&!String.IsNullOrEmpty(row.normal))throw new InvalidDataException("Local triplanar normal basis is not supplied by this map layout");
            mat.SetColor("_Tint",Color4(row.tint,Color.white));mat.SetFloat("_Metallic",row.metallic);mat.SetFloat("_Roughness",row.roughness);
            if(row.profile=="cyberWeathered"){mat.SetColor("_BounceColor",Color4(row.bounceColor,Color.clear));mat.SetFloat("_FloorFinish",row.floorFinish);}
            mat.SetFloat("_NormalStrength",String.IsNullOrEmpty(row.normal)?0:row.normalScale);
            mat.SetVector("_UvScale",row.uvScale==null?Vector3.one:Vec(row.uvScale));mat.SetVector("_UvOffset",row.uvOffset==null?Vector3.zero:Vec(row.uvOffset));
            mat.SetFloat("_Triplanar",row.uvTriplanar?1:0);mat.SetFloat("_WorldTriplanar",row.uvWorldTriplanar?1:0);mat.SetFloat("_TriplanarSharpness",row.uvTriplanarSharpness);
            mat.SetFloat("_VertexColor",row.vertexColor?1:0);mat.SetFloat("_VertexColorSrgb",row.vertexColorSrgb?1:0);mat.SetFloat("_Unshaded",row.unshaded?1:0);
            mat.SetFloat("_Cutoff",row.transparency==2||row.transparency==3?row.alphaCutoff:0);
            mat.SetFloat("_Cull",row.cull==2?0:row.cull==1?1:2);
            bool blend=row.transparency==1||row.transparency==4;
            mat.SetFloat("_SrcBlend",blend?(float)BlendMode.SrcAlpha:(float)BlendMode.One);mat.SetFloat("_DstBlend",blend?(float)BlendMode.OneMinusSrcAlpha:(float)BlendMode.Zero);
            mat.SetFloat("_ZWrite",blend?0:1);if(blend)mat.renderQueue=(int)RenderQueue.Transparent;
            mat.SetInt("_RoughnessChannel",row.roughnessChannel);mat.SetInt("_MetallicChannel",row.metallicChannel);mat.SetInt("_OcclusionChannel",row.occlusionChannel);
            mat.SetColor("_EmissionColor",Color4(row.emissionColor,Color.black));mat.SetFloat("_EmissionEnergy",row.emissionEnabled?row.emissionEnergy:0);
            mat.SetFloat("_EmissionHasMap",String.IsNullOrEmpty(row.emissionMap)?0:1);mat.SetInt("_EmissionOperator",row.emissionOperator);
            string[] names={row.color,row.normal,row.roughnessMap,row.metallicMap,row.occlusionMap,row.emissionMap};
            string[] properties={"_BaseMap","_NormalMap","_RoughnessMap","_MetallicMap","_OcclusionMap","_EmissionMap"};
            for(int j=0;j<names.Length;j++)if(!String.IsNullOrEmpty(names[j]))mat.SetTexture(properties[j],textures[names[j]]);
        }
        private static Vector3 ReadVector(BinaryReader r) { return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }
        private void BuildChunk(BinaryReader r,int chunk)
        {
                    int material = r.ReadInt32(); int collisionFlags=r.ReadInt32();bool collision = collisionFlags != 0; bool visible = r.ReadInt32() != 0;
                    if(manifest.version==2&&collisionFlags!=0&&collisionFlags!=1&&collisionFlags!=3)throw new InvalidDataException("Unknown source collision flags");
                    int nv = r.ReadInt32(), ni = r.ReadInt32();
                    long vertexStart=r.BaseStream.Position;
                    if (material < 0 || material >= materials.Length || nv < 1 || nv > 200000 || ni < 3 || ni > 3000000 || ni % 3 != 0) throw new InvalidDataException("Invalid R23 mesh range");
                    // Retired source authoring symbols retain encoded chunk identity so
                    // panel membership stays stable, but allocate no native meshes.
                    if(!collision&&!visible)
                    {
                        long bytes=(long)nv*(manifest.version==2?72:48);
                        if(r.BaseStream.Length-r.BaseStream.Position<bytes+(long)ni*4)throw new EndOfStreamException("Truncated retired R23 chunk");
                        r.BaseStream.Seek(bytes,SeekOrigin.Current);
                        for(int j=0;j<ni;j++){int index=r.ReadInt32();if(index<0||index>=nv)throw new InvalidDataException("Invalid retired R23 index");}
                        return;
                    }
                    var positions = new Vector3[nv]; var normals = new Vector3[nv]; var uv = new Vector2[nv]; var colors = new Color[nv];
                    var sourcePositions=new List<Vector3>();var sourceNormals=new List<Vector3>();
                    for (int j = 0; j < nv; j++)
                    {
                        positions[j] = ReadVector(r); normals[j] = ReadVector(r); uv[j] = new Vector2(r.ReadSingle(), r.ReadSingle());
                        colors[j] = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        if(manifest.version==2){sourcePositions.Add(ReadVector(r));sourceNormals.Add(ReadVector(r));}
                    }
                    long indexStart=r.BaseStream.Position;var indices = new int[ni];
                    for (int j = 0; j < ni; j++) { indices[j] = r.ReadInt32(); if (indices[j] < 0 || indices[j] >= nv) throw new InvalidDataException("Invalid R23 index"); }
                    var patch=towerFixtures==null?null:towerFixtures.Read(chunk,r.BaseStream,vertexStart,indexStart,r.BaseStream.Position,material,collisionFlags,visible,nv,ni);
                    if(sourceArt!=null&&sourceArt.Plan!=null)sourceArt.Plan.VerifyChunk(chunk,r.BaseStream,vertexStart,indexStart,r.BaseStream.Position,nv,ni);
                    bool cuts=sourceCuts!=null&&sourceCuts.VerifyChunk(chunk,r.BaseStream,vertexStart,indexStart,r.BaseStream.Position,material,collisionFlags,visible,nv,ni);
                    int[] cutStatic=cuts?sourceCuts.KeepStatic(chunk,indices,patch==null?null:patch.staticTriangleRanges):null;
                    if(patch==null)BuildOwnerPart(chunk,material,collisionFlags,visible,positions,normals,uv,colors,indices,cutStatic??indices,sourcePositions,sourceNormals,-1,false);
                    else
                    {
                        var partitions=R24TowerFixtures.Partition(patch,indices,towerFixtures.OwnerCount);
                        BuildOwnerPart(chunk,material,collisionFlags,visible,positions,normals,uv,colors,indices,cutStatic??partitions[0],sourcePositions,sourceNormals,-1,false);
                        for(int part=0;part<patch.parts.Length;part++)
                        {
                            BuildOwnerPart(chunk,material,collisionFlags,visible,positions,normals,uv,colors,indices,partitions[part+1],null,null,towerFixtures.BasePanels+patch.parts[part].owner,true);
                        }
                    }
                    if(cuts)
                    {
                        var original=new float[nv*12];for(int v=0;v<nv;v++){int at=v*12;original[at]=positions[v].x;original[at+1]=positions[v].y;original[at+2]=positions[v].z;original[at+3]=normals[v].x;original[at+4]=normals[v].y;original[at+5]=normals[v].z;original[at+6]=uv[v].x;original[at+7]=uv[v].y;original[at+8]=colors[v].r;original[at+9]=colors[v].g;original[at+10]=colors[v].b;original[at+11]=colors[v].a;}
                        foreach(var part in sourceCuts.Assemble(chunk,original,indices))BuildSourceCutPart(chunk,material,collisionFlags,visible,indices,part);
                    }
        }
        void BuildOwnerPart(int chunk,int material,int collisionFlags,bool visible,Vector3[] positions,Vector3[] normals,Vector2[] uv,Color[] colors,int[] full,int[] owner,List<Vector3> sourcePositions,List<Vector3> sourceNormals,int fixtureOwner,bool compact)
        {
            int[] render=owner;var draws=new List<ArtDraw>();R24SourceArtPart[] details;
            if(visible&&sourceArt!=null&&sourceArt.Plan!=null&&sourceArt.Plan.Select(chunk,full,owner,out render,out details))foreach(var part in details)
            {var mesh=R24SourceArtMaterials.BuildDetail(part,positions,normals,colors);owned.Add(mesh);draws.Add(new ArtDraw{Mesh=mesh,Material=sourceArt.ForKind(part.Kind)});}
            var kinds=architecture==null?null:architecture.Kinds(chunk,full,render);
            var fragments=architecture==null?null:architecture.RenderFragments(chunk,full,render);
            if(compact)
            {
                var remap=new Dictionary<int,int>();var p=new List<Vector3>();var n=new List<Vector3>();var u=new List<Vector2>();var c=new List<Color>();var collider=new int[owner.Length];
                for(int i=0;i<owner.Length;i++){int at;if(!remap.TryGetValue(owner[i],out at)){at=p.Count;remap.Add(owner[i],at);p.Add(positions[owner[i]]);n.Add(normals[owner[i]]);u.Add(uv[owner[i]]);c.Add(colors[owner[i]]);}collider[i]=at;}
                var keep=new int[render.Length];for(int i=0;i<keep.Length;i++)keep[i]=remap[render[i]];
                BuildMeshPart(chunk,material,collisionFlags,visible,p.ToArray(),n.ToArray(),u.ToArray(),c.ToArray(),collider,sourcePositions,sourceNormals,fixtureOwner,kinds,keep,draws.ToArray(),fragments);
            }
            else BuildMeshPart(chunk,material,collisionFlags,visible,positions,normals,uv,colors,owner,sourcePositions,sourceNormals,fixtureOwner,kinds,render,draws.ToArray(),fragments);
        }
        void BuildSourceCutPart(int chunk,int material,int flags,bool visible,int[] original,R24SourceCutMesh part)
        {
            int count=part.Vertices.Length/12;var p=new Vector3[count];var n=new Vector3[count];var u=new Vector2[count];var c=new Color[count];var indices=new int[count];
            for(int i=0;i<count;i++){int at=i*12;p[i]=new Vector3(part.Vertices[at],part.Vertices[at+1],part.Vertices[at+2]);n[i]=new Vector3(part.Vertices[at+3],part.Vertices[at+4],part.Vertices[at+5]);u[i]=new Vector2(part.Vertices[at+6],part.Vertices[at+7]);c[i]=new Color(part.Vertices[at+8],part.Vertices[at+9],part.Vertices[at+10],part.Vertices[at+11]);indices[i]=i;}
            int[] render=indices;var draws=new List<ArtDraw>();R24SourceArtPart[] details;
            var rotation=SourceCutRotation(part);
            if(visible&&sourceArt!=null&&sourceArt.Plan!=null&&sourceArt.Plan.SelectBarycentric(chunk,original,part.SourceTriangles,part.Barycentric,out render,out details))foreach(var art in details)
            {var mesh=R24SourceArtMaterials.BuildDetail(art,p,n,c,Quaternion.Inverse(rotation));owned.Add(mesh);draws.Add(new ArtDraw{Mesh=mesh,Material=sourceArt.ForKind(art.Kind)});}
            var kinds=new byte[render.Length/3];for(int i=0;i<kinds.Length;i++)kinds[i]=part.Kinds[render[i*3]/3];
            BuildMeshPart(chunk,material,flags,visible,p,n,u,c,indices,null,null,part.Owner,kinds,render,draws.ToArray(),null,part);
        }
        static Quaternion SourceCutRotation(R24SourceCutMesh part){var q=R24SourceCuts.Rotation(part);return new Quaternion(q[0],q[1],q[2],q[3]);}
        void BuildMeshPart(int chunk,int material,int collisionFlags,bool visible,Vector3[] positions,Vector3[] normals,Vector2[] uv,Color[] colors,int[] indices,List<Vector3> sourcePositions,List<Vector3> sourceNormals,int fixtureOwner,byte[] architectureKinds=null,int[] renderSubset=null,ArtDraw[] artDetails=null,R24ArchitectureMaterials.Fragment[] fragments=null,R24SourceCutMesh sourceCut=null)
        {
                    if(indices.Length==0)return;int nv=positions.Length,ni=indices.Length;bool collision=collisionFlags!=0;
                    var mesh = new Mesh(); owned.Add(mesh); mesh.name = "R23 chunk " + chunk+(fixtureOwner>=0?" tower owner "+fixtureOwner:"");
                    var renderPositions=positions;var renderNormals=normals;var renderUv=uv;var renderColors=colors;var renderIndices=renderSubset??indices;
                    R24ArchitectureMaterials.ExpandRender(visible?architectureKinds:null,ref renderPositions,ref renderNormals,ref renderUv,ref renderColors,ref renderIndices,visible?fragments:null);
                    mesh.indexFormat = renderPositions.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                    mesh.vertices = renderPositions; mesh.normals = renderNormals; mesh.uv = renderUv; mesh.colors = renderColors; mesh.triangles = renderIndices; mesh.RecalculateBounds();
                    if(manifest.version==2){mesh.SetUVs(1,sourcePositions);mesh.SetUVs(2,sourceNormals);}
                    var go = new GameObject(mesh.name); go.layer = gameObject.layer; go.transform.SetParent(geometryRoot.transform, false);
                    if(sourceCut!=null){go.transform.localPosition=Vec(sourceCut.Origin);go.transform.localRotation=SourceCutRotation(sourceCut);}
                    if (visible)
                    {
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = manifest.chunkClearGlass!=null&&manifest.chunkClearGlass[chunk]?clearGlass:materials[material];
                        if(sourceCut!=null){var block=new MaterialPropertyBlock();block.SetVector("_SourceObjectOffset",Vec(sourceCut.Origin));block.SetFloat("_SourceCutFrame",sourceCut.Precision?1:0);if(sourceCut.Precision)block.SetMatrix("_SourceCutLocalToMap",Matrix4x4.TRS(go.transform.localPosition,go.transform.localRotation,Vector3.one));renderer.SetPropertyBlock(block);}
                        renderer.shadowCastingMode = manifest.chunkCastShadows!=null?(manifest.chunkCastShadows[chunk]?ShadowCastingMode.On:ShadowCastingMode.Off):manifest.version==1?ShadowCastingMode.Off:ShadowCastingMode.On; renderer.receiveShadows = manifest.version==2;
                        if(artDetails!=null)foreach(var detail in artDetails)
                        {var child=new GameObject(detail.Mesh.name);child.layer=go.layer;child.transform.SetParent(go.transform,false);child.AddComponent<MeshFilter>().sharedMesh=detail.Mesh;var view=child.AddComponent<MeshRenderer>();view.sharedMaterial=detail.Material;view.shadowCastingMode=ShadowCastingMode.Off;view.receiveShadows=false;detail.Mesh.UploadMeshData(true);}
                    }
                    if (collision)
                    {
                        // R23 concave collision is explicitly two sided. Retain both faces without changing rendering.
                        bool convex=manifest.version==2&&collisionFlags==3;
                        bool backfaces=manifest.version==1||manifest.chunkBackfaceCollision[chunk];
                        var twoSided=indices;
                        if(backfaces&&!convex){twoSided = new int[ni * 2]; Array.Copy(indices, twoSided, ni);for (int j = 0; j < ni; j += 3) { twoSided[ni+j] = indices[j]; twoSided[ni+j+1] = indices[j+2]; twoSided[ni+j+2] = indices[j+1]; }}
                        if(convex&&nv>255)throw new InvalidDataException("Source convex exceeds native vertex limit");
                        var colliderMesh = new Mesh(); owned.Add(colliderMesh); colliderMesh.name = mesh.name + " collision";
                        colliderMesh.indexFormat = mesh.indexFormat; colliderMesh.vertices = positions; colliderMesh.triangles = twoSided; colliderMesh.RecalculateBounds();
                        var collisionNode=go;
                        int role=manifest.version==1?gameObject.layer:SourceLayer(manifest.chunkSourceLayers[chunk]);
                        if(role==BallisticOnlyLayer||role==PlayerOnlyLayer||role==FootProbeOnlyLayer){collisionNode=new GameObject(mesh.name+" source collision role "+role);collisionNode.layer=role;collisionNode.transform.SetParent(go.transform,false);}
                        var collider = collisionNode.AddComponent<MeshCollider>();collider.convex=convex;if(sourceCut!=null)collider.cookingOptions=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.UseFastMidphase;collider.sharedMesh = colliderMesh;if(sourceCut!=null)SourceCutColliderCount++;
                        if(destructibles!=null){if(fixtureOwner>=0)destructibles.RegisterOwner(fixtureOwner,go,collider);else destructibles.Register(chunk,go,collider);}
                        // Runtime concave cooking can occur when this inactive chunk
                        // becomes live. Keep collision CPU data for its lifetime:
                        // UploadMeshData(true) made every player-build floor unavailable
                        // even though Editor meshes continued to collide.
                    }
                    if(fixtureOwner>=0&&!collision)destructibles.RegisterOwner(fixtureOwner,go,null);
                    mesh.UploadMeshData(true);
                    if(sourceCut!=null){SourceCutMeshCount++;SourceCutTriangleCount+=indices.Length/3;if(sourceCut.Precision)SourceCutOrientedMeshCount++;}
        }
        private void Complete()
        {
            if(towerFixtures!=null)towerFixtures.Complete();
            if(sourceCuts!=null)sourceCuts.Complete();
            if(destructibles!=null)destructibles.Validate();
            if(manifest.boxes!=null)
            {
                if(manifest.boxes.Length>2048)throw new InvalidDataException("Too many source box shapes");
                foreach(var row in manifest.boxes)
                {
                    if(row==null)throw new InvalidDataException("Missing source box shape");var size=Vec(row.size);
                    if(size.x<=0||size.y<=0||size.z<=0)throw new InvalidDataException("Invalid source box size");
                    var node=new GameObject(row.name);node.layer=manifest.version==1?gameObject.layer:SourceLayer(row.sourceLayer);node.transform.SetParent(geometryRoot.transform,false);node.transform.localPosition=Vec(row.center);
                    if(row.rotation!=null){if(row.rotation.Length!=4)throw new InvalidDataException("Source box rotation");var q=new Quaternion(row.rotation[0],row.rotation[1],row.rotation[2],row.rotation[3]);float magnitude=Quaternion.Dot(q,q);if(Single.IsNaN(magnitude)||Mathf.Abs(magnitude-1)>.001f)throw new InvalidDataException("Source box quaternion must be unit");node.transform.localRotation=q;}
                    node.AddComponent<BoxCollider>().size=size;
                }
            }
            var colliders=geometryRoot.GetComponentsInChildren<MeshCollider>(true);
            int total=geometryRoot.GetComponentsInChildren<Collider>(true).Length;
            if(total==0)throw new InvalidDataException("R23 map has no collision shapes");
            foreach(var c in colliders)if(c.sharedMesh==null||!c.sharedMesh.isReadable)throw new InvalidDataException("Runtime map collision mesh must remain CPU-readable");
            geometryRoot.SetActive(presentationEnabled);Physics.SyncTransforms();IsLoaded=true;IsLoading=false;
            if(environment!=null)environment.SetActiveEnvironment(presentationEnabled);
            if(archiveEnvironment!=null)archiveEnvironment.SetActive(presentationEnabled);
            if(cyberEnvironment!=null)cyberEnvironment.SetActive(presentationEnabled);
            Debug.Log("R23 native collision ready: "+colliders.Length+" readable meshes / "+total+" total source shapes for "+MapId);
        }
        static int SourceLayer(int bits){return (bits&1)!=0?0:(bits&8)!=0?BallisticOnlyLayer:(bits&4)!=0?PlayerOnlyLayer:(bits&16)!=0?FootProbeOnlyLayer:0;}
        private void Fail(Exception e)
        {
            ReleaseGeometry(); IsLoading = false; IsLoaded = false; LoadError = e.Message;
            Debug.LogError("R23PlayableMap: " + e, this);
        }
        public void DisposeMap()
        {
            if (pending != null) { StopCoroutine(pending); pending = null; }
            if (request != null) { request.Abort(); request.Dispose(); request = null; }
            ReleaseGeometry(); IsLoaded = false; IsLoading = false;
        }
        private void ReleaseGeometry()
        {
            if(cyberEnvironment!=null){cyberEnvironment.Dispose();cyberEnvironment=null;}
            if(archiveEnvironment!=null){archiveEnvironment.Dispose();archiveEnvironment=null;}
            if(mapAudio!=null){mapAudio.Retire();Destroy(mapAudio);mapAudio=null;}
            if(environment!=null){environment.DisposeEnvironment();Destroy(environment);environment=null;}
            if(geometryReader!=null){geometryReader.Dispose();geometryReader=null;}
            if (geometryRoot != null) { geometryRoot.SetActive(false); Destroy(geometryRoot); geometryRoot = null; }
            if(sourceArt!=null){sourceArt.Dispose();sourceArt=null;}
            foreach (var obj in owned) if (obj != null) Destroy(obj);
            owned.Clear(); textures.Clear(); materials = null; manifest = null;CameraFarDistance=2500;
            SourceCutMeshCount=SourceCutColliderCount=SourceCutOrientedMeshCount=SourceCutTriangleCount=0;destructibles=null;towerFixtures=null;clearGlass=null;architecture=null;sourceCuts=null;sourceCutTowerHash=sourceCutArchitectureHash=null;
        }
        private void OnDestroy() { DisposeMap(); }
    }
}
