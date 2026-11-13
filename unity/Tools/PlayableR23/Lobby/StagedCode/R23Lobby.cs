#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Lobby
{
    // Actual standalone lobby_map_091. This object never moves the player or camera.
    public sealed class R23Lobby:MonoBehaviour
    {
        [Serializable] sealed class ColliderRow {public string name="";public float[] at=new float[0],size=new float[0];}
        [Serializable] sealed class Manifest {public int version=0;public float[] origin=new float[0],spawn=new float[0],menu=new float[0],preview=new float[0];public ColliderRow[] colliders=new ColliderRow[0];}
        public bool Ready {get;private set;}
        public bool Loading {get;private set;}
        public bool Visible {get{return shown;}}
        public string Failure {get;private set;}
        public Transform PlayerRoot {get;private set;}
        public Transform Head {get;private set;}
        public Transform MenuAnchor {get;private set;}
        public Transform PreviewAnchor {get;private set;}
        public Vector3 Origin {get{return new Vector3(1000,0,-1000);}}
        public Vector3 Spawn {get{return Origin+new Vector3(0,.3f,-3.8f);}}
        public Quaternion SpawnRotation {get{return Quaternion.identity;}}
        public Vector2 MenuSize {get{return new Vector2(2.20f,1.375f);}}
        public Vector3 ReferenceEye {get{return Spawn+Vector3.up*1.7088f;}}
        public Quaternion ReferenceRotation {get{return Quaternion.identity;}}
        public event Action Loaded;
        GameObject content;bool shown,environmentApplied;Camera cameraRef;
        Material sky,previousSky;Light sun,previousSun;AmbientMode previousMode;Color previousAmbient;
        bool previousFog;CameraClearFlags previousClear;Color previousBackground;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        public void Initialize(Transform playerRoot,Transform headPose)
        {
            if(PlayerRoot!=null)throw new InvalidOperationException("lobby already initialized");
            if(playerRoot==null||headPose==null)throw new ArgumentException("accepted player root and head required");
            PlayerRoot=playerRoot;Head=headPose;cameraRef=headPose.GetComponent<Camera>();
            content=new GameObject("R23 StandaloneLobbyMap091");content.transform.position=Origin;content.SetActive(false);
            MenuAnchor=Anchor("Source menu board",new Vector3(.10f,1.30f,-1.55f),Quaternion.identity);
            // Source avatar local yaw PI survives Z reflection as PI. Rendering owner supplies its real preview.
            PreviewAnchor=Anchor("Source full-size avatar pedestal",new Vector3(1.35f,.31f,.65f),Quaternion.Euler(0,180,0));
        }
        public void Initialize(Transform playerRoot,Camera viewCamera){if(viewCamera==null)throw new ArgumentNullException("viewCamera");Initialize(playerRoot,viewCamera.transform);}
        Transform Anchor(string label,Vector3 at,Quaternion rotation)
        {var t=new GameObject(label).transform;t.SetParent(content.transform,false);t.localPosition=at;t.localRotation=rotation;return t;}
        public void Load(){if(PlayerRoot==null)throw new InvalidOperationException("Initialize lobby first");if(!Loading&&!Ready){Loading=true;Failure=null;StartCoroutine(LoadPayload());}}
        IEnumerator Read(string filename,Dictionary<string,byte[]> files)
        {
            string path=Application.streamingAssetsPath+"/PlayableR23/Lobby/"+filename;if(!path.Contains("://"))path=new Uri(path).AbsoluteUri;
            using(var request=UnityWebRequest.Get(path)){request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Failure=filename+": "+request.error;yield break;}files[filename]=request.downloadHandler.data;}
        }
        IEnumerator LoadPayload()
        {
            var files=new Dictionary<string,byte[]>();yield return Read("lobby.json",files);if(Failure!=null){Loading=false;yield break;}
            yield return Read("lobby.meshbin",files);if(Failure!=null){Loading=false;yield break;}
            Manifest manifest=null;BinaryReader reader=null;Material[] materials=null;int surfaces=0;
            try
            {
                manifest=JsonUtility.FromJson<Manifest>(System.Text.Encoding.UTF8.GetString(files["lobby.json"]));if(manifest==null||manifest.version!=1)throw new IOException("source lobby manifest");
                reader=new BinaryReader(new MemoryStream(files["lobby.meshbin"]));if(reader.ReadInt32()!=0x5232334c)throw new IOException("source lobby geometry");surfaces=reader.ReadInt32();if(surfaces!=5)throw new IOException("source lobby surfaces");materials=BuildMaterials();
            }
            catch(Exception error){Failure=error.ToString();}
            if(Failure!=null){if(reader!=null)reader.Dispose();Loading=false;yield break;}
            for(int surface=0;surface<surfaces;surface++)
            {
                try
                {
                    int finish=reader.ReadInt32(),vc=reader.ReadInt32(),ic=reader.ReadInt32();if(finish<0||finish>=5||vc<1||vc>1000000||ic<1||ic>3000000)throw new IOException("source lobby surface bounds");
                    var positions=new Vector3[vc];var normals=new Vector3[vc];var indices=new int[ic];
                    for(int v=0;v<vc;v++){positions[v]=ReadVector(reader);normals[v]=ReadVector(reader);}
                    for(int i=0;i<ic;i++){indices[i]=reader.ReadInt32();if(indices[i]<0||indices[i]>=vc)throw new IOException("source lobby index");}
                    var mesh=new Mesh{name="R23 lobby source surface "+finish,indexFormat=vc>65535?IndexFormat.UInt32:IndexFormat.UInt16};owned.Add(mesh);mesh.vertices=positions;mesh.normals=normals;mesh.triangles=indices;mesh.RecalculateBounds();mesh.UploadMeshData(true);
                    var go=new GameObject(mesh.name);go.transform.SetParent(content.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=materials[finish];
                }
                catch(Exception error){Failure=error.ToString();}
                if(Failure!=null){reader.Dispose();Loading=false;yield break;}yield return null;
            }
            reader.Dispose();
            try
            {
                foreach(var row in manifest.colliders){var go=new GameObject(row.name);go.transform.SetParent(content.transform,false);go.transform.localPosition=Vector(row.at);go.AddComponent<BoxCollider>().size=Vector(row.size);}
                sun=MakeLight("Source sun",LightType.Directional,Vector3.zero,Color.white,1.4f,0);sun.transform.localRotation=Quaternion.Euler(40,30,0);
                foreach(float x in new[]{-4f,4f})MakeLight("Source blue fill",LightType.Point,new Vector3(x,4,1),Hex("b6e7ff"),4,12);
                var skyShader=Resources.Load<Shader>("R23LobbySky");if(skyShader==null)throw new IOException("R23 source sky shader missing");sky=new Material(skyShader);owned.Add(sky);
                Ready=true;
            }
            catch(Exception error){Failure=error.ToString();}
            Loading=false;if(Ready){content.SetActive(shown);if(shown)ApplyEnvironment();var callback=Loaded;if(callback!=null)callback();}
        }
        Material[] BuildMaterials()
        {
            var shader=Resources.Load<Shader>("R23LobbySurface");if(shader==null)throw new IOException("R23 source lobby surface shader missing");
            string[] colors={"414b58","1b2432","18e3f0","dc3488","ffffff"};float[] metals={.65f,.5f,.2f,.2f,.6f};var result=new Material[5];
            for(int i=0;i<5;i++){var m=new Material(shader){name="Actual R23 lobby "+i};m.SetColor("_BaseColor",Hex(colors[i]));m.SetFloat("_Metallic",metals[i]);m.SetFloat("_Roughness",i==4?.43f:.48f);m.SetFloat("_Facade",i==4?1:0);m.SetColor("_Emission",i==2||i==3?Hex(colors[i])*1.5f:Color.black);result[i]=m;owned.Add(m);}return result;
        }
        Light MakeLight(string label,LightType type,Vector3 at,Color color,float intensity,float range)
        {var go=new GameObject(label);go.transform.SetParent(content.transform,false);go.transform.localPosition=at;var light=go.AddComponent<Light>();light.type=type;light.color=color;light.intensity=intensity;if(type==LightType.Point)light.range=range;return light;}
        public void Show(){shown=true;if(Ready){content.SetActive(true);ApplyEnvironment();}}
        public void Hide(){shown=false;RestoreEnvironment();if(content!=null)content.SetActive(false);}
        void ApplyEnvironment()
        {
            if(environmentApplied)return;environmentApplied=true;previousSky=RenderSettings.skybox;previousSun=RenderSettings.sun;previousMode=RenderSettings.ambientMode;previousAmbient=RenderSettings.ambientLight;previousFog=RenderSettings.fog;
            RenderSettings.skybox=sky;RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Hex("bbd9ed")*.55f;RenderSettings.fog=false;
            if(cameraRef!=null){previousClear=cameraRef.clearFlags;previousBackground=cameraRef.backgroundColor;cameraRef.clearFlags=CameraClearFlags.Skybox;cameraRef.backgroundColor=Hex("a3c9e4");}
        }
        void RestoreEnvironment()
        {
            if(!environmentApplied)return;environmentApplied=false;RenderSettings.skybox=previousSky;RenderSettings.sun=previousSun;RenderSettings.ambientMode=previousMode;RenderSettings.ambientLight=previousAmbient;RenderSettings.fog=previousFog;
            if(cameraRef!=null){cameraRef.clearFlags=previousClear;cameraRef.backgroundColor=previousBackground;}
        }
        // Current source UI invokes this after pointer interaction; preview rotation owns visuals only.
        float previewHold;
        public void FacePreview(){if(PreviewAnchor==null||Head==null)return;var toward=Head.position-PreviewAnchor.position;toward.y=0;if(toward.sqrMagnitude>.000001f)PreviewAnchor.rotation=Quaternion.LookRotation(toward,Vector3.up);previewHold=.6f;}
        void Update(){if(!Ready||!shown)return;if(previewHold>0)previewHold=Mathf.Max(0,previewHold-Time.deltaTime);else PreviewAnchor.Rotate(Vector3.up,-Time.deltaTime*.54f*Mathf.Rad2Deg,Space.Self);}
        static Color Hex(string text){Color color;ColorUtility.TryParseHtmlString("#"+text,out color);return color;}
        static Vector3 ReadVector(BinaryReader reader){return new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());}
        static Vector3 Vector(float[] xyz){if(xyz==null||xyz.Length!=3)throw new IOException("source lobby vector");return new Vector3(xyz[0],xyz[1],xyz[2]);}
        void OnDisable(){Hide();}
        void OnDestroy(){RestoreEnvironment();if(content!=null)Destroy(content);foreach(var item in owned)if(item!=null)Destroy(item);owned.Clear();}
    }
}
#endif
