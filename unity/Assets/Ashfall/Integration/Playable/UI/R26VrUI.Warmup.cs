#if UNITY_6000_0_OR_NEWER
using UnityEngine;using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        bool warmupExploration;
        Transform warmupHud;
        public bool WarmupExploration{get{return warmupExploration;}}
        public string WarmupCaption{get{return warmupExploration?R26WarmupFeedback.HostLine:"";}}
        public bool WarmupHudVisible{get{return warmupHud!=null&&warmupHud.gameObject.activeInHierarchy;}}
        public string WarmupShotCaption{get{return "";}}
        public void SetWarmupExploration(bool value)
        {
            bool changed=warmupExploration!=value;warmupExploration=value;
            if(value&&Page==R23UiPage.Playing&&hud!=null&&warmupHud==null)BuildWarmupHud();
            RefreshWarmupHud();if(changed)SetLocalMatchStatus(localMatchStatus);
            if(value&&Page==R23UiPage.MatchRules&&matchStatusText!=null)matchStatusText.text=R26WarmupFeedback.HostLine+"\n"+RoomLine();
        }
        public void NotifyWarmupShot(){}
        void BuildWarmupHud()
        {
            // HUD19: small host-starts-match line just above the health/shield bars (bars sit at y -.456/-.433; shield top is -.4235). No shot or damage-off text.
            warmupHud=Child(hud,"Warmup host line");
            Quad(warmupHud,"Warmup host line backing",new Vector3(0,-.4105f,.803f),new Vector2(.29f,.019f),Hex(R25Theme.HudBacking));
            var caption=Text(warmupHud,R26WarmupFeedback.HostLine,new Vector3(0,-.4105f,.797f),.0105f,.28f,Hex(R25Theme.Text));caption.fontSize=32;CenterHudGlyphs(caption,-.4105f);
            RefreshWarmupHud();
        }
        void RefreshWarmupHud()
        {
            if(warmupHud==null)return;warmupHud.gameObject.SetActive(warmupExploration&&Page==R23UiPage.Playing);
        }
    }
}
#endif
