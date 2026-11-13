#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Original mp9_laser091 world-space camera-facing beam. Caller supplies the
    // real clipped bore ray; this presenter never aims, fires, or damages anything.
    public sealed class R23Mp9Laser:IDisposable
    {
        readonly GameObject root;readonly Mesh mesh;readonly Material material;readonly MeshRenderer renderer;
        public bool Visible {get{return renderer!=null&&renderer.enabled;}}public Vector3 WorldStart {get;private set;}public Vector3 WorldEnd {get;private set;}
        public R23Mp9Laser(Transform content){if(content==null)throw new ArgumentNullException("content");root=new GameObject("Source MP9 physical red laser");root.layer=19;root.transform.SetParent(content,false);mesh=new Mesh();mesh.name="Source MP9 beam2tri";mesh.MarkDynamic();root.AddComponent<MeshFilter>().sharedMesh=mesh;material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(3,.032f,.018f,.8f));material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;}
        public void Present(Vector3 start,Vector3 end,Vector3 headPosition,Vector3 boreRight,bool visible){renderer.enabled=false;if(!visible)return;var direction=end-start;if(direction.sqrMagnitude<.0000001f)return;var side=Vector3.Cross(direction.normalized,headPosition-start).normalized*.0012f;if(side.sqrMagnitude<.0000001f)side=boreRight*.0012f;var inverse=root.transform.worldToLocalMatrix;mesh.vertices=new[]{inverse.MultiplyPoint3x4(start-side),inverse.MultiplyPoint3x4(start+side),inverse.MultiplyPoint3x4(end+side),inverse.MultiplyPoint3x4(end-side)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();WorldStart=start;WorldEnd=end;renderer.enabled=true;}
        public void Hide(){if(renderer!=null)renderer.enabled=false;}
        public void Dispose(){Hide();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);}
    }
}
#endif
