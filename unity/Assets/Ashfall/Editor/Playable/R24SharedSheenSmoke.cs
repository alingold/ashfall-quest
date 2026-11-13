#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Appearance;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfall.Editor.Playable
{
    public static partial class R24Feedback6237Smoke
    {
        [Serializable] sealed class ResponseRows { public int version=0;public ResponseSlot[] entries=null; }
        [Serializable] sealed class ResponseSlot
        {public string styleId="";public int materialIndex=0;public float roughnessFloor=0,dielectricScale=1;public bool rawMapFactors=false;}
        static IEnumerator SharedSheen98254(R23PlayableLoop loop,Action<string> note)
        {
            note("SHEEN98254 actual production material response fixture entered");
            var text=Resources.Load<TextAsset>("R23AppearanceMaterialResponses");Need(text!=null,"Actual response Resource exists");
            var rows=JsonUtility.FromJson<ResponseRows>(text.text);Need(rows.version==1&&rows.entries.Length==342,"Exact audited response table");
            var owner=typeof(R23PlayerAppearance);var manifestType=owner.GetNestedType("Manifest",BindingFlags.NonPublic);
            var assetType=owner.GetNestedType("Asset",BindingFlags.NonPublic);var lookup=assetType.GetMethod("SurfaceResponse",BindingFlags.Static|BindingFlags.NonPublic);
            Need(manifestType!=null&&lookup!=null,"Actual production deserializer and rule selector");
            var profiles=new Dictionary<string,object>(StringComparer.Ordinal);var rules=new Dictionary<string,ResponseSlot>(StringComparer.Ordinal);
            foreach(var row in rows.entries)
            {
                object profile;if(!profiles.TryGetValue(row.styleId,out profile))
                {profile=JsonUtility.FromJson(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"PlayableR23/Appearance/"+row.styleId+".json")),manifestType);profiles.Add(row.styleId,profile);}
                Need(lookup.Invoke(null,new[]{profile,(object)row.materialIndex})!=null,"Actual source-bound response "+row.styleId+"/"+row.materialIndex);
                rules.Add(row.styleId+"/"+row.materialIndex,row);
            }
            note("Actual JsonUtility source-slot matching342/342 across51 profiles; this is binding evidence, not rendering51 outfits.");
            var prior=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Ashfall/Editor/Playable/ArtControls/R23Appearance98254Before.shader");
            var shader=Resources.Load<Shader>("R23AppearanceDoubleSided");var specular=Resources.Load<Shader>("R23AppearanceSpecular");Need(prior!=null&&shader!=null&&specular!=null,"Current/common and immutable98254 control shaders");
            string[][] sets={new[]{"_SPECULAR_SETUP"},new[]{"_METALLICSPECGLOSSMAP","_SPECULAR_SETUP"},new[]{"_METALLICSPECGLOSSMAP","_NORMALMAP","_SPECULAR_SETUP"},new[]{"_EMISSION","_METALLICSPECGLOSSMAP","_NORMALMAP","_OCCLUSIONMAP","_SPECULAR_SETUP"}};
            var variants=new ShaderVariantCollection();try{foreach(var set in sets)variants.Add(new ShaderVariantCollection.ShaderVariant(shader,PassType.ScriptableRenderPipeline,set));variants.WarmUp();}finally{UnityEngine.Object.Destroy(variants);}
            foreach(var s in new[]{shader,specular,prior})foreach(var message in ShaderUtil.GetShaderMessages(s))Need(message.severity.ToString()!="Error",message.message);
            var originals=new Dictionary<MeshRenderer,Material>();var controls=new List<Material>();var layers=new Dictionary<GameObject,int>();Camera camera=null;
            string output=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";Directory.CreateDirectory(output);
            loop.ReturnToLobby();float rotation=loop.Preview.RotationMultiplier;loop.Preview.RotationMultiplier=0;int captures=0;string originalStyle=loop.Appearance.SelectedStyleId;
            try
            {
                camera=new GameObject("Owned shared sheen comparison camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=40;camera.cullingMask=1<<30;camera.nearClipPlane=.03f;camera.farClipPlane=20;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.045f,.055f,1);camera.allowHDR=false;
                foreach(string style in new[]{"claude","prowler","morgan_midnight","stacy_throwback","pj_golden","obsidian"})
                {
                    Need(loop.Preview.Show(style),"Actual production preview "+style);float end=Time.realtimeSinceStartup+40;
                    while((loop.Preview.Loading||loop.Preview.SelectedStyleId!=style)&&loop.Preview.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
                    Need(loop.Preview.Ready&&!loop.Preview.Loading&&loop.Preview.SelectedStyleId==style,"Actual committed style "+style+": "+loop.Preview.Failure);yield return null;
                    var appearance=(R23PlayerAppearance)Read(loop.Preview,"appearance");var avatar=(Transform)Read(loop.Preview,"avatar");
                    var asset=Read(appearance,"current");var profile=Read(asset,"Profile");var materials=(Array)Read(profile,"materials");
                    var bound=(Material[])Read(asset,"Materials");int affected=0;
                    for(int i=0;i<bound.Length;i++)
                    {
                        ResponseSlot row;bool selected=rules.TryGetValue(style+"/"+i,out row);var m=bound[i];
                        if(selected){Need(m.shader==shader&&m.IsKeywordEnabled("_SPECULAR_SETUP"),"Actual selected shader/keyword "+style+"/"+i);var response=m.GetVector("_SpecColor");Need(Mathf.Abs(response.x-row.roughnessFloor)<.00001f&&Mathf.Abs(response.y-row.dielectricScale)<.00001f&&response.w==(row.rawMapFactors?1:0),"Actual recipe/domain "+style+"/"+i);affected++;}
                        else Need(!m.IsKeywordEnabled("_SPECULAR_SETUP")||m.shader==specular,"Unlisted source slot is not assigned dry response "+style+"/"+i);
                    }
                    Need(style=="obsidian"?affected==0:affected>0,"Selected/protected coverage "+style);
                    File.WriteAllText(Path.Combine(output,style+"-actual-bindings.txt"),appearance.InspectMaterialBindings());
                    if(style=="prowler")
                    {
                        Call(loop,"SelectAppearance",style);float liveEnd=Time.realtimeSinceStartup+40;
                        while((loop.Appearance.Loading||loop.Appearance.SelectedStyleId!=style)&&loop.Appearance.Failure==null&&Time.realtimeSinceStartup<liveEnd)yield return null;
                        Need(loop.Appearance.Ready&&!loop.Appearance.Loading&&loop.Appearance.SelectedStyleId==style,"Actual live Root committed Prowler");
                        var glove=loop.Appearance.CurrentGloveMaterial;Need(glove!=null&&glove.IsKeywordEnabled("_SPECULAR_SETUP"),"Shared body/glove slot receives explicit skin response");
                        Need(ReferenceEquals(Read(loop.Gameplay,"leftGloveStyle"),glove)&&ReferenceEquals(Read(loop.Gameplay,"rightGloveStyle"),glove),"Actual published live material reaches both tracked glove owners");
                        File.WriteAllText(Path.Combine(output,style+"-live-bindings.txt"),loop.Appearance.InspectMaterialBindings());
                        note("Actual Root.SelectAppearance->live Asset.Build->both tracked glove material owners shares Prowler skin recipe; controller/art pose unchanged. HMD remains unverified.");
                    }
                    foreach(var root in new[]{avatar,appearance.CurrentVisualRoot})foreach(var t in root.GetComponentsInChildren<Transform>(true)){if(!layers.ContainsKey(t.gameObject))layers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=30;}
                    foreach(var renderer in appearance.CurrentVisualRoot.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var m=renderer.sharedMaterial;int index;Need(Int32.TryParse(m.name.Substring(m.name.LastIndexOf(' ')+1),out index),"Actual source material slot");
                        var control=new Material(m);controls.Add(control);originals.Add(renderer,m);
                        if(m.shader==shader)
                        {
                            var source=materials.GetValue(index);bool oldRecipe=style=="claude"||style=="witch_doctor"||style=="sol"||style=="astra"||style=="argon";
                            bool doubleSided=(bool)Read(source,"doubleSided");string normal=(string)Read(source,"normal");
                            control.shader=oldRecipe||doubleSided||!String.IsNullOrEmpty(normal)?prior:Shader.Find("Universal Render Pipeline/Lit");
                            if(oldRecipe&&m.IsKeywordEnabled("_SPECULAR_SETUP")){var value=control.GetVector("_SpecColor");value.w=1;control.SetVector("_SpecColor",value);}
                            else control.DisableKeyword("_SPECULAR_SETUP");
                        }
                    }
                    var center=avatar.position+Vector3.up*.92f;var eye=avatar.position+avatar.forward*2.8f+avatar.right*.85f+Vector3.up*1.15f;
                    camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(center-eye,Vector3.up));
                    for(int mode=0;mode<2;mode++)
                    {
                        int i=0;foreach(var pair in originals){pair.Key.sharedMaterial=mode==0?controls[i]:pair.Value;i++;}
                        string path=Path.Combine(output,style+(mode==0?"-98254-control.png":"-shared-response.png"));Capture(camera,path);captures++;
                        foreach(var pair in originals)foreach(var message in ShaderUtil.GetShaderMessages(pair.Key.sharedMaterial.shader))Need(message.severity.ToString()!="Error",message.message);
                        note("UNMATCHED Unity-only "+style+" "+(mode==0?"98254 material control":"shared response")+" image="+path+" eye="+eye.ToString("F6")+" target="+center.ToString("F6")+" FOV40; identical geometry/textures/camera/lighting; affectedSlots="+affected);
                    }
                    Restore(originals,controls,layers);
                }
                Need(captures==12,"Exactly12 source-current material comparison images");
                note("Four real keyword variants; six actual production previews,12 paired Unity-only images and committed material readouts. Obsidian protected; gold metallic pixels use bypass. No Godot-lighting/115-style/HMD/performance acceptance.");
            }
            finally{Restore(originals,controls,layers);if(camera!=null)UnityEngine.Object.Destroy(camera.gameObject);loop.Preview.RotationMultiplier=rotation;if(!String.IsNullOrEmpty(originalStyle))Call(loop,"SelectAppearance",originalStyle);}
        }
    }
}
#endif
