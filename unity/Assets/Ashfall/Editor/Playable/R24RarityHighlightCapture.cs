#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Ashfall.Integration.Playable;
using UnityEngine;
using UnityEditor;
namespace Ashfall.Editor.Playable
{
    // Four Unity-only images: original additive versus current soft tint at two
    // identical cameras, using five actual unowned MK18 world instances/tiers.
    public sealed class R24RarityHighlightCapture
    {
        [Serializable]sealed class Image{public string mode,view,path;public Vector3 eye,target;public float fov;public int width=1200,height=800;public string colorSpace;public bool matchedGodot=false;}
        [Serializable]sealed class Receipt{public bool success,nativeExecuted,cleanup,inventoryRetained,geometryRetained,headsetVerified=false;public string error="";public List<string> actions=new List<string>(),shaderErrors=new List<string>();public List<Image> images=new List<Image>();}
        readonly Receipt result=new Receipt();readonly Dictionary<MeshRenderer,Material> originals=new Dictionary<MeshRenderer,Material>();readonly Dictionary<MeshFilter,Mesh> meshes=new Dictionary<MeshFilter,Mesh>();readonly Dictionary<Material,Material> controls=new Dictionary<Material,Material>();
        R23PlayableLoop loop;Camera camera;string originalMap,inventoryBefore,output;float deadline;bool manual,external,rig,pump,persist;
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        void Pump(){Need(Time.realtimeSinceStartup<deadline-15,"Rarity240s bound/cleanup margin");loop.SmokeUiUpdate();}
        string Inventory(){var rows=new List<string>();foreach(string id in loop.Gameplay.OwnedWeaponActionIds){var state=loop.Gameplay.ReadWeaponState(id);rows.Add(id+":"+state.Loaded+":"+state.Seated+":"+state.Shots);}foreach(string id in new[]{"pistol","smg","rifle","shells","sniper"})rows.Add(id+":"+loop.Gameplay.ReadAmmoReserveFamily(id));foreach(var item in loop.Consumables.OwnedItems)rows.Add(item.Id+":"+item.Slot);foreach(var item in loop.Explosives.OwnedItems)rows.Add(item.Id+":"+item.Slot);rows.Sort(StringComparer.Ordinal);return String.Join("|",rows.ToArray());}
        public IEnumerator Run()
        {
            deadline=Time.realtimeSinceStartup+240;while(Time.realtimeSinceStartup<deadline-140){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual ready XR-off Root required";Finish();yield break;}
            result.nativeExecuted=true;originalMap=loop.Map.MapId;manual=loop.ManualSmoke;external=loop.ExternalProbePump;rig=loop.Rig.enabled;pump=loop.Gameplay.ExternalInputPump;persist=loop.Calibration.SuppressPersistence;inventoryBefore=Inventory();
            output=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";Directory.CreateDirectory(output);
            loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
            try{var work=Drive(Capture(),"capture");while(work.MoveNext())yield return work.Current;work=Drive(Cleanup(),"cleanup");while(work.MoveNext())yield return work.Current;}
            finally{RestoreMaterials();if(camera!=null)UnityEngine.Object.Destroy(camera.gameObject);foreach(var material in controls.Values)UnityEngine.Object.Destroy(material);foreach(string id in result.actions)if(loop!=null)loop.Gameplay.DespawnWorldWeapon(id);loop.ManualSmoke=manual;loop.ExternalProbePump=external;loop.Rig.enabled=rig;loop.Gameplay.ExternalInputPump=pump;loop.Calibration.SuppressPersistence=persist;Finish();}
        }
        IEnumerator Drive(IEnumerator work,string phase){try{while(true){bool more=false;object next=null;try{Pump();more=work.MoveNext();if(more)next=work.Current;}catch(Exception e){result.error+=phase+": "+e+"\n";break;}if(!more)break;yield return next;}}finally{var d=work as IDisposable;if(d!=null)d.Dispose();}}
        IEnumerator Capture()
        {
            var before=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Ashfall/Editor/Playable/ArtControls/R23GroundRarityBefore.shader");Need(before!=null,"Preserved original rarity control shader missing");
            loop.ReturnToLobby();loop.SelectMap("range",true);while(!loop.Ready&&loop.Failure==null){Pump();yield return null;}Need(loop.Ready&&loop.Map.MapId=="range","Actual Range load failed: "+loop.Failure);loop.StartPlaying();loop.WorldLoot.SetWorldEnabled(false);
            Vector3 center=loop.Map.GameSpawn+new Vector3(0,1.1f,0);Bounds total=new Bounds(center,Vector3.zero);bool haveBounds=false;
            for(int stars=1;stars<=5;stars++)
            {
                string id;Need(loop.Gameplay.SpawnWorldWeapon("mk18",stars,center+Vector3.right*((stars-3)*.85f),Quaternion.Euler(0,90,0),out id,true),"Actual MK18 world rarity instance spawn refused");result.actions.Add(id);
                Need(loop.Gameplay.TryReadWeaponInstance(id,out var instance)&&instance.Stars==stars&&!loop.Gameplay.OwnsWeapon(id),"Actual finite unowned tier identity");
                var dictionary=(IDictionary)loop.Gameplay.GetType().GetField("droppedWeapons",Fields).GetValue(loop.Gameplay);object record=dictionary[id];var root=(Transform)record.GetType().GetField("Visual",Fields).GetValue(record);Need(root!=null,"Actual world weapon visual missing");
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.gameObject.layer=30;var filter=renderer.GetComponent<MeshFilter>();if(filter!=null)meshes.Add(filter,filter.sharedMesh);
                    if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.name=="Ashfall/R23GroundRarity")
                    {var original=renderer.sharedMaterial;originals.Add(renderer,original);if(!controls.ContainsKey(original)){var control=new Material(before);control.SetColor("_RarityColor",original.GetColor("_RarityColor"));controls.Add(original,control);}}
                    if(!haveBounds){total=renderer.bounds;haveBounds=true;}else total.Encapsulate(renderer.bounds);
                }
            }
            Need(originals.Count>=5&&controls.Count==5,"Actual five rarity overlay material routes required");
            camera=new GameObject("Owned rarity comparison camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=50;camera.aspect=1.5f;camera.nearClipPlane=.03f;camera.farClipPlane=40;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.10f,.12f,1);camera.allowHDR=false;
            float near=Mathf.Max(3.5f,total.extents.x/(Mathf.Tan(25*Mathf.Deg2Rad)*1.5f)*1.35f);Vector3 target=total.center;
            foreach(string view in new[]{"near","far"})
            {
                float distance=near*(view=="near"?1:1.8f);camera.transform.SetPositionAndRotation(target+new Vector3(0,.2f,-distance),Quaternion.LookRotation(new Vector3(0,-.2f,distance),Vector3.up));
                foreach(string mode in new[]{"before","current"}){foreach(var pair in originals)pair.Key.sharedMaterial=mode=="before"?controls[pair.Value]:pair.Value;CaptureImage(mode,view,target);}
            }
            foreach(var pair in meshes)Need(pair.Key!=null&&pair.Key.sharedMesh==pair.Value,"Probe geometry changed during shader comparison");result.geometryRetained=true;Need(Inventory()==inventoryBefore,"Rarity capture changed carried inventory or reserves");result.inventoryRetained=true;
            RestoreMaterials();yield break;
        }
        void CaptureImage(string mode,string view,Vector3 target)
        {
            string path=Path.Combine(output,mode+"-"+view+".png");Need(!File.Exists(path),"Never overwrite rarity capture");var render=RenderTexture.GetTemporary(1200,800,24,RenderTextureFormat.ARGB32);var old=camera.targetTexture;var active=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels=new Texture2D(1200,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,800),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());result.images.Add(new Image{mode=mode,view=view,path=path,eye=camera.transform.position,target=target,fov=camera.fieldOfView,colorSpace=QualitySettings.activeColorSpace.ToString()});}
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(render);if(pixels!=null)UnityEngine.Object.Destroy(pixels);}
            var checkedShaders=new HashSet<Shader>();foreach(var pair in originals)if(checkedShaders.Add(pair.Key.sharedMaterial.shader))foreach(var message in ShaderUtil.GetShaderMessages(pair.Key.sharedMaterial.shader))if(message.severity.ToString()=="Error")result.shaderErrors.Add(pair.Key.sharedMaterial.shader.name+": "+message.message);
            Need(result.shaderErrors.Count==0,"Actual rendered rarity shader errors");
        }
        void RestoreMaterials(){foreach(var pair in originals)if(pair.Key!=null)pair.Key.sharedMaterial=pair.Value;}
        IEnumerator Cleanup(){RestoreMaterials();foreach(string id in result.actions)loop.Gameplay.DespawnWorldWeapon(id);loop.ReturnToLobby();if(loop.Map.MapId!=originalMap){loop.SelectMap(originalMap,false);while(!loop.Ready&&loop.Failure==null){Pump();yield return null;}}Need(loop.Ready&&loop.Map.MapId==originalMap,"Original map restore failed");loop.ReturnToLobby();Need(Inventory()==inventoryBefore,"Cleanup changed carried inventory/reserves");result.cleanup=true;}
        void Finish(){result.success=result.nativeExecuted&&result.images.Count==4&&result.geometryRetained&&result.inventoryRetained&&result.cleanup&&result.error.Length==0&&result.shaderErrors.Count==0;R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));}
    }
}
#endif
