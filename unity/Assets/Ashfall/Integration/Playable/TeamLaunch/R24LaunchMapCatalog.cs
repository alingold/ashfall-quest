using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ashfall.Core;
#if UNITY_6000_0_OR_NEWER
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
#endif

namespace Ashfall.Integration.Playable.TeamLaunch
{
    public sealed class R24LaunchMapInfo
    {
        public readonly string Id,PayloadFolder,SourceSha256,ManifestSha256,CacheFingerprint,AdapterSha256;
        public readonly Float3 SourceOriginGodot,RemovedOriginGodot;
        public readonly float CatalogMinX,CatalogMinZ,CatalogMaxX,CatalogMaxZ,SolidMinY,SolidMaxY,DeploymentTop;
        public readonly bool HasArchiveMetadata,HasDeploymentTop;
        Float3[] spawns=new Float3[0],loot=new Float3[0];public Float3[] CandidateSpawns{get{return(Float3[])spawns.Clone();}}public Float3[] CandidateLoot{get{return(Float3[])loot.Clone();}}
        public bool CandidatesReady{get;internal set;}public string CandidateFailure{get;internal set;}
        public Float3 RepresentedOriginUnity{get{return FromGodotWorld(SourceOriginGodot);}}
        public R24LaunchMapInfo(string id,string folder,Float3 origin,Float3 removed,float minX,float minZ,float maxX,float maxZ,bool archive,float solidMin,float solidMax,bool hasTop,float top,string sourceSha="",string manifestSha="",string cache="",string adapter="")
        {if(String.IsNullOrEmpty(id)||!origin.IsFinite||!removed.IsFinite||maxX<=minX||maxZ<=minZ)throw new ArgumentException("Actual source map catalog metadata");Id=id;PayloadFolder=folder;SourceOriginGodot=origin;RemovedOriginGodot=removed;CatalogMinX=minX;CatalogMinZ=minZ;CatalogMaxX=maxX;CatalogMaxZ=maxZ;HasArchiveMetadata=archive;SolidMinY=solidMin;SolidMaxY=solidMax;HasDeploymentTop=hasTop;DeploymentTop=top;SourceSha256=sourceSha;ManifestSha256=manifestSha;CacheFingerprint=cache;AdapterSha256=adapter;}
        public Float3 FromGodotWorld(Float3 original){var p=R24LaunchMath.Sub(original,RemovedOriginGodot);return new Float3(p.X,p.Y,-p.Z);}
        public Float3 ToGodotWorld(Float3 represented)
        {if(!represented.IsFinite)throw new ArgumentException("Finite represented map point required");return new Float3(represented.X+RemovedOriginGodot.X,represented.Y+RemovedOriginGodot.Y,-represented.Z+RemovedOriginGodot.Z);}
        public R24LaunchMap ToLaunchMap(Float3 actualMapWorldTranslation,float actualCollisionTop)
        {
            if(!actualMapWorldTranslation.IsFinite||!HasDeploymentTop&&!R24LaunchMath.Finite(actualCollisionTop))throw new ArgumentException("Real map translation/native collision top required");
            var origin=R24LaunchMath.Add(RepresentedOriginUnity,actualMapWorldTranslation);
            float top=HasDeploymentTop?DeploymentTop-RemovedOriginGodot.Y+actualMapWorldTranslation.Y:Math.Max(origin.Y+40,actualCollisionTop);
            return new R24LaunchMap{Id=Id,Minimum=new Float3(origin.X+CatalogMinX,0,origin.Z-CatalogMaxZ),Maximum=new Float3(origin.X+CatalogMaxX,0,origin.Z-CatalogMinZ),OriginY=origin.Y,Top=top};
        }
        internal void SetCandidates(Float3[] actualSpawns,Float3[] actualLoot){spawns=actualSpawns;loot=actualLoot;CandidatesReady=true;CandidateFailure=null;}
    }
    public sealed class R24LaunchMapCatalog
    {
        readonly Dictionary<string,R24LaunchMapInfo> rows=new Dictionary<string,R24LaunchMapInfo>(StringComparer.Ordinal);
        public bool Ready{get;private set;}public bool Loading{get;private set;}public string Failure{get;private set;}public int Count{get{return rows.Count;}}
        public bool TryRead(string mapId,out R24LaunchMapInfo row){row=null;return mapId!=null&&rows.TryGetValue(mapId,out row);}
        // Existing exported numeric arrays are read without duplicating their
        // large map/city payloads into this small source catalog.
        public static Float3[] ReadTriples(string json,string field,bool reflectGodot)
        {
            int at=json.IndexOf("\""+field+"\"",StringComparison.Ordinal);if(at<0)return new Float3[0];at=json.IndexOf('[',at);if(at<0)throw new InvalidDataException(field+" array");at++;var result=new List<Float3>();Action white=delegate{while(at<json.Length&&Char.IsWhiteSpace(json[at]))at++;};
            white();while(at<json.Length&&json[at]!=']'){if(result.Count>=16384||json[at++]!='[')throw new InvalidDataException(field+" tuple");var v=new float[3];for(int axis=0;axis<3;axis++){white();int first=at;while(at<json.Length&&(Char.IsDigit(json[at])||json[at]=='-'||json[at]=='+'||json[at]=='.'||json[at]=='e'||json[at]=='E'))at++;if(first==at||!Single.TryParse(json.Substring(first,at-first),NumberStyles.Float,CultureInfo.InvariantCulture,out v[axis])||!R24LaunchMath.Finite(v[axis]))throw new InvalidDataException(field+" finite number");white();if(axis<2&&(at>=json.Length||json[at++]!=','))throw new InvalidDataException(field+" comma");}if(at>=json.Length||json[at++]!=']')throw new InvalidDataException(field+" tuple end");result.Add(new Float3(v[0],v[1],reflectGodot?-v[2]:v[2]));white();if(at<json.Length&&json[at]==','){at++;white();}else break;}if(at>=json.Length||json[at]!=']')throw new InvalidDataException(field+" array end");return result.ToArray();
        }
#if UNITY_6000_0_OR_NEWER
        [Serializable]sealed class Data{public int version=0;public string contentId="",sourceManifestSha256="";public Row[] rows=null;}
        [Serializable]sealed class Row{public string id="",payloadFolder="",sourceSha256="",manifestSha256="",cacheFingerprint="",adapterSha256="";public float[] sourceOrigin=null,removedOrigin=null;public float catalogMinX=0,catalogMinZ=0,catalogMaxX=0,catalogMaxZ=0,solidMinY=0,solidMaxY=0,deploymentTop=0;public bool hasArchiveMetadata=false,hasDeploymentTop=false;}
        [Serializable]sealed class MapData{public string mapId="",sourceSha256="";public float[] gameSpawn=null,spawn=null;}
        public IEnumerator Load()
        {
            if(Ready||Loading)yield break;Loading=true;Failure=null;
            using(var request=UnityWebRequest.Get(Url("TeamLaunch/launch-maps.json"))){request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Failure=request.error;Loading=false;yield break;}try{var data=JsonUtility.FromJson<Data>(request.downloadHandler.text);if(data==null||data.version!=1||data.contentId!="23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520"||data.sourceManifestSha256!="b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111"||data.rows==null)throw new InvalidDataException("Actual R6 launch map catalog missing");foreach(var row in data.rows)rows.Add(row.id,new R24LaunchMapInfo(row.id,row.payloadFolder,V(row.sourceOrigin),V(row.removedOrigin),row.catalogMinX,row.catalogMinZ,row.catalogMaxX,row.catalogMaxZ,row.hasArchiveMetadata,row.solidMinY,row.solidMaxY,row.hasDeploymentTop,row.deploymentTop,row.sourceSha256,row.manifestSha256,row.cacheFingerprint,row.adapterSha256));Ready=true;}catch(Exception error){rows.Clear();Failure=error.ToString();}}Loading=false;
        }
        public IEnumerator LoadCandidates(string mapId)
        {
            R24LaunchMapInfo row;if(!Ready||!TryRead(mapId,out row))throw new ArgumentException("Load actual map catalog first");if(row.CandidatesReady)yield break;var spawns=new List<Float3>();var loot=new Float3[0];
            using(var request=UnityWebRequest.Get(Url(row.PayloadFolder+"/map.json"))){request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){row.CandidateFailure=request.error;yield break;}try{var data=JsonUtility.FromJson<MapData>(request.downloadHandler.text);if(data.mapId!=row.Id||data.sourceSha256!=row.SourceSha256)throw new InvalidDataException("Launch candidate source map mismatch");var position=data.gameSpawn??data.spawn;if(position!=null)spawns.Add(V(position));}catch(Exception error){row.CandidateFailure=error.ToString();yield break;}}
            if(row.Id=="cyber_bird")using(var request=UnityWebRequest.Get(Url(row.PayloadFolder+"/city.json"))){request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){row.CandidateFailure=request.error;yield break;}try{var world=ReadTriples(request.downloadHandler.text,"lootPoints",false);loot=new Float3[world.Length];for(int i=0;i<world.Length;i++)loot[i]=row.FromGodotWorld(world[i]);}catch(Exception error){row.CandidateFailure=error.ToString();yield break;}}
            row.SetCandidates(spawns.ToArray(),loot);
        }
        // Source builtins without _meta/deployment_top scan actual enabled shapes,
        // never render bounds. Root calls this only after its map is truly loaded.
        public static float ScanCollisionTop(Transform actualMap,float sourceOriginWorldY)
        {
            if(actualMap==null)throw new ArgumentNullException("actualMap");float top=sourceOriginWorldY+40;
            foreach(var collider in actualMap.GetComponentsInChildren<Collider>(true))
            {
                if(collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy)continue;
                var launch=collider.GetComponentInParent<R24TeamLaunch>();if(launch!=null&&launch.OwnsCollider(collider))continue;
                float at=collider.bounds.max.y;var capsule=collider as CapsuleCollider;
                if(capsule!=null){var scale=capsule.transform.lossyScale;at=capsule.transform.TransformPoint(capsule.center).y+capsule.height*.5f*Mathf.Abs(scale.y)+capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));}
                top=Mathf.Max(top,at);
            }
            return top;
        }
        static Float3 V(float[] point){if(point==null||point.Length!=3)throw new InvalidDataException("Actual source map vector");return new Float3(point[0],point[1],point[2]);}
        static string Url(string relative){string url=Application.streamingAssetsPath+"/PlayableR23/"+relative;return url.Contains("://")?url:new Uri(url).AbsoluteUri;}
#endif
    }
}
