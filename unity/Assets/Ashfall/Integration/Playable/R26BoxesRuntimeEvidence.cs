#if UNITY_6000_0_OR_NEWER
using System;using System.IO;using UnityEngine;
namespace Ashfall.Integration.Playable
{
    // Opt-in PC evidence only. No work or object is created in normal play.
    public sealed class R26BoxesRuntimeEvidence:MonoBehaviour
    {
        string folder,map;float entered=-1;int capture;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
        {
            var args=Environment.GetCommandLineArgs();string folder=null,map=null;
            for(int i=0;i+1<args.Length;i++){if(args[i]=="-ashfallBoxesEvidence")folder=args[i+1];if(args[i]=="-ashfallNetBotMap")map=args[i+1];}
            if(String.IsNullOrEmpty(folder)||String.IsNullOrEmpty(map))return;Directory.CreateDirectory(folder);var go=new GameObject("BOXES runtime evidence");DontDestroyOnLoad(go);var probe=go.AddComponent<R26BoxesRuntimeEvidence>();probe.folder=folder;probe.map=map;
        }
        void Update()
        {
            if(capture>=3)return;var root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();
            if(root==null||!root.Ready||root.Map.MapId!=map||root.Lobby.Visible||root.SourceArchiveEntryPending||map.StartsWith("archive:",StringComparison.Ordinal)&&!root.ArchiveWorldReady)return;
            if(entered<0)entered=Time.realtimeSinceStartup;if(Time.realtimeSinceStartup-entered<capture*5+.5f)return;
            string stem=Path.Combine(folder,"spawn-"+capture);var feet=root.Rig.transform.position;Ashfall.Core.Float3 source;bool hasSource=root.TryArchiveWorldToSource(feet,out source);
            RaycastHit floor,ceiling;bool floorHit=Physics.Raycast(feet+Vector3.up*.3f,Vector3.down,out floor,3,1,QueryTriggerInteraction.Ignore),ceilingHit=Physics.Raycast(feet+Vector3.up*2,Vector3.up,out ceiling,1000,1,QueryTriggerInteraction.Ignore);
            File.WriteAllText(stem+".json","{\"mapId\":\""+map+"\",\"elapsed\":"+(Time.realtimeSinceStartup-entered).ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"feetWorld\":"+V(feet)+",\"sourceFeet\":"+(hasSource?V(new Vector3(source.X,source.Y,source.Z)):"null")+",\"floorGap\":"+(floorHit?(feet.y-floor.point.y).ToString(System.Globalization.CultureInfo.InvariantCulture):"null")+",\"ceilingRise\":"+(ceilingHit?ceiling.distance.ToString(System.Globalization.CultureInfo.InvariantCulture):"null")+",\"pods\":"+(root.TeamLaunch!=null&&root.TeamLaunch.Eligible?"true":"false")+"}\n");
            var camera=root.Rig.Head;var prior=camera.targetTexture;var active=RenderTexture.active;var target=RenderTexture.GetTemporary(1280,720,24);Texture2D pixels=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(stem+".png",pixels.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)Destroy(pixels);}capture++;
        }
        static string V(Vector3 p){return "["+p.x.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+p.y.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+p.z.ToString(System.Globalization.CultureInfo.InvariantCulture)+"]";}
    }
}
#endif
