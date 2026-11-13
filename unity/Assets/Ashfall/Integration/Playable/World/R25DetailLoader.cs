#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Ashfall.Integration.Playable.MapStyles;
namespace Ashfall.Integration.Playable.World
{
    public static class R25DetailLoader
    {
        static bool attempted, loading, warned;
        static R25DetailRules rules;
        static readonly Dictionary<string,Texture2D> maps=new Dictionary<string,Texture2D>();
        static void Warn() { if(!warned){warned=true;Debug.LogWarning("Detail layer unavailable or invalid; using original surfaces.");} }
        public static IEnumerator Load()
        {
            if(!Expansion.R25Features.DetailLayer)yield break;
            while(loading)yield return null;
            if(attempted)yield break;
            loading=true;
            try
            {
                R25DetailRules parsed=null;
                using(var request=UnityWebRequest.Get(R25StyleLoader.Url("Styles/detail/detail.json")))
                {
                    request.timeout=30;yield return request.SendWebRequest();
                    if(request.result==UnityWebRequest.Result.Success)parsed=R25DetailRules.Parse(request.downloadHandler.text);
                }
                if(parsed==null){Warn();yield break;}
                foreach(string name in R25DetailRules.Maps)
                {
                    Texture2D texture=null;
                    using(var request=UnityWebRequest.Get(R25StyleLoader.Url("Styles/detail/"+name)))
                    {
                        request.timeout=30;yield return request.SendWebRequest();
                        if(request.result==UnityWebRequest.Result.Success)
                        {
                            try
                            {
                                texture=new Texture2D(2,2,TextureFormat.RGBA32,true,true);
                                texture.name="Detail "+name;
                                if(!ImageConversion.LoadImage(texture,request.downloadHandler.data,true))throw new FormatException("Detail image");
                                texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=4;
                            }
                            catch(Exception){if(texture!=null)UnityEngine.Object.Destroy(texture);texture=null;}
                        }
                    }
                    if(texture==null){Warn();yield break;}
                    maps.Add(name,texture);
                }
                rules=parsed;
            }
            finally
            {
                if(rules==null){foreach(var texture in maps.Values)UnityEngine.Object.Destroy(texture);maps.Clear();}
                attempted=true;loading=false;
            }
        }
        public static void Bind(Material material,string albedo,bool ground)
        {
            if(!Expansion.R25Features.DetailLayer || rules==null || material==null || !material.HasProperty("_R25DetailStrength"))return;
            if(material.HasProperty("_Cutoff") && material.GetFloat("_Cutoff")>0)return;
            var choice=ground?rules.Ground(albedo):rules.Select(albedo);
            if(choice==null||choice.Map==null||choice.Strength<=0)return;
            material.SetTexture("_R25DetailMap",maps[choice.Map]);
            material.SetFloat("_R25DetailMetres",choice.Metres);
            material.SetFloat("_R25DetailStrength",choice.Strength);
        }
    }
}
#endif
