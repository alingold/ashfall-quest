#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Ashfall.Integration.Playable.Appearance;
namespace Ashfall.Integration.Playable.Match
{
    // ghost_appearance_r13: private surface overrides, never shared art mutation.
    // Only the host-supplied selected body and articulated-glove roots are touched.
    public sealed class R24GhostAppearance:MonoBehaviour
    {
        sealed class Record
        {public Renderer View;public Material[] Original,Replacement;public ShadowCastingMode Shadow;}
        readonly List<Record> records=new List<Record>();
        readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
        readonly List<Renderer> scratch=new List<Renderer>();
        readonly Transform[] roots=new Transform[3];Shader shader;Material defaultMaterial;
        public bool Active{get;private set;}
        public int SurfaceCount{get;private set;}
        public int RendererCount{get{return records.Count;}}
        public string Failure{get;private set;}
        public void SetRoots(Transform body,Transform leftGlove,Transform rightGlove)
        {
            if(roots[0]==body&&roots[1]==leftGlove&&roots[2]==rightGlove)return;
            bool resume=Active;Restore();roots[0]=body;roots[1]=leftGlove;roots[2]=rightGlove;
            if(resume)SetEnabled(true);
        }
        public bool SetEnabled(bool value)
        {
            if(value==Active)return true;
            if(!value){Restore();return true;}
            if(shader==null)shader=Resources.Load<Shader>("R24SourceGhost");
            if(shader==null){Failure="Source R13 ghost shader missing";return false;}
            Failure=null;Active=true;
            try
            {
                for(int i=0;i<roots.Length;i++)
                {
                    if(roots[i]==null)continue;scratch.Clear();roots[i].GetComponentsInChildren<Renderer>(true,scratch);
                    foreach(var view in scratch)
                    {
                        if(!(view is MeshRenderer)&&!(view is SkinnedMeshRenderer))continue;
                        bool duplicate=false;foreach(var record in records)if(record.View==view){duplicate=true;break;}if(duplicate)continue;
                        Material[] originals=view.sharedMaterials;var replacements=new Material[originals.Length];
                        for(int s=0;s<originals.Length;s++)replacements[s]=GhostMaterial(originals[s]);
                        records.Add(new Record{View=view,Original=originals,Replacement=replacements,Shadow=view.shadowCastingMode});
                        view.sharedMaterials=replacements;view.shadowCastingMode=ShadowCastingMode.Off;SurfaceCount+=originals.Length;
                    }
                }
                return true;
            }
            catch(Exception error){Restore();Failure=error.Message;return false;}
        }
        Material GhostMaterial(Material original)
        {
            Material result;
            if(original==null){if(defaultMaterial!=null)return defaultMaterial;result=NewMaterial(null);defaultMaterial=result;return result;}
            if(materials.TryGetValue(original,out result))return result;
            result=NewMaterial(original);materials.Add(original,result);return result;
        }
        Material NewMaterial(Material original)
        {
            var result=new Material(shader);result.name="Source R13 ghost / "+(original==null?"default":original.name);result.renderQueue=3012;
            float sourceDefault=R23AppearanceColorDomain.LinearRgb(.65f);
            Vector4 tint=new Vector4(sourceDefault,sourceDefault,sourceDefault,1);Texture texture=null;Vector2 scale=Vector2.one,offset=Vector2.zero;
            if(original!=null)
            {
                string color=original.HasProperty("_BaseColor")?"_BaseColor":original.HasProperty("_Color")?"_Color":null;
                if(color!=null)
                {
                    tint=original.GetVector(color);
                    if(QualitySettings.activeColorSpace==ColorSpace.Gamma)
                    {tint.x=R23AppearanceColorDomain.LinearRgb(Mathf.Max(0,tint.x));tint.y=R23AppearanceColorDomain.LinearRgb(Mathf.Max(0,tint.y));tint.z=R23AppearanceColorDomain.LinearRgb(Mathf.Max(0,tint.z));}
                }
                string map=original.HasProperty("_BaseMap")?"_BaseMap":original.HasProperty("_MainTex")?"_MainTex":null;
                if(map!=null){texture=original.GetTexture(map);scale=original.GetTextureScale(map);offset=original.GetTextureOffset(map);}
            }
            result.SetVector("_SourceTint",tint);result.SetTexture("_SourceAlbedo",texture);result.SetFloat("_HasAlbedo",texture==null?0:1);
            result.SetVector("_SourceUV",new Vector4(scale.x,scale.y,offset.x,offset.y));
            // The actual selected-body and articulated-glove readers contain no COLOR
            // stream and their original BaseMaterials disable vertex albedo.
            result.SetFloat("_VertexAlbedo",0);result.SetFloat("_GhostOpacity",R24GhostLifePolicy.Opacity);return result;
        }
        public void Refresh()
        {
            if(!Active)return;
            // A skin/glove change is infrequent. Rebind only if an actual source
            // material was replaced; ordinary pose/visibility writes stay untouched.
            bool changed=false;
            foreach(var r in records)if(r.View!=null)
            {var live=r.View.sharedMaterials;if(live.Length!=r.Replacement.Length){changed=true;break;}for(int i=0;i<live.Length;i++)if(live[i]!=r.Replacement[i]){changed=true;break;}if(changed)break;}
            if(!changed)return;
            // Preserve a legitimate source material replacement that arrived while
            // ghosted, rather than restoring the previous style over it.
            foreach(var r in records)if(r.View!=null)
            {var live=r.View.sharedMaterials;if(live.Length!=r.Replacement.Length)r.Original=live;else for(int i=0;i<live.Length;i++)if(live[i]!=r.Replacement[i])r.Original[i]=live[i];}
            Restore();SetEnabled(true);
        }
        public void Restore()
        {
            foreach(var r in records)if(r.View!=null)
            {
                // A real skin/glove material change may arrive before the host's
                // SetRoots call. Restore only slots we still own, preserving it.
                var live=r.View.sharedMaterials;
                for(int i=0;i<live.Length&&i<r.Replacement.Length;i++)if(live[i]==r.Replacement[i])live[i]=r.Original[i];
                r.View.sharedMaterials=live;r.View.shadowCastingMode=r.Shadow;
            }
            records.Clear();foreach(var pair in materials)if(pair.Value!=null)Destroy(pair.Value);materials.Clear();if(defaultMaterial!=null)Destroy(defaultMaterial);defaultMaterial=null;
            scratch.Clear();SurfaceCount=0;Active=false;
        }
        void OnDisable(){Restore();}void OnDestroy(){Restore();}
    }
}
#endif
