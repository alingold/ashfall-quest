#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.JumpPads
{
    public sealed class R23JumpPads:MonoBehaviour
    {
        Transform actorRoot,mapRoot;Func<Vector3,float,bool> positiveLaunch;Manifest data;R23PadContacts contacts;double clock;
        R23SourcePad[] authoredPads=new R23SourcePad[0];AudioSource launchVoice;AudioClip launchClip;float sfx=1;public bool AudioReady{get{return launchClip!=null;}}public string AudioFailure{get;private set;}
        public bool Ready {get;private set;}public bool Loading {get;private set;}
        public string MapId {get;private set;}public string Failure {get;private set;}
        public int PadCount {get;private set;}public int TiltedCount {get;private set;}public int LaunchCount {get;private set;}
        public string LastLaunchedPadId {get{return contacts==null?null:contacts.LastPadId;}}
        public R23SourcePad[] AuthoredPads {get{return (R23SourcePad[])authoredPads.Clone();}}
        public void Initialize(string mapId,Transform playerRoot,Transform loadedMapRoot,Func<Vector3,float,bool> tryLaunch)
        {
            if(actorRoot!=null)throw new InvalidOperationException("jump pads already initialized");
            if(playerRoot==null||loadedMapRoot==null||tryLaunch==null)throw new ArgumentException("accepted actor, map and real launch owner required");
            actorRoot=playerRoot;mapRoot=loadedMapRoot;positiveLaunch=tryLaunch;MapId=mapId;var voice=new GameObject("Accepted jump-pad launch cue");voice.transform.SetParent(transform,false);launchVoice=voice.AddComponent<AudioSource>();launchVoice.playOnAwake=false;launchVoice.spatialBlend=1;launchVoice.dopplerLevel=0;launchVoice.minDistance=1;launchVoice.maxDistance=40;launchVoice.volume=Mathf.Pow(10,-5/20f)*sfx;StartCoroutine(Load());
        }
        IEnumerator Load()
        {
            Loading=true;string url=Application.streamingAssetsPath+"/PlayableR23/JumpPads/pads.json";if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;
            using(var request=UnityWebRequest.Get(url))
            {
                request.timeout=30;yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){Failure=request.error;Loading=false;yield break;}
                try{data=JsonUtility.FromJson<Manifest>(request.downloadHandler.text);if(data==null||data.version!=1||data.maps==null)throw new InvalidOperationException("source pad manifest");SetMap(MapId);}catch(Exception error){Failure=error.Message;Ready=false;}
            }
            string audio=Application.streamingAssetsPath+"/PlayableR23/JumpPads/pad_launch.wav";if(!audio.Contains("://"))audio=new Uri(audio).AbsoluteUri;
            using(var req=UnityWebRequestMultimedia.GetAudioClip(audio,AudioType.WAV)){yield return req.SendWebRequest();if(req.result==UnityWebRequest.Result.Success){launchClip=DownloadHandlerAudioClip.GetContent(req);launchVoice.clip=launchClip;}else AudioFailure=req.error;}
            Loading=false;
        }
        public bool SetMap(string mapId)
        {
            if(launchVoice!=null)launchVoice.Stop();MapId=mapId;Ready=false;contacts=null;authoredPads=new R23SourcePad[0];PadCount=TiltedCount=LaunchCount=0;clock=0;Failure=null;
            if(data==null)return false;
            foreach(var map in data.maps)if(map.mapId==mapId)
            {
                var pads=new R23SourcePad[map.pads.Length];float scale=Mathf.Abs(mapRoot.lossyScale.x);
                for(int i=0;i<pads.Length;i++)
                {var row=map.pads[i];Vector3 center=mapRoot.TransformPoint(V(row.center)),normal=mapRoot.TransformDirection(V(row.normal));pads[i]=new R23SourcePad(mapId+"/pad/"+row.sourceIndex,F(center),F(normal),row.radius*scale,row.speed,row.tilted);if(row.tilted)TiltedCount++;}
                authoredPads=pads;contacts=new R23PadContacts(pads);PadCount=pads.Length;Ready=true;return true;
            }
            Failure="No source jump-pad data for "+mapId;return false;
        }
        #if UNITY_EDITOR
        public bool CaptureContactDiagnostics;
        public string ContactDiagnostics {get;private set;}
        public int PositiveLaunchProbeCalls {get;private set;}
        #endif
        public void ResetContacts(){if(contacts!=null)contacts.ResetContacts();}
        public bool Step(Vector3 actualFeet,float deltaSeconds,bool inputReady,bool climbing,bool mantling,bool ghost=false)
        {
            if(!Ready||!isActiveAndEnabled||Single.IsNaN(deltaSeconds)||Single.IsInfinity(deltaSeconds)||deltaSeconds<=0||deltaSeconds>.1f)return false;
            #if UNITY_EDITOR
            if(CaptureContactDiagnostics){ContactDiagnostics="feet="+actualFeet.ToString("F6")+" inputReady="+inputReady+" climbing="+climbing+" mantling="+mantling+" ghost="+ghost;PositiveLaunchProbeCalls=0;}
            #endif
            clock+=deltaSeconds;Physics.SyncTransforms();
            bool launched=contacts.TryLaunch(F(actualFeet),clock,inputReady,climbing,mantling,ghost,ClearContact,delegate(Float3 normal,float speed){
            #if UNITY_EDITOR
            if(CaptureContactDiagnostics){PositiveLaunchProbeCalls++;bool accepted=positiveLaunch(U(normal),speed);ContactDiagnostics+=" positiveLaunch="+accepted+" speed="+speed+" normal="+U(normal).ToString("F6");return accepted;}
            #endif
            return positiveLaunch(U(normal),speed);});
            if(launched){LaunchCount++;launchVoice.transform.position=U(contacts.LastPadCenter);if(launchClip!=null)launchVoice.PlayOneShot(launchClip);}return launched;
        }
        bool ClearContact(Float3 from,Float3 to)
        {
            Vector3 start=U(from),motion=U(to)-start;float length=motion.magnitude;if(length==0)return true;
            #if UNITY_EDITOR
            if(CaptureContactDiagnostics)ContactDiagnostics+=" clearFrom="+start.ToString("F6")+" clearTo="+U(to).ToString("F6")+" clearLength="+length;
            #endif
            foreach(var hit in Physics.RaycastAll(start,motion/length,length,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
            {var c=hit.collider;
            #if UNITY_EDITOR
            if(CaptureContactDiagnostics)ContactDiagnostics+=" hit="+c.name+" type="+c.GetType().Name+" layer="+c.gameObject.layer+" point="+hit.point.ToString("F6")+" normal="+hit.normal.ToString("F6")+" distance="+hit.distance+" actorChild="+c.transform.IsChildOf(actorRoot)+" mapChild="+c.transform.IsChildOf(mapRoot);
            #endif
            if(!c.transform.IsChildOf(actorRoot)&&(c.transform.IsChildOf(mapRoot)||c.gameObject.layer==8))return false;}
            return true;
        }
        public void SetSfxVolume(float value){if(Single.IsNaN(value)||Single.IsInfinity(value))throw new ArgumentOutOfRangeException();sfx=Mathf.Clamp01(value);if(launchVoice!=null)launchVoice.volume=Mathf.Pow(10,-5/20f)*sfx;}
        void OnDisable(){ResetContacts();if(launchVoice!=null)launchVoice.Stop();}
        void OnDestroy(){if(launchClip!=null)Destroy(launchClip);}
        [Serializable]sealed class Manifest {public int version=0;public Map[] maps=null;}
        [Serializable]sealed class Map {public string mapId="",sourceSha256="";public Row[] pads=null;}
        [Serializable]sealed class Row {public int sourceIndex=0;public float[] center=null,normal=null;public double radius=0;public float speed=0;public bool tilted=false;}
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}static Vector3 U(Float3 v){return new Vector3(v.X,v.Y,v.Z);}
        static Vector3 V(float[] v){if(v==null||v.Length!=3)throw new InvalidOperationException("pad vector");return new Vector3(v[0],v[1],v[2]);}
    }
}
#endif
