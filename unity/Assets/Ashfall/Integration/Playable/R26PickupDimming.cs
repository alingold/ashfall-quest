#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;using UnityEngine;
namespace Ashfall.Integration.Playable
{
    // MP-PICKUP: one shared "unavailable" look for world pickups the local player cannot
    // take right now. Applied only when the state changes: cached material clones keep
    // SRP batching (no property blocks) and nothing allocates while the state is steady.
    static class R26PickupDimming
    {
        static readonly Dictionary<Material,Material> dimmedBySource=new Dictionary<Material,Material>();
        static readonly Dictionary<Material,Material> sourceByDimmed=new Dictionary<Material,Material>();
        static readonly List<Renderer> renderers=new List<Renderer>();static readonly List<Material> slots=new List<Material>();
        static readonly int BaseColorId=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color"),RarityId=Shader.PropertyToID("_RarityColor"),EmissionId=Shader.PropertyToID("_EmissionColor");
        internal static void Apply(Component root,bool dimmed)
        {
            if(root==null)return;renderers.Clear();root.GetComponentsInChildren(true,renderers);
            for(int r=0;r<renderers.Count;r++)
            {
                var renderer=renderers[r];if(renderer==null||!(renderer is MeshRenderer||renderer is SkinnedMeshRenderer))continue;renderer.GetSharedMaterials(slots);bool changed=false;
                for(int i=0;i<slots.Count;i++){var source=slots[i];if(source==null)continue;var next=dimmed?Dimmed(source):Source(source);if(!ReferenceEquals(next,source)){slots[i]=next;changed=true;}}
                if(changed)renderer.sharedMaterials=slots.ToArray();
            }
            renderers.Clear();slots.Clear();
        }
        static Material Source(Material m){Material source;return sourceByDimmed.TryGetValue(m,out source)&&source!=null?source:m;}
        static Material Dimmed(Material m)
        {
            if(sourceByDimmed.ContainsKey(m))return m;Material d;if(dimmedBySource.TryGetValue(m,out d)&&d!=null)return d;
            d=new Material(m){name=m.name+" (unavailable)",hideFlags=HideFlags.DontSave};
            Grey(d,BaseColorId);Grey(d,ColorId);Grey(d,RarityId);if(d.HasProperty(EmissionId)){d.SetColor(EmissionId,Color.black);d.DisableKeyword("_EMISSION");}
            dimmedBySource[m]=d;sourceByDimmed[d]=m;return d;
        }
        // Mostly desaturated and at 40% brightness; alpha is kept.
        static void Grey(Material m,int id)
        {if(!m.HasProperty(id))return;Color c=m.GetColor(id);float l=c.r*.3f+c.g*.59f+c.b*.11f;m.SetColor(id,new Color((c.r*.25f+l*.75f)*.4f,(c.g*.25f+l*.75f)*.4f,(c.b*.25f+l*.75f)*.4f,c.a));}
    }
}
#endif
