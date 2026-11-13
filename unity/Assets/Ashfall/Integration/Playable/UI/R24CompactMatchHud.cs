#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Integration.Playable.WorldScores;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        R24CompactMatchHudState compactMatchState;int compactOwnTeam;
        Transform compactMatchHud,compactLeftFill,compactRightFill,compactOwnPlate,compactOtherPlate;
        TextMesh compactLeftText,compactRightText,compactCapText,compactClockText,compactPersonalText;
        Label compactClockLabel;
        public bool CompactMatchHudVisible {get{return compactMatchHud!=null&&compactMatchHud.gameObject.activeInHierarchy;}}
        public Transform CompactMatchHudRoot {get{return compactMatchHud;}}
        public string CompactMatchClock {get{return compactClockText==null?"":compactClockText.text;}}
        public string CompactOwnScore {get{return compactLeftText==null?"":compactLeftText.text;}}
        public string CompactOtherScore {get{return compactRightText==null?"":compactRightText.text;}}
        public string CompactPersonalScore {get{return compactPersonalText==null?"":compactPersonalText.text;}}
        public void SetCompactMatchState(R24WorldScoreState state,string ownActor,int ownTeam)
        {compactOwnTeam=ownTeam;compactMatchState=new R24CompactMatchHudState(state,ownActor,ownTeam);RefreshCompactMatchHud();}
        void ClearCompactMatchHudReferences()
        {compactMatchHud=compactLeftFill=compactRightFill=null;compactLeftText=compactRightText=compactCapText=compactClockText=compactPersonalText=null;compactClockLabel=null;}
        TextMesh CompactLabel(string caption,Vector3 at,float em,float width)
        {var text=Text(compactMatchHud,caption,at,em*.72f,width,Hex(R25Theme.Text));text.fontSize=32;CenterHudGlyphs(text,at.y);return text;}
        void BuildCompactMatchHud()
        {
            // Coplanar overlay geometry keeps the connecting bar centred in perspective.
            compactMatchHud=Child(hud,"R6 compact match score and timer");
            Quad(compactMatchHud,"Source score backdrop",new Vector3(-.02f,-.389f,.800f),new Vector2(.29f,.020f),Hex(R25Theme.HudBacking));
            compactLeftFill=Quad(compactMatchHud,"Own score remaining",new Vector3(-.0835f,-.389f,.800f),new Vector2(.087f,.020f),Hex(R25Theme.Team2));
            compactRightFill=Quad(compactMatchHud,"Other score remaining",new Vector3(.0435f,-.389f,.800f),new Vector2(.087f,.020f),Hex(R25Theme.Team1));
            compactOwnPlate=Quad(compactMatchHud,"Own score plate",new Vector3(-.146f,-.389f,.800f),new Vector2(.038f,.020f),Hex(R25Theme.Team2Dim));
            compactOtherPlate=Quad(compactMatchHud,"Other score plate",new Vector3(.106f,-.389f,.800f),new Vector2(.038f,.020f),Hex(R25Theme.Team1Dim));
            Quad(compactMatchHud,"Source cap plate",new Vector3(-.02f,-.389f,.800f),new Vector2(.040f,.020f),Hex(R25Theme.HudBacking));
            compactLeftText=CompactLabel("",new Vector3(-.146f,-.389f,.800f),.012f,.038f);
            compactRightText=CompactLabel("",new Vector3(.106f,-.389f,.800f),.012f,.038f);
            compactCapText=CompactLabel("",new Vector3(-.02f,-.389f,.800f),.012f,.040f);
            compactClockText=CompactLabel("",new Vector3(-.02f,-.367f,.797f),.0105f,.29f);compactClockLabel=labels[labels.Count-1];
            compactPersonalText=CompactLabel("",new Vector3(-.02f,-.4185f,.797f),.0085f,.29f);compactPersonalText.gameObject.SetActive(false);
            RefreshCompactMatchHud();
        }
        void RefreshCompactMatchHud()
        {
            if(compactMatchHud==null)return;bool show=compactMatchState!=null&&compactMatchState.Visible&&Page==R23UiPage.Playing;compactMatchHud.gameObject.SetActive(show);if(!show)return;
            SetRendererColor(compactLeftFill.GetComponent<Renderer>(),Hex(compactOwnTeam==1?R25Theme.Team1:R25Theme.Team2));SetRendererColor(compactRightFill.GetComponent<Renderer>(),Hex(compactOwnTeam==1?R25Theme.Team2:R25Theme.Team1));
            SetRendererColor(compactOwnPlate.GetComponent<Renderer>(),Hex(compactOwnTeam==1?R25Theme.Team1Dim:R25Theme.Team2Dim));SetRendererColor(compactOtherPlate.GetComponent<Renderer>(),Hex(compactOwnTeam==1?R25Theme.Team2Dim:R25Theme.Team1Dim));
            var s=compactMatchState;compactLeftText.text=s.Left;compactRightText.text=s.Right;compactCapText.text=s.Cap;compactClockText.text=string.IsNullOrEmpty(s.Clock)?s.Personal:s.Clock+"   "+s.Personal;compactPersonalText.text=s.Personal;FitText(compactClockLabel);
            compactLeftFill.localScale=new Vector3(Mathf.Max(.0001f,s.LeftRatio),1,1);compactRightFill.localScale=new Vector3(Mathf.Max(.0001f,s.RightRatio),1,1);
            var left=compactLeftFill.localPosition;left.x=-.127f+.0435f*s.LeftRatio;compactLeftFill.localPosition=left;
            var right=compactRightFill.localPosition;right.x=.087f-.0435f*s.RightRatio;compactRightFill.localPosition=right;
        }
    }
}
#endif
