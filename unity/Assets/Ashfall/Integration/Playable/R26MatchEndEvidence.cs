#if UNITY_6000_0_OR_NEWER
using System;using System.IO;using UnityEngine;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.UI;
namespace Ashfall.Integration.Playable
{
    // Explicit opt-in rig driver: starts a short real round, observes the normal host result path.
    public sealed class R26MatchEndEvidence:MonoBehaviour
    {
        R23PlayableLoop root;bool requested,ended,warmup;float began;int captures;string directory;
        public void Initialize(R23PlayableLoop owner){root=owner;directory=Path.GetDirectoryName(R25NetBot.Arg("-ashfallNetBotLog"));}
        void LateUpdate()
        {
            var lobby=root.LanSession==null?null:root.LanSession.Lobby;if(lobby==null||!root.Ready)return;var map=lobby.SelectedMap;
            if(!requested&&lobby.State==R24LanLobbyState.Hosting&&map!=null&&map.Active&&map.Rules!=null&&root.MatchActive&&root.MatchClock>1){requested=true;root.LanSession.RouteRoundRequest(map.Rules.With("mode","respawn").With("time_limit",15).With("rounds",1).With("score_limit",0));Debug.Log("R26 MATCHEND evidence short round requested");}
            var celebration=root.GetComponent<R26MatchEndCelebration>();
            if(celebration!=null&&celebration.Visible){if(!ended){ended=true;began=Time.unscaledTime;}float elapsed=Time.unscaledTime-began;if(captures==0&&elapsed>=.2f||captures==1&&elapsed>=5||captures==2&&elapsed>=9){Capture(captures++);}}
            if(ended&&!warmup&&map!=null&&map.Active&&!map.Cancelled&&map.Rules==null&&!root.LanSession.MapEntryBlocked){warmup=true;Debug.Log("R26 MATCHEND evidence warmup elapsed="+(Time.unscaledTime-began).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" root="+root.Rig.transform.position);Capture(3);}
        }
        void Capture(int index){if(String.IsNullOrEmpty(directory))return;var camera=root.Rig.Head;var previous=camera.targetTexture;var active=RenderTexture.active;var target=RenderTexture.GetTemporary(960,540,24);Texture2D pixels=null;try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(960,540,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(directory,R25NetBot.Arg("-ashfallNetBot")+"-matchend-"+index+".png"),pixels.EncodeToPNG());}finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)Destroy(pixels);}}
    }
}
#endif
