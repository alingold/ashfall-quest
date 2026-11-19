#if UNITY_6000_0_OR_NEWER
using System;using System.Globalization;using UnityEngine;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.UI;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R25NetBot
    {
        bool warmupOpened,warmupStarted,showedWarmupRules;float warmupBegan=-1,warmupRulesAt;int warmupCaptures;
        // Explicit evidence option: use the normal map entry first, then the normal START MATCH route.
        void LateUpdate()
        {
            string option=Arg("-ashfallNetBotWarmupSeconds");if(!HitregEnabled||option.Length==0||mode!="host"||closed||root==null||!root.Ready||log==null)return;
            float seconds;if(!Single.TryParse(option,NumberStyles.Float,CultureInfo.InvariantCulture,out seconds)||Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<12){Finish("invalid-warmup-seconds-minimum-12");return;}
            requestedMap=true;var lobby=root.LanSession.Lobby;if(lobby==null||lobby.State!=R24LanLobbyState.Hosting||lobby.MemberCount<2)return;
            if(!warmupOpened){warmupOpened=root.LanSession.RouteMapEntry(root.Map.MapId);Write("warmup-map-request",root.Map.MapId);return;}
            var map=lobby.SelectedMap;if(map==null||!map.Active||map.Cancelled||root.LanSession.MapEntryBlocked)return;
            float now=Time.realtimeSinceStartup;
            if(map.Rules==null){
                if(warmupBegan<0)warmupBegan=now;float elapsed=now-warmupBegan;
                if(warmupCaptures==0&&elapsed>=2){Capture(200);WarmupEvidence("hud");warmupCaptures++;}
                if(warmupCaptures==1&&elapsed>=3&&root.UI.WarmupShotCaption.Length>0){Capture(201);WarmupEvidence("shot");warmupCaptures++;}
                if(!showedWarmupRules&&elapsed>=8){showedWarmupRules=true;warmupRulesAt=now;root.UI.OpenMatchRules();}
                if(warmupCaptures==2&&showedWarmupRules&&now-warmupRulesAt>=.25f){Capture(202);WarmupEvidence("rules");root.UI.SetPlaying();warmupCaptures++;}
                if(!warmupStarted&&elapsed>=seconds){warmupStarted=root.LanSession.RouteRoundRequest(HitregRoundRules());Write("warmup-start-match","after="+elapsed.ToString("F2",CultureInfo.InvariantCulture));}
            }else if(warmupCaptures==3&&lobby.RoundElapsed>=4){Capture(203);WarmupEvidence("match");warmupCaptures++;}
        }
        void WarmupEvidence(string stage)
        {Write("warmup-ui",stage+" epoch="+root.LanSession.Lobby.Epoch+" state="+root.LanSession.Room.Stage+" caption="+root.UI.WarmupCaption+" visible="+root.UI.WarmupHudVisible+" shot="+root.UI.WarmupShotCaption+" remoteAim="+root.Gameplay.RemoteAimCount+" page="+root.UI.Page);}
    }
}
#endif
