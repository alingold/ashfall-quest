#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Gallery
{
    // Read the exact production fit payload; optional new profiles never fall
    // back to a rifle's muzzle/magazine or display an invented substitute.
    [Serializable]internal sealed class R23GalleryFit
    {
        public string id="";public float modelScale=0,reloadScale=0,opticScale=0,opticMountHeight=0;
        public bool chargingMirrorX=false,heldMagazineGunBasis=false,topFeed=false,hideSeatedMagazine=false;
        public float[] muzzle=null,magazineHome=null,weaponGripOrigin=null;
        public static IEnumerator Load(string id,float expectedScale,Action<R23GalleryFit> accept)
        {
            string url=Application.streamingAssetsPath+"/PlayableR23/HandsWeapons/"+id+"-fit.json";if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;
            using(var request=UnityWebRequest.Get(url))
            {
                request.timeout=15;yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success)throw new InvalidOperationException("Source gallery fit "+id+": "+request.error);
                var fit=JsonUtility.FromJson<R23GalleryFit>(request.downloadHandler.text);
                if(fit==null||fit.id!=id||!Finite(fit.modelScale)||Mathf.Abs(fit.modelScale-expectedScale)>.00001f||!Finite(fit.reloadScale)||fit.reloadScale<=0||!Finite(fit.opticScale)||fit.opticScale<0||!Finite(fit.opticMountHeight))throw new InvalidOperationException("Source gallery fit identity/scale mismatch: "+id);
                Vector(fit.muzzle);Vector(fit.magazineHome);Vector(fit.weaponGripOrigin);accept(fit);
            }
        }
        public static Vector3 Vector(float[] v){if(v==null||v.Length!=3||!Finite(v[0])||!Finite(v[1])||!Finite(v[2]))throw new InvalidOperationException("Finite original three-component gallery fit required");return new Vector3(v[0],v[1],v[2]);}
        static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    }
}
#endif
