#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        void BuildThemePanel(bool popup)
        {
            // The start screen keeps its floating buttons: a board here hid the lobby and the title wordmark.
            if(Page==R23UiPage.Ready||Page==R23UiPage.SpawnChoice||Page==R23UiPage.Weapons)return;
            // Only the card half needs backing; leave the full-size pedestal outfit open.
            if(Page==R23UiPage.Appearance)
            {
                Quad(panel,"Theme panel backdrop",new Vector3(-.55f,0,.004f),new Vector2(1.1f,1.375f),Hex(R25Theme.Panel));
                return;
            }
            if(!popup&&Page!=R23UiPage.Loadout)Quad(panel,"Theme panel backdrop",new Vector3(0,0,.004f),new Vector2(2.2f,1.375f),Hex(R25Theme.Panel));
            float width=popup?1.3f:Page==R23UiPage.Loadout?1.2f:2.2f;
            float top=popup?.74f:Page==R23UiPage.Loadout?(creativeCatalog?.79f:.38f):.6875f;
            float height=popup?.205f:R25Theme.HeaderPx*R25Theme.MetresPerPixel;
            Quad(panel,"Theme header backdrop",new Vector3(0,top-height*.5f,-.002f),new Vector2(width,height),Hex(R25Theme.Header)).GetComponent<Renderer>().sortingOrder=1;
            Quad(panel,"Theme header hairline",new Vector3(0,top-height,-.003f),new Vector2(width,2*R25Theme.MetresPerPixel),Hex(R25Theme.Stroke));
            float x=popup?0:-width*.5f+.172f;
            Quad(panel,"Theme header accent",new Vector3(x,top-height,-.004f),new Vector2(120*R25Theme.MetresPerPixel,4*R25Theme.MetresPerPixel),Hex(R25Theme.Ember));
        }
        Renderer[] ThemeFrameAt(Transform parent,Vector3 position,Vector2 size)
        {var anchor=Child(parent,"Theme thumbnail frame");anchor.localPosition=position;return ThemeFrame(anchor,size,Hex(R25Theme.Stroke));}
        Renderer[] ThemeFrame(Transform parent,Vector2 size,Color color)
        {
            float t=2*R25Theme.MetresPerPixel;
            var result=new Renderer[4];
            for(int i=0;i<4;i++)
            {
                bool side=i<2;float sign=i%2==0?-1:1;
                var pos=side?new Vector3(sign*size.x*.5f,0,-.006f):new Vector3(0,sign*size.y*.5f,-.006f);
                var frame=Quad(parent,"Theme card frame",pos,side?new Vector2(t,size.y):new Vector2(size.x,t),Color.white);
                result[i]=frame.GetComponent<Renderer>();result[i].sortingOrder=13;SetRendererColor(result[i],color);
            }
            return result;
        }
    }
}
#endif
