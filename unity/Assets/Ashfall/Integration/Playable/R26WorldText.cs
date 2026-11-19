#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfall.Integration.Playable
{
    public static class R26WorldText
    {
        static readonly Dictionary<Font,Material> inks=new Dictionary<Font,Material>();

        public static void Bind(TextMesh label)
        {
            Font font=label.font;
            if(font==null){font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.font=font;}
            Material ink;
            if(!inks.TryGetValue(font,out ink)){
                ink=new Material(Resources.Load<Shader>("R23VrUi")){name="World text "+font.name,renderQueue=3050};
                ink.SetFloat("_Font",1);
                ink.SetFloat("_ZTest",(float)CompareFunction.LessEqual);
                ink.mainTexture=font.material.mainTexture;
                if(inks.Count==0)Font.textureRebuilt+=RefreshAtlas;
                inks.Add(font,ink);
            }
            label.GetComponent<MeshRenderer>().sharedMaterial=ink;
        }

        static void RefreshAtlas(Font font)
        {
            Material ink;
            if(inks.TryGetValue(font,out ink))ink.mainTexture=font.material.mainTexture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMaterials()
        {
            Font.textureRebuilt-=RefreshAtlas;
            foreach(var ink in inks.Values)if(ink!=null)Object.Destroy(ink);
            inks.Clear();
        }
    }
}
#endif
