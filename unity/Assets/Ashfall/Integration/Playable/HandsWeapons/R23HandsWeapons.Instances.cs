#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        readonly Material[] groundRarity=new Material[5];
        static readonly string[] RarityColors={"ffffff","4b9bff","bd69ff","ffc542","ff415c"};
        void ApplyGroundRarity(Transform prop,int stars){
            if(stars<1||stars>5)throw new ArgumentOutOfRangeException("stars");int index=stars-1;
            if(groundRarity[index]==null){var shader=Resources.Load<Shader>("R23GroundRarity");if(shader==null)throw new InvalidOperationException("Source ground rarity shader missing");var material=new Material(shader);Color color;ColorUtility.TryParseHtmlString("#"+RarityColors[index],out color);material.SetColor("_RarityColor",color);groundRarity[index]=material;}
            // Subtle rarity tint over the existing mesh; broad edge emphasis
            // replaces the harsh additive outline. Pickup bounds remain exact.
            foreach(var renderer in prop.GetComponentsInChildren<MeshRenderer>(true)){
                var original=renderer.GetComponent<MeshFilter>();if(original==null||original.sharedMesh==null)continue;
                var overlay=new GameObject("Source rarity rim");overlay.layer=renderer.gameObject.layer;overlay.transform.SetParent(renderer.transform,false);overlay.AddComponent<MeshFilter>().sharedMesh=original.sharedMesh;var rim=overlay.AddComponent<MeshRenderer>();rim.sharedMaterial=groundRarity[index];rim.shadowCastingMode=ShadowCastingMode.Off;rim.receiveShadows=false;
            }
        }
        void DisposeGroundRarity(){foreach(var material in groundRarity)if(material!=null)Destroy(material);}
    }
}
#endif
