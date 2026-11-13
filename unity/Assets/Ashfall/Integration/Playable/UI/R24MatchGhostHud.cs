#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        Transform ghostLifeHud;
        TextMesh ghostLifeHudText;
        Label ghostLifeHudLabel;
        public bool IsLocalMatchGhost {get{return R24MatchLifeText.IsGhost(localMatchLife,localMatchResults);}}
        public bool GhostHudVisible {get{return IsLocalMatchGhost&&Page==R23UiPage.Playing&&ghostLifeHud!=null&&ghostLifeHud.gameObject.activeInHierarchy;}}
        public Transform GhostHudRoot {get{return ghostLifeHud;}}
        void ClearLocalMatchGhostHudReferences(){ghostLifeHud=null;ghostLifeHudText=null;ghostLifeHudLabel=null;}
        void BuildLocalMatchGhostHud()
        {
            // Source match_hud_r3.gd:33-40, all in camera-local metres. Godot
            // negative forward Z is reflected exactly once into Unity +Z.
            ghostLifeHud=Child(hud,"R6 source compact local ghost notice");
            Quad(ghostLifeHud,"Source ghost life panel",new Vector3(R24MatchLifeText.PanelX,R24MatchLifeText.PanelY,R24MatchLifeText.PanelUnityZ),new Vector2(R24MatchLifeText.PanelWidth,R24MatchLifeText.PanelHeight),Hex(R25Theme.HudBacking));
            // Text() takes capital height (.72 em), while source Label3D height
            // is the full em: pixel_size=.0095/32, font_size=32. Same original
            // Godot fallback glyph bytes, via the existing isolated UI font clone.
            ghostLifeHudText=Text(ghostLifeHud,"",new Vector3(R24MatchLifeText.PanelX,R24MatchLifeText.PanelY,R24MatchLifeText.TextUnityZ),R24MatchLifeText.TextEmHeight*.72f,R24MatchLifeText.PanelWidth,Hex(R25Theme.Text));
            ghostLifeHudText.fontSize=R24MatchLifeText.SourceFontSize;
            ghostLifeHudLabel=labels[labels.Count-1];RefreshLocalMatchGhostHud();
        }
        void RefreshLocalMatchGhostHud()
        {
            if(ghostLifeHud==null)return;
            bool show=IsLocalMatchGhost&&Page==R23UiPage.Playing;ghostLifeHud.gameObject.SetActive(show);
            if(!show)return;ghostLifeHudText.text=LocalMatchLifeCaption;FitText(ghostLifeHudLabel);
        }
    }
}
#endif
