#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.Traversal
{
    public static class R25GrappleArt
    {
        public static void Build(Transform root)
        {
            var owner=root.gameObject.AddComponent<R25GrappleMaterials>();
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            owner.Body=new Material(shader);owner.Body.color=new Color(.12f,.16f,.20f);
            owner.Metal=new Material(shader);owner.Metal.color=new Color(.62f,.72f,.78f);
            owner.Charge=new Material(shader);owner.Charge.color=Color.cyan;
            owner.Charge.EnableKeyword("_EMISSION");owner.Charge.SetColor("_EmissionColor",Color.cyan*1.8f);
            Part(root,"Launcher body",PrimitiveType.Cube,new Vector3(0,.035f,.13f),new Vector3(.085f,.09f,.24f),owner.Body);
            Part(root,"One hand grip",PrimitiveType.Cube,new Vector3(0,-.055f,.025f),new Vector3(.055f,.14f,.07f),owner.Body);
            var drum=Part(root,"Cable drum",PrimitiveType.Cylinder,new Vector3(0,.02f,.13f),new Vector3(.13f,.065f,.13f),owner.Metal);drum.localRotation=Quaternion.Euler(0,0,90);
            Part(root,"Visible hook shaft",PrimitiveType.Cube,new Vector3(0,.04f,.275f),new Vector3(.018f,.018f,.09f),owner.Metal);
            for(int side=-1;side<=1;side+=2){var hook=Part(root,"Hook barb",PrimitiveType.Cube,new Vector3(side*.025f,.04f,.30f),new Vector3(.018f,.018f,.065f),owner.Metal);hook.localRotation=Quaternion.Euler(0,side*45,0);}
            Part(root,"Emissive charge strip",PrimitiveType.Cube,new Vector3(0,.084f,.13f),new Vector3(.022f,.006f,.14f),owner.Charge);
        }
        static Transform Part(Transform root,string name,PrimitiveType shape,Vector3 at,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.layer=root.gameObject.layer;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider);
            go.transform.SetParent(root,false);go.transform.localPosition=at;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        public static byte[] Icon()
        {
            var texture=new Texture2D(128,64,TextureFormat.RGBA32,false);var pixels=new Color32[128*64];
            for(int y=0;y<64;y++)for(int x=0;x<128;x++){
                bool body=x>=25&&x<=94&&y>=30&&y<=45;
                bool grip=x>=30&&x<=46&&y>=10&&y<30;
                bool hook=x>=94&&x<=117&&y>=35&&y<=40||x>=109&&x<=114&&y>=27&&y<=47;
                bool drum=(x-64)*(x-64)+(y-33)*(y-33)<144;
                bool strip=x>=35&&x<=87&&y>=43&&y<=47;
                pixels[y*128+x]=strip?new Color32(0,240,255,255):hook||drum?new Color32(186,211,225,255):body||grip?new Color32(110,135,158,255):new Color32(0,0,0,0);
            }
            texture.SetPixels32(pixels);texture.Apply();byte[] bytes=texture.EncodeToPNG();Object.Destroy(texture);return bytes;
        }
    }
    public sealed class R25GrappleMaterials:MonoBehaviour
    {
        public Material Body,Metal,Charge;
        void OnDestroy(){if(Body!=null)Destroy(Body);if(Metal!=null)Destroy(Metal);if(Charge!=null)Destroy(Charge);}
    }
}
#endif
