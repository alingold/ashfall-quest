using System;
using Ashfall.Core;
#if UNITY_6000_0_OR_NEWER
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
#endif

namespace Ashfall.Integration.Playable.JumpPads
{
    public sealed class R23SourceHealthStation
    {
        public readonly string Id;
        public readonly Float3 Center,Normal;
        public readonly double Radius;
        public readonly float HealthPerSecond;
        public R23SourceHealthStation(string id,Float3 center,Float3 normal,double radius,float healthPerSecond)
        {
            double length=Math.Sqrt((double)normal.X*normal.X+(double)normal.Y*normal.Y+(double)normal.Z*normal.Z);
            if(String.IsNullOrEmpty(id)||!center.IsFinite||!normal.IsFinite||length<.000001||Double.IsNaN(radius)||Double.IsInfinity(radius)||radius<=0||
                Single.IsNaN(healthPerSecond)||Single.IsInfinity(healthPerSecond)||healthPerSecond<=0)throw new ArgumentException("source health station");
            Id=id;Center=center;Normal=new Float3((float)(normal.X/length),(float)(normal.Y/length),(float)(normal.Z/length));Radius=radius;HealthPerSecond=healthPerSecond;
        }
    }
    // map_semantics_r14.gd: contains_face(.75,-.20), _clear_contact and max rate.
    public static class R23HealthContactMath
    {
        public static bool Contains(R23SourceHealthStation row,Float3 point)
        {
            if(row==null||!point.IsFinite)return false;
            Float3 delta=Subtract(point,row.Center);float signed=Dot(delta,row.Normal);
            Float3 radial=Subtract(delta,Multiply(row.Normal,signed));
            // Godot Vector3 arithmetic is float32; its script face limits/radius are doubles.
            return (double)signed>=-.20&&(double)signed<=.75&&(double)Dot(radial,radial)<=row.Radius*row.Radius;
        }
        public static void ContactSegment(R23SourceHealthStation row,Float3 point,out Float3 start,out Float3 end)
        {start=Add(row.Center,Multiply(row.Normal,.20f));end=Add(point,Multiply(row.Normal,.12f));}
        public static bool NeedsContactRay(Float3 start,Float3 end)
        {Float3 delta=Subtract(end,start);return (double)Dot(delta,delta)>=.01;}
        static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        static Float3 Subtract(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        static Float3 Multiply(Float3 a,float k){return new Float3(a.X*k,a.Y*k,a.Z*k);}
    }
    public sealed class R23HealthContacts
    {
        readonly R23SourceHealthStation[] stations;
        public R23HealthContacts(R23SourceHealthStation[] authored)
        {if(authored==null)throw new ArgumentNullException("authored");stations=(R23SourceHealthStation[])authored.Clone();foreach(var row in stations)if(row==null)throw new ArgumentException("null station");}
        public float RateAt(Float3 actualFeet,Func<Float3,Float3,bool> clearContact)
        {
            if(!actualFeet.IsFinite||clearContact==null)return 0;float maximum=0;
            foreach(var row in stations)
            {
                if(!R23HealthContactMath.Contains(row,actualFeet))continue;Float3 start,end;
                R23HealthContactMath.ContactSegment(row,actualFeet,out start,out end);
                if(R23HealthContactMath.NeedsContactRay(start,end)&&!clearContact(start,end))continue;
                maximum=Math.Max(maximum,row.HealthPerSecond);
            }
            return maximum;
        }
        public bool Step(Float3 actualFeet,float seconds,bool enabled,Func<Float3,Float3,bool> clearContact,Func<float,bool> applyHealthGain,out float rate,out float requestedGain)
        {
            rate=requestedGain=0;
            if(!enabled||Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<=0||seconds>.1f||applyHealthGain==null)return false;
            rate=RateAt(actualFeet,clearContact);if(rate<=0)return false;
            requestedGain=rate*seconds;return applyHealthGain(requestedGain);
        }
    }
#if UNITY_6000_0_OR_NEWER
    // Root supplies accepted feet once per frame. Only its real health owner applies gain.
    public sealed class R23HealthStations:MonoBehaviour
    {
        [Serializable]sealed class Manifest {public int version=0;public Map[] maps=null;}
        [Serializable]sealed class Map {public string mapId="",sourceSha256="",cacheFingerprint="";public Row[] stations=null;}
        [Serializable]sealed class Row {public int sourceIndex=0;public float[] center=null,normal=null;public double radius=0;public float healthPerSecond=0;}
        Transform actorRoot,mapRoot;Func<bool> restore,eligibility;Manifest data;R23HealthStationRecharge recharge;R23HealthStationVisuals visuals;float pollClock,sfxVolume=1;
        R23SourceHealthStation[] authored=new R23SourceHealthStation[0];
        public bool Ready {get;private set;}public bool Loading {get;private set;}
        public string MapId {get;private set;}public string Failure {get;private set;}
        public int StationCount {get{return authored.Length;}}
        public float LastRate {get;private set;}public float LastRequestedGain {get;private set;}
        public bool LastApplicationAccepted {get;private set;}
        public int LastRestoredStationIndex {get;private set;}
        public bool RestoreBound {get{return restore!=null;}}
        public bool RestoreAudioReady {get{return visuals!=null&&visuals.AudioReady;}}
        public void SetSfxVolume(float value){sfxVolume=float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp01(value);if(visuals!=null)visuals.SetSfxVolume(sfxVolume);}
        public double CooldownRemaining(int stationIndex){return recharge==null?0:recharge.Remaining(stationIndex,Time.realtimeSinceStartupAsDouble);}
        public void BindRestore(Func<bool> actualRestore,Func<bool> eligible=null){restore=actualRestore;eligibility=eligible;}
        public R23SourceHealthStation[] AuthoredStations {get{return (R23SourceHealthStation[])authored.Clone();}}
        // Compatibility signature: never silently restore health alone. Root must
        // bind the R18 health+shield owner before stations can consume recharge.
        public void Initialize(string mapId,Transform playerRoot,Transform loadedMapRoot,Func<float,bool> legacyHealthGain)
        {Initialize(mapId,playerRoot,loadedMapRoot,(Func<bool>)null,null);}
        public void Initialize(string mapId,Transform playerRoot,Transform loadedMapRoot,Func<bool> actualRestore,Func<bool> eligible=null)
        {
            if(actorRoot!=null)throw new InvalidOperationException("health stations already initialized");
            if(playerRoot==null||loadedMapRoot==null)throw new ArgumentException("actor and native map required");
            actorRoot=playerRoot;mapRoot=loadedMapRoot;BindRestore(actualRestore,eligible);MapId=CanonicalMap(mapId);StartCoroutine(Load());
        }
        IEnumerator Load()
        {
            Loading=true;string url=Application.streamingAssetsPath+"/PlayableR23/HealthStations/stations.json";if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;
            using(var request=UnityWebRequest.Get(url))
            {
                request.timeout=30;yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){Failure=request.error;Loading=false;yield break;}
                try
                {
                    data=JsonUtility.FromJson<Manifest>(request.downloadHandler.text);
                    if(data==null||data.version!=1||data.maps==null)throw new InvalidOperationException("source health station manifest");
                    SetMap(MapId);
                }
                catch(Exception error){Failure=error.Message;Ready=false;}
            }
            Loading=false;
        }
        public bool SetMap(string mapId,bool actualMapReplacement=false)
        {
            string canonical=CanonicalMap(mapId);
            if(Ready&&MapId==canonical&&!actualMapReplacement){ResetContact();return true;}
            if(visuals!=null){visuals.Dispose();visuals=null;}
            MapId=canonical;Ready=false;recharge=null;authored=new R23SourceHealthStation[0];Failure=null;ResetContact();pollClock=0;
            if(data==null)return false;
            if(mapRoot==null){Failure="Native map root unavailable";return false;}
            Vector3 scale=mapRoot.lossyScale;
            if(Mathf.Abs(scale.x-1)>.0001f||Mathf.Abs(scale.y-1)>.0001f||Mathf.Abs(scale.z-1)>.0001f)
            {Failure="Source station distances require unit-scale native map";return false;}
            foreach(var map in data.maps)if(map.mapId==MapId)
            {
                try
                {
                    var rows=new R23SourceHealthStation[map.stations.Length];
                    for(int i=0;i<rows.Length;i++)
                    {
                        var row=map.stations[i];rows[i]=new R23SourceHealthStation(MapId+"/health/"+row.sourceIndex,
                            F(mapRoot.TransformPoint(V(row.center))),F(mapRoot.TransformDirection(V(row.normal))),row.radius,row.healthPerSecond);
                    }
                    authored=rows;recharge=new R23HealthStationRecharge(rows.Length);visuals=new R23HealthStationVisuals(transform,actorRoot,rows);visuals.SetSfxVolume(sfxVolume);Ready=true;return true;
                }
                catch(Exception error){Failure=error.Message;return false;}
            }
            Failure="No original archive health semantics for "+MapId;return false;
        }
        // A round restart/pause only resets transient contact, not source ready_at.
        public void ResetContact(){LastRate=LastRequestedGain=0;LastApplicationAccepted=false;LastRestoredStationIndex=-1;}
        public bool Step(Vector3 actualFeet,float seconds,bool enabled)
        {
            ResetContact();if(!Ready||!isActiveAndEnabled||recharge==null)return false;
            double now=Time.realtimeSinceStartupAsDouble;
            if(visuals!=null)visuals.Refresh(now,recharge);
            if(Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<0)return false;
            pollClock+=seconds;if(pollClock<.1f)return false;pollClock=0;
            if(!enabled||restore==null||(eligibility!=null&&!eligibility()))return false;
            Physics.SyncTransforms();Float3 feet=F(actualFeet);
            for(int i=0;i<authored.Length;i++)
            {
                var row=authored[i];if(!R23HealthContactMath.Contains(row,feet))continue;
                Float3 start,end;R23HealthContactMath.ContactSegment(row,feet,out start,out end);
                if(R23HealthContactMath.NeedsContactRay(start,end)&&!ClearContact(start,end))continue;
                bool accepted=recharge.TryRestore(i,now,restore);LastApplicationAccepted=accepted;
                if(accepted){LastRestoredStationIndex=i;if(visuals!=null){visuals.PlayRestore(actualFeet);visuals.Refresh(now,recharge);}}
                return accepted; // Source station_at returns its first valid contact.
            }
            return false;
        }
        bool ClearContact(Float3 from,Float3 to)
        {
            Vector3 start=U(from),end=U(to),motion=end-start;float length=motion.magnitude;if(length<.000001f)return true;
            // Source query mask 1: native world bit-1 geometry maps to Unity layer 0,
            // constructed source cover to 8. Excludes bullet-only 19/player-only 20.
            // Reverse cast retains source hit_back_faces=true without a global setting.
            return !Blocked(start,motion/length,length)&&!Blocked(end,-motion/length,length);
        }
        bool Blocked(Vector3 start,Vector3 direction,float distance)
        {
            foreach(var hit in Physics.RaycastAll(start,direction,distance,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore))
            {
                var c=hit.collider;if(c!=null&&c.enabled&&c.gameObject.activeInHierarchy&&c.transform!=actorRoot&&!c.transform.IsChildOf(actorRoot))return true;
            }
            return false;
        }
        void OnDisable(){ResetContact();}
        void OnDestroy(){if(visuals!=null)visuals.Dispose();}
        static string CanonicalMap(string id){if(id=="paintball")return "archive:battlefield-paintball-gods-pitdevil";if(id=="alpha"||id=="alfa")return "archive:alfa-aglae-souza";return id;}
        static Vector3 V(float[] v){if(v==null||v.Length!=3)throw new InvalidOperationException("health vector");return new Vector3(v[0],v[1],v[2]);}
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}static Vector3 U(Float3 v){return new Vector3(v.X,v.Y,v.Z);}
    }
#endif
}
