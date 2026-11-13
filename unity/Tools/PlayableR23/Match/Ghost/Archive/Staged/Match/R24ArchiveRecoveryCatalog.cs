#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Ashfall.Core;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Match
{
    public sealed class R24ArchiveRecoveryCatalog
    {
        const string ExpectedSha256="72c1827251d48c02b35530ba7ff694abef5a7ff09b0938ae71fa9b9fb64dc8b7";
        [Serializable]sealed class P{public float x=0,y=0,z=0;public Float3 V(){return new Float3(x,y,z);}}
        [Serializable]sealed class B{public P position=null,size=null;public R24ArchiveBox V(){return new R24ArchiveBox(position.V(),size.V());}}
        [Serializable]sealed class Plane{public double x=0,y=0,z=0,d=0;}
        [Serializable]sealed class Hazard{public P minimum=null,maximum=null;public Plane[] planes=null;public R24ArchiveHazard V(){var values=new double[planes.Length*4];for(int i=0;i<planes.Length;i++){values[i*4]=planes[i].x;values[i*4+1]=planes[i].y;values[i*4+2]=planes[i].z;values[i*4+3]=planes[i].d;}return new R24ArchiveHazard(minimum.V(),maximum.V(),values);}}
        [Serializable]sealed class Row{public string id="",sourceSha256="",manifestSha256="",cacheFingerprint="",adapterSha256="";public P boundsMin=null,boundsMax=null,solidMin=null,solidMax=null,center=null;public P[] recorded=null,deathmatch=null,warmup=null;public B[] spawnFloors=null,panels=null;public Hazard[] hazards=null;}
        [Serializable]sealed class Data{public int version=0;public string sourceManifestSha256="",contentId="";public Row[] rows=null;}
        readonly Dictionary<string,R24ArchiveRecoveryInfo> rows=new Dictionary<string,R24ArchiveRecoveryInfo>(StringComparer.Ordinal);
        public bool Ready{get;private set;}public bool Loading{get;private set;}public string Failure{get;private set;}
        public bool TryRead(string id,out R24ArchiveRecoveryInfo value){value=null;return id!=null&&rows.TryGetValue(id,out value);}
        static Float3[] Points(P[] p){var r=new Float3[p.Length];for(int i=0;i<r.Length;i++)r[i]=p[i].V();return r;}
        static R24ArchiveBox[] Boxes(B[] p){var r=new R24ArchiveBox[p.Length];for(int i=0;i<r.Length;i++)r[i]=p[i].V();return r;}
        public IEnumerator Load()
        {
            if(Ready||Loading||Failure!=null)yield break;Loading=true;
            string path=Application.streamingAssetsPath+"/PlayableR23/TeamLaunch/ArchiveRecovery/maps.json";if(!path.Contains("://"))path=new Uri(path).AbsoluteUri;
            using(var request=UnityWebRequest.Get(path))
            {
                request.timeout=30;yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){Failure=request.error;Loading=false;yield break;}
                try
                {
                    byte[] bytes=request.downloadHandler.data;string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                    if(hash!=ExpectedSha256)throw new InvalidDataException("Archive recovery payload does not match its frozen resource export");
                    var data=JsonUtility.FromJson<Data>(request.downloadHandler.text);
                    if(data==null||data.version!=1||data.sourceManifestSha256!="b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111"||data.contentId!="23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520"||data.rows==null||data.rows.Length!=105)throw new InvalidDataException("Actual R6 archive recovery metadata required");
                    foreach(var row in data.rows){var hazards=new R24ArchiveHazard[row.hazards.Length];for(int i=0;i<hazards.Length;i++)hazards[i]=row.hazards[i].V();rows.Add(row.id,new R24ArchiveRecoveryInfo(row.id,row.sourceSha256,row.manifestSha256,row.cacheFingerprint,row.adapterSha256,row.boundsMin.V(),row.boundsMax.V(),row.solidMin.V(),row.solidMax.V(),row.center.V(),Points(row.recorded),Points(row.deathmatch),Points(row.warmup),Boxes(row.spawnFloors),Boxes(row.panels),hazards));}
                    Ready=true;
                }
                catch(Exception error){rows.Clear();Failure=error.ToString();}
            }
            Loading=false;
        }
    }
}
#endif
