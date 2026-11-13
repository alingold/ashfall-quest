#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfall.Integration.Playable.PickupHints
{
    public struct R23PickupHintTarget
    {
        public readonly string Id,Label;public readonly Vector3 Center;
        public R23PickupHintTarget(string id,string label,Vector3 worldCenter){Id=id;Label=label;Center=worldCenter;}
        public bool Valid {get{return !String.IsNullOrEmpty(Id)&&!String.IsNullOrEmpty(Label)&&Label.Length<=256&&Finite(Center);}}
        static bool Finite(Vector3 p){return !(Single.IsNaN(p.x)||Single.IsNaN(p.y)||Single.IsNaN(p.z)||Single.IsInfinity(p.x)||Single.IsInfinity(p.y)||Single.IsInfinity(p.z));}
    }
    public struct R23PickupHintReadout
    {
        public readonly bool Visible;public readonly string TargetId,Label;public readonly Vector3 Center;public readonly Quaternion Rotation;public readonly Color Color;
        public readonly int RingVertices,RingTriangles,LabelGlyphs;
        internal R23PickupHintReadout(bool visible,R23PickupHintTarget target,Quaternion rotation,Color color,int glyphs)
        {Visible=visible;TargetId=target.Id;Label=target.Label;Center=target.Center;Rotation=rotation;Color=color;RingVertices=576;RingTriangles=288;LabelGlyphs=glyphs;}
    }
    // Passive visual port: the real weapon/consumable providers own target identity,
    // labels, aim/range/occlusion eligibility and hand custody. No hardware polling.
    public sealed class R23PickupHints:MonoBehaviour
    {
        sealed class Hand
        {internal Transform Ring;internal Mesh LabelMesh;internal MeshRenderer LabelRenderer;internal R23PickupHintTarget Target;internal bool Dirty;internal int Glyphs;internal Color Color;}
        readonly Hand[] hands=new Hand[2];readonly List<Material> materials=new List<Material>();
        Transform head,content;Mesh ringMesh;Font font;Material fontMaterial;bool initialized,gameplay;
        public bool Ready {get{return initialized;}}
        public int VisibleCount {get{return initialized?(hands[0].Ring.gameObject.activeSelf?1:0)+(hands[1].Ring.gameObject.activeSelf?1:0):0;}}
        public Transform VisualRoot {get{return content;}}
        public void Initialize(Transform acceptedHead)
        {
            if(initialized)throw new InvalidOperationException("Pickup hints already initialized");if(acceptedHead==null)throw new ArgumentNullException("acceptedHead");
            var shader=Resources.Load<Shader>("R23PickupHint");if(shader==null)throw new InvalidOperationException("Source pickup-hint shader missing");
            var sourceFont=Resources.Load<Font>("R23GodotUI");if(sourceFont==null)throw new InvalidOperationException("Original Godot UI font missing");
            // Unity cannot clone dynamic fonts. Share the original asset and rebuild our own glyph mesh when its atlas changes.
            font=sourceFont;head=acceptedHead;
            content=new GameObject("R23 source pickup aim hints").transform;content.gameObject.layer=2;
            Float3[] source;int[] indices;R23PickupHintGeometry.Build(out source,out indices);var vertices=new Vector3[source.Length];for(int i=0;i<vertices.Length;i++)vertices[i]=U(source[i]);
            ringMesh=new Mesh();ringMesh.name="Source24-segment pickup ring";ringMesh.vertices=vertices;ringMesh.uv=new Vector2[vertices.Length];ringMesh.triangles=indices;ringMesh.RecalculateBounds();
            fontMaterial=new Material(shader);fontMaterial.name="R23 source30px pickup name";fontMaterial.SetColor("_Color",Color.white);fontMaterial.SetFloat("_Font",1);fontMaterial.SetInt("_SrcBlend",5);fontMaterial.SetInt("_DstBlend",10);fontMaterial.SetInt("_ZWrite",0);fontMaterial.SetInt("_Cull",0);fontMaterial.renderQueue=3000;materials.Add(fontMaterial);
            for(int i=0;i<2;i++)
            {
                var hand=new Hand();hand.Color=i==0?new Color(.25f,.85f,1):new Color(1,.8f,.2f);hand.Ring=Node(i==0?"PickupAimRingLeft":"PickupAimRingRight",content);
                var material=new Material(shader);material.name=i==0?"Source left cyan pickup ring":"Source right gold pickup ring";material.SetColor("_Color",hand.Color);material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_ZWrite",1);material.SetInt("_Cull",2);material.renderQueue=2000;materials.Add(material);Render(hand.Ring,ringMesh,material);
                var label=Node("TargetName",hand.Ring);label.localPosition=Vector3.up*R23PickupHintGeometry.LabelHeight;hand.LabelMesh=new Mesh();hand.LabelMesh.name="Original pickup name pixel mesh";hand.LabelMesh.MarkDynamic();hand.LabelRenderer=Render(label,hand.LabelMesh,fontMaterial);hand.Ring.gameObject.SetActive(false);hands[i]=hand;
            }
            Font.textureRebuilt+=AtlasChanged;initialized=true;
        }
        static Transform Node(string name,Transform parent){var go=new GameObject(name);go.layer=2;go.transform.SetParent(parent,false);return go.transform;}
        static MeshRenderer Render(Transform node,Mesh mesh,Material material)
        {node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;return renderer;}
        // One call per host frame/state transition, with actual physical hand0/1 targets.
        public void Present(bool gameplayEnabled,R23PickupHintTarget left,R23PickupHintTarget right)
        {
            if(!initialized)return;gameplay=gameplayEnabled;
            Float3 x,y,z;if(!gameplayEnabled||!isActiveAndEnabled||head==null||!head.gameObject.activeInHierarchy||!R23PickupHintGeometry.Upright(F(head.forward),out x,out y,out z)){ClearAll();return;}
            var rotation=Quaternion.LookRotation(U(z),U(y));Set(0,left,rotation);Set(1,right,rotation);RefreshLabels();
        }
        void Set(int handIndex,R23PickupHintTarget target,Quaternion rotation)
        {
            var hand=hands[handIndex];if(!target.Valid){Clear(handIndex);return;}
            hand.Dirty|=hand.Target.Label!=target.Label;hand.Target=target;hand.Ring.SetPositionAndRotation(target.Center,rotation);hand.Ring.gameObject.SetActive(true);
        }
        public void Clear(int physicalHand)
        {
            if(physicalHand<0||physicalHand>1)throw new ArgumentOutOfRangeException("physicalHand");if(!initialized)return;
            var hand=hands[physicalHand];if(hand.Ring!=null)hand.Ring.gameObject.SetActive(false);hand.Target=new R23PickupHintTarget();hand.Dirty=false;hand.Glyphs=0;if(hand.LabelMesh!=null)hand.LabelMesh.Clear();
        }
        public void ClearAll(){if(initialized){Clear(0);Clear(1);}}
        public void Suspend(){gameplay=false;ClearAll();}
        public R23PickupHintReadout Read(int physicalHand)
        {
            if(physicalHand<0||physicalHand>1)throw new ArgumentOutOfRangeException("physicalHand");if(!initialized)return new R23PickupHintReadout();
            var hand=hands[physicalHand];return new R23PickupHintReadout(hand.Ring.gameObject.activeSelf,hand.Target,hand.Ring.rotation,hand.Color,hand.Glyphs);
        }
        void AtlasChanged(Font changed){if(changed==font)foreach(var hand in hands)if(hand!=null)hand.Dirty=true;}
        void RefreshLabels()
        {
            if(font==null)return;
            string characters="";foreach(var hand in hands)if(hand.Ring.gameObject.activeSelf)characters+=hand.Target.Label;
            if(characters.Length>0)font.RequestCharactersInTexture(characters,R23PickupHintGeometry.FontPixels,FontStyle.Normal);
            fontMaterial.SetTexture("_MainTex",font.material.mainTexture);
            foreach(var hand in hands)if(hand.Dirty&&hand.Ring.gameObject.activeSelf){BuildLabel(hand);hand.Dirty=false;}
        }
        void BuildLabel(Hand hand)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();float pen=0;float low=float.PositiveInfinity,high=float.NegativeInfinity;
            foreach(char character in hand.Target.Label)
            {
                CharacterInfo glyph;if(!font.GetCharacterInfo(character,out glyph,R23PickupHintGeometry.FontPixels,FontStyle.Normal))continue;
                int first=vertices.Count;
                if(glyph.maxX>glyph.minX&&glyph.maxY>glyph.minY)
                {
                    vertices.Add(new Vector3(pen+glyph.minX,glyph.minY,0));vertices.Add(new Vector3(pen+glyph.maxX,glyph.minY,0));vertices.Add(new Vector3(pen+glyph.maxX,glyph.maxY,0));vertices.Add(new Vector3(pen+glyph.minX,glyph.maxY,0));
                    uv.Add(glyph.uvBottomLeft);uv.Add(glyph.uvBottomRight);uv.Add(glyph.uvTopRight);uv.Add(glyph.uvTopLeft);indices.AddRange(new[]{first,first+2,first+1,first,first+3,first+2});low=Mathf.Min(low,glyph.minY);high=Mathf.Max(high,glyph.maxY);
                }
                pen+=glyph.advance;
            }
            float centerY=vertices.Count>0?(low+high)*.5f:0;
            for(int i=0;i<vertices.Count;i++){var p=vertices[i];vertices[i]=new Vector3((p.x-pen*.5f)*R23PickupHintGeometry.PixelSize,(p.y-centerY)*R23PickupHintGeometry.PixelSize,0);}
            hand.LabelMesh.Clear();hand.LabelMesh.SetVertices(vertices);hand.LabelMesh.SetUVs(0,uv);hand.LabelMesh.SetTriangles(indices,0,true);hand.Glyphs=vertices.Count/4;
        }
        void LateUpdate()
        {
            if(!initialized)return;if(!gameplay||head==null||!head.gameObject.activeInHierarchy){ClearAll();return;}
            RefreshLabels(); // text-atlas maintenance only; no input/target selection or tracked pose writes.
        }
        void OnDisable(){Suspend();}
        void OnApplicationPause(bool paused){if(paused)Suspend();}
        void OnDestroy()
        {
            Font.textureRebuilt-=AtlasChanged;if(content!=null)Destroy(content.gameObject);if(ringMesh!=null)Destroy(ringMesh);
            foreach(var hand in hands)if(hand!=null&&hand.LabelMesh!=null)Destroy(hand.LabelMesh);foreach(var material in materials)if(material!=null)Destroy(material);font=null;initialized=false;
        }
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}static Vector3 U(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
    }
}
#endif
