#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.FlashSmoke
{
    internal static class R25ThrowableIcons
    {
        static byte[] flash,smoke;
        internal static byte[] Read(string kind)
        {
            if(kind=="flash"&&flash!=null)return flash;
            if(kind=="smoke"&&smoke!=null)return smoke;
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            var pixels=new Color32[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float dx=(x-31.5f)/30,dy=(y-31.5f)/30;
                float r=Mathf.Sqrt(dx*dx+dy*dy),theta=Mathf.Atan2(dy,dx);
                bool fill=kind=="flash"?r<.45f+.35f*Mathf.Pow(Mathf.Abs(Mathf.Cos(theta*6)),8):
                    dx*dx+(dy+.15f)*(dy+.15f)<.45f||((dx+.42f)*(dx+.42f)+dy*dy<.22f)||((dx-.42f)*(dx-.42f)+dy*dy<.22f);
                bool canister=Mathf.Abs(dx)<(kind=="flash"?.28f:.34f)&&Mathf.Abs(dy)<(kind=="flash"?.55f:.73f);bool band=canister&&Mathf.Abs(dy+.1f)<.15f;bool vents=kind=="flash"&&canister&&Mathf.Abs(dx)<.18f&&Mathf.Abs(Mathf.Sin(dy*20))>.65f;pixels[y*64+x]=band?(kind=="flash"?new Color32(255,193,35,255):new Color32(215,85,35,255)):vents?new Color32(255,225,130,255):canister?new Color32(185,202,190,255):fill?new Color32(110,125,135,100):new Color32(0,0,0,0);
            }
            texture.SetPixels32(pixels);texture.Apply();var bytes=texture.EncodeToPNG();Object.Destroy(texture);
            if(kind=="flash")flash=bytes;else smoke=bytes;return bytes;
        }
    }
}
#endif
