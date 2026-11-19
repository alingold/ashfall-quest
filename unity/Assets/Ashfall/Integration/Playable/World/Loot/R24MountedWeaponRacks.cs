#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.TeamLaunch;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.World.Loot
{
    // Source signs only. Geometry queries and finite guns remain with the real
    // shared world and weapon owners; hardware adds no collision or inventory.
    public sealed class R24MountedWeaponRacks:MonoBehaviour,IR24WeaponRackWorld
    {
        R23PlayableLoop root;Manifest manifest;MapRow row;string mapId;long generation=-1;bool worldActive,itemsEnabled;float clock,visualClock;
        readonly List<string> instances=new List<string>();readonly List<R24WeaponRackPlan> plans=new List<R24WeaponRackPlan>();
        readonly List<GameObject> hardware=new List<GameObject>();readonly List<Mesh> meshes=new List<Mesh>();bool[] attempted=new bool[0];Material metal;
        public bool Ready{get;private set;}public string Failure{get;private set;}public int AttemptedBoards{get;private set;}public int RejectedBoards{get;private set;}
        public int ValidatedBoards{get{return plans.Count;}}public int CandidateBoards{get{return attempted.Length;}}public string PreparedMapId{get{return mapId;}}
        public string[] WorldInstanceActionIds{get{return instances.ToArray();}}public R24WeaponRackPlan[] Plans{get{return plans.ToArray();}}
        public R24WeaponBoard[] SourceBoards{get{if(row==null)return new R24WeaponBoard[0];var result=new R24WeaponBoard[row.boards.Length];for(int i=0;i<result.Length;i++)result[i]=row.boards[i].Read();return result;}}
        public string LastRefusal{get;private set;}
        public void Initialize(R23PlayableLoop actualRoot){if(root!=null||actualRoot==null)throw new ArgumentException("Actual root required once");root=actualRoot;StartCoroutine(Load());}
        IEnumerator Load()
        {
            string url=Application.streamingAssetsPath+"/PlayableR23/Loot/weapon-boards-r18.json";if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;
            using(var request=UnityWebRequest.Get(url))
            {
                request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Failure=request.error;yield break;}
                try
                {
                    // r19 MAPKIT: the whole-file hash pin and the fixed 37-map / 1,344-board counts are gone so imported maps can add rows; every row is still bound to the loaded map by source hash + cache fingerprint (Prepare).
                    manifest=JsonUtility.FromJson<Manifest>(request.downloadHandler.text);if(manifest==null||manifest.version!=1||manifest.contentId!="23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520"||manifest.sourceManifestSha256!="b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111"||manifest.maps==null||manifest.maps.Length==0)throw new InvalidOperationException("Frozen R6 rack source required");
                    var maps=new HashSet<string>();int total=0;foreach(var map in manifest.maps){if(map==null||!maps.Add(map.mapId)||map.sourceSha256.Length!=64||map.cacheFingerprint.Length!=64||map.boards==null)throw new InvalidOperationException("Unique source rack map");var ids=new HashSet<string>();foreach(var board in map.boards){if(board==null||!ids.Add(board.id))throw new InvalidOperationException("Unique source board");board.Read();total++;}}if(total==0)throw new InvalidOperationException("Prepared board membership required");Ready=true;
                }catch(Exception e){Failure=e.ToString();}
            }
        }
        bool ContextReady{get{return root!=null&&root.ArchiveWorldReady&&root.Map!=null&&root.Map.IsLoaded&&root.ArchiveWorldMapId==root.Map.MapId;}}
        public bool AllowsWorldClaim(string id){return !instances.Contains(id)||worldActive&&itemsEnabled&&ContextReady&&mapId==root.Map.MapId&&generation==root.ArchiveWorldGeneration;}
        public void SetWorldEnabled(bool active,bool allowItems)
        {
            worldActive=active;itemsEnabled=active&&allowItems;
            foreach(var go in hardware)if(go!=null)go.SetActive(active);
            if(!itemsEnabled)HideItems();
        }
        public void WithdrawMap(){ClearWorld();mapId=null;row=null;generation=-1;attempted=new bool[0];AttemptedBoards=RejectedBoards=0;LastRefusal=null;clock=visualClock=0;}
        public void ResetMap(){WithdrawMap();}
        void ClearWorld()
        {
            if(root!=null&&root.Gameplay!=null)foreach(string id in instances)root.Gameplay.DespawnWorldWeapon(id);instances.Clear();plans.Clear();
            foreach(var go in hardware)if(go!=null){go.SetActive(false);Destroy(go);}hardware.Clear();foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);meshes.Clear();
        }
        void HideItems(){if(root!=null&&root.Gameplay!=null)foreach(string id in instances)root.Gameplay.SetWorldWeaponVisible(id,false);}
        void Prepare()
        {
            // A menu/focus or planner context refresh must not replenish a
            // board already visited. Actual map/reset paths call WithdrawMap.
            if(mapId==root.Map.MapId&&row!=null){generation=root.ArchiveWorldGeneration;return;}
            WithdrawMap();mapId=root.Map.MapId;generation=root.ArchiveWorldGeneration;
            foreach(var item in manifest.maps)if(item.mapId==mapId){row=item;break;}if(row==null)return;
            R24LaunchMapInfo frame;if(!root.LaunchMapCatalog.TryRead(mapId,out frame)||frame.SourceSha256!=row.sourceSha256||frame.CacheFingerprint!=row.cacheFingerprint||root.Map.SourceSha256!=row.sourceSha256){LastRefusal="Source rack/map binding differs: "+mapId;row=null;return;}
            attempted=new bool[row.boards.Length];
        }
        void Update()
        {
            if(!worldActive||!Ready||Failure!=null||root==null||!root.Gameplay.Ready||!ContextReady)return;
            if(mapId!=root.Map.MapId||generation!=root.ArchiveWorldGeneration)Prepare();if(Failure!=null||row==null)return;
            clock+=Time.deltaTime;visualClock+=Time.deltaTime;if(clock>=.25f){clock=0;ActivateNear();}if(visualClock>=.35f){visualClock=0;StreamVisuals();}
        }
        public void ActivateNear()
        {
            if(!worldActive||!ContextReady||row==null||generation!=root.ArchiveWorldGeneration)return;
            Float3 actor;if(!root.TryArchiveWorldToSource(root.Rig.transform.position,out actor))return;int budget=2;
            for(int b=0;b<attempted.Length&&budget>0;b++)
            {
                if(attempted[b])continue;var board=row.boards[b].Read();var d=R24WeaponRackMath.Sub(board.Center,actor);if(R24WeaponRackMath.Dot(d,d)>=4900)continue;
                attempted[b]=true;AttemptedBoards++;budget--;R24WeaponRackPlan plan;string refusal;if(!R24WeaponRackMath.TryBuild(mapId,board,this,out plan,out refusal)){RejectedBoards++;LastRefusal=refusal;continue;}
                if(!CreateBoard(plan)){RejectedBoards++;continue;}plans.Add(plan);
            }
        }
        bool CreateBoard(R24WeaponRackPlan plan)
        {
            int[] catalog=R24WeaponRackMath.Catalog;var positions=new Vector3[catalog.Length];var profiles=new string[catalog.Length];
            Vector3 right=Direction(plan.Right);Quaternion rotation=Quaternion.LookRotation(-right,Vector3.up);
            for(int slot=0;slot<catalog.Length;slot++){if(!root.TryArchiveSourceToWorld(plan.Position(slot),out positions[slot]))return false;foreach(string id in root.Gameplay.AvailableWeaponIds)if(R23WeaponPickupRules.CatalogIndex(id)==catalog[slot]){profiles[slot]=id;break;}if(profiles[slot]==null){LastRefusal="Source rack profile unavailable: "+catalog[slot];return false;}}
            var created=new List<string>();for(int slot=0;slot<catalog.Length;slot++){string id;if(!root.Gameplay.SpawnWorldWeapon(profiles[slot],5,positions[slot],rotation,out id,false)){foreach(var old in created)root.Gameplay.DespawnWorldWeapon(old);LastRefusal="Actual weapon owner refused board";return false;}created.Add(id);if(!root.Gameplay.MarkWorldWeaponWallMounted(id)){foreach(var old in created)root.Gameplay.DespawnWorldWeapon(old);LastRefusal="Actual weapon owner refused source wall-mount disposition";return false;}}
            try{BuildHardware(plan);}catch{foreach(var id in created)root.Gameplay.DespawnWorldWeapon(id);throw;}instances.AddRange(created);return true;
        }
        void StreamVisuals()
        {
            int shown=0;foreach(string id in instances){string actual;Vector3 center,at;Quaternion rotation;if(!root.Gameplay.TryReadWorldWeapon(id,out actual,out center,out at,out rotation))continue;bool near=itemsEnabled&&(at-root.Rig.transform.position).sqrMagnitude<1600;if(!near)root.Gameplay.SetWorldWeaponVisible(id,false);else if(shown<12&&!root.Gameplay.IsWorldWeaponVisible(id)&&root.Gameplay.SetWorldWeaponVisible(id,true))shown++;}
        }
        Vector3 Point(Float3 source){Vector3 value;if(!root.TryArchiveSourceToWorld(source,out value))throw new InvalidOperationException("Active source rack frame required");return value;}
        Vector3 Direction(Float3 source){return root.Map.transform.TransformDirection(new Vector3(source.X,source.Y,-source.Z)).normalized;}
        void BuildHardware(R24WeaponRackPlan plan)
        {
            if(metal==null){Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("Rack metal shader unavailable");metal=new Material(shader){name="Source R18 rack metal"};metal.SetColor("_BaseColor",new Color(26/255f,36/255f,43/255f,1));metal.SetFloat("_Metallic",.65f);metal.SetFloat("_Smoothness",.35f);}
            // Build the actual thin source box; primitive creation would add a
            // collider and interfere with the source access rays.
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();
            var center=plan.BackplateCenter;float x=plan.Columns*1.55f*.5f,y=.055f,z=.04f;
            Float3[] corners=new Float3[8];for(int i=0;i<8;i++)corners[i]=R24WeaponRackMath.Add(R24WeaponRackMath.Add(R24WeaponRackMath.Add(center,R24WeaponRackMath.Scale(plan.Right,(i&1)==0?-x:x)),new Float3(0,(i&2)==0?-y:y,0)),R24WeaponRackMath.Scale(plan.Normal,(i&4)==0?-z:z));
            int[][] faces={new[]{0,2,3,1},new[]{4,5,7,6},new[]{0,1,5,4},new[]{2,6,7,3},new[]{0,4,6,2},new[]{1,3,7,5}};
            foreach(var face in faces)AddQuad(vertices,normals,indices,corners[face[0]],corners[face[1]],corners[face[2]],corners[face[3]],null);
            for(int slot=0;slot<plan.Count;slot++)foreach(float side in new[]{-.32f,.32f}){var a=R24WeaponRackMath.Sub(R24WeaponRackMath.Sub(R24WeaponRackMath.Add(plan.Position(slot),R24WeaponRackMath.Scale(plan.Right,side)),R24WeaponRackMath.Scale(plan.Normal,.15f)),new Float3(0,.12f,0));a=R24WeaponRackMath.Sub(a,R24WeaponRackMath.Scale(plan.Right,.025f));var b=R24WeaponRackMath.Add(a,new Float3(0,.11f,0));var c=R24WeaponRackMath.Add(b,R24WeaponRackMath.Scale(plan.Right,.05f));var d=R24WeaponRackMath.Add(a,R24WeaponRackMath.Scale(plan.Right,.05f));AddQuad(vertices,normals,indices,a,b,c,d,plan.Normal);}
            var mesh=new Mesh{name="Source R18 backplate and hooks"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();meshes.Add(mesh);var go=new GameObject("Source weapon board "+plan.Board.Id);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=metal;hardware.Add(go);go.SetActive(worldActive);
        }
        void AddQuad(List<Vector3> vertices,List<Vector3> normals,List<int> indices,Float3 a,Float3 b,Float3 c,Float3 d,Float3? sourceNormal)
        {
            Vector3 pa=Point(a),pb=Point(b),pc=Point(c),pd=Point(d);Vector3 n=sourceNormal.HasValue?Direction(sourceNormal.Value):Vector3.Cross(pc-pa,pb-pa).normalized;int first=vertices.Count;
            foreach(var p in new[]{pa,pb,pc,pd})vertices.Add(transform.InverseTransformPoint(p));for(int i=0;i<4;i++)normals.Add(transform.InverseTransformDirection(n));foreach(int index in new[]{0,2,1,0,3,2})indices.Add(first+index);
        }
        public bool Floor(Float3 from,float depth,out Float3 floor){return root.ArchiveWorldFloor(from,depth,out floor);}
        public bool SafeSpawn(Float3 point){return root.ArchiveWorldSafeSpawn(point);}
        public bool Ray(Float3 from,Float3 to,int sourceMask,out R24RackHit result)
        {
            result=new R24RackHit();Vector3 a,b;if(!root.TryArchiveSourceToWorld(from,out a)||!root.TryArchiveSourceToWorld(to,out b))throw new InvalidOperationException("Active source rack ray required");Vector3 delta=b-a;if(delta.sqrMagnitude<1e-10f)return false;RaycastHit nearest=new RaycastHit();float distance=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(a,delta.normalized,delta.magnitude,sourceMask==9?(1<<0)|(1<<19):1,QueryTriggerInteraction.Ignore)){var c=hit.collider;if(c==null||c.transform.IsChildOf(root.Rig.transform)||root.Gameplay.IsSourceActorLayer4(c))continue;if(hit.distance<distance){nearest=hit;distance=hit.distance;}}
            if(float.IsPositiveInfinity(distance))return false;Float3 point;if(!root.TryArchiveWorldToSource(nearest.point,out point))throw new InvalidOperationException("Source rack hit frame retired");var normal=root.Map.transform.InverseTransformDirection(nearest.normal);result=new R24RackHit{Point=point,Normal=new Float3(normal.x,normal.y,-normal.z)};return true;
        }
        void OnDestroy(){ClearWorld();if(metal!=null)Destroy(metal);}
        static Float3 V(float[] value){if(value==null||value.Length!=3)throw new ArgumentException("Source board vector");return new Float3(value[0],value[1],value[2]);}
        [Serializable]sealed class Manifest{public int version=0;public string contentId="",sourceManifestSha256="";public MapRow[] maps=null;}
        [Serializable]sealed class MapRow{public string mapId="",sourceSha256="",cacheFingerprint="";public BoardRow[] boards=null;}
        [Serializable]sealed class BoardRow{public string id="";public float[] min=null,size=null,normal=null;public bool hasFloorHint=false;public float floorHint=0;public R24WeaponBoard Read(){return new R24WeaponBoard(id,V(min),V(size),V(normal),hasFloorHint,floorHint);}}
    }
}
#endif
