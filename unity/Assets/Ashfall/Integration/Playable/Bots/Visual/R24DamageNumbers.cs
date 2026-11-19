#if UNITY_6000_0_OR_NEWER
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Bots
{
    // damage_numbers0992.gd: one non-accumulating world number per actual pool loss.
    // No collider, targeting or damage authority; the bot owner supplies actual loss.
    public sealed class R24DamageNumbers:IDisposable
    {
        public const int Limit=48;public const float Lifetime=.85f;
        sealed class Entry {internal TextMesh Text;internal MeshRenderer Renderer;internal Vector3 Start;internal float Remaining;internal Color Tint;}
        readonly Entry[] entries=new Entry[Limit];readonly Transform root,head;readonly Font font;readonly Material material;
        int cursor,serial;bool disposed;public long Emitted {get;private set;}public float LastAmount {get;private set;}
        public int ActiveCount {get{if(disposed||root==null)return 0;int count=0;foreach(var e in entries)if(e!=null&&e.Remaining>0)count++;return count;}}
        public R24DamageNumbers(Transform actualHead)
        {
            if(actualHead==null)throw new ArgumentNullException("actualHead");head=actualHead;
            font=Resources.Load<Font>("R23InventoryUI");var shader=Resources.Load<Shader>("R24DamageNumber");
            if(font==null||shader==null)throw new InvalidOperationException("Damage number font/shader missing");
            material=new Material(shader){name="Shared depth-tested source damage numbers"};material.SetTexture("_MainTex",font.material.mainTexture);
            root=new GameObject("Source damage numbers48 / .85s").transform;Font.textureRebuilt+=FontRebuilt;
        }
        void FontRebuilt(Font changed){if(!disposed&&font!=null&&changed==font)material.SetTexture("_MainTex",font.material.mainTexture);}
        public bool Emit(float actualLoss,Vector3 sourceAnchor,bool shield){return Emit(actualLoss,sourceAnchor,shield,false);}
        public bool EmitBlade(float actualLoss,Vector3 contact,bool shield){return Emit(actualLoss,contact,shield,true);}
        bool Emit(float actualLoss,Vector3 sourceAnchor,bool shield,bool blade)
        {
            if(disposed||root==null||material==null||font==null||head==null||!Finite(actualLoss)||actualLoss<=0||!Finite(sourceAnchor))return false;
            float distance=Vector3.Distance(head.position,sourceAnchor);if(!Finite(distance))return false;
            var point=blade?sourceAnchor:sourceAnchor+head.right.normalized*(Mathf.Max(.55f,distance*.014f)+.04f*(serial%2))+Vector3.up*(.10f+.12f*(serial%4));
            var entry=entries[cursor];if(entry==null){var node=new GameObject("One actual damage event "+cursor);node.layer=R24ValleyBots.CombatLayer;node.transform.SetParent(root,false);var text=node.AddComponent<TextMesh>();text.font=font;text.fontSize=26;text.richText=false;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;var renderer=node.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;entry=new Entry{Text=text,Renderer=renderer};entries[cursor]=entry;}
            entry.Renderer.sharedMaterial=material;
            entry.Start=point;entry.Remaining=Lifetime;entry.Tint=shield?new Color(78/255f,216/255f,1):new Color(1,240/255f,173/255f);
            // TextMesh uses .1 units per font pixel at characterSize1. Source Label3D
            // pixel_size is metres per pixel; retain its distance-scaled angular size.
            entry.Text.characterSize=10*Mathf.Max(distance*.00060f,.0015f)*(blade?2f:1f);entry.Text.text=Mathf.Max(1,Mathf.FloorToInt(Mathf.Min(actualLoss,1000000)+.5f)).ToString(CultureInfo.InvariantCulture);
            font.RequestCharactersInTexture(entry.Text.text,26);material.SetTexture("_MainTex",font.material.mainTexture);entry.Text.color=entry.Tint;entry.Text.transform.position=point;entry.Text.gameObject.SetActive(true);
            cursor=(cursor+1)%Limit;serial=(serial+1)%4;Emitted++;LastAmount=actualLoss;return true;
        }
        public void Advance(float seconds)
        {
            if(disposed||root==null)return;if(!Finite(seconds)||seconds<0)throw new ArgumentOutOfRangeException("seconds");
            foreach(var entry in entries)if(entry!=null&&entry.Remaining>0){entry.Remaining=Mathf.Max(0,entry.Remaining-seconds);if(entry.Remaining<=0){entry.Text.gameObject.SetActive(false);continue;}entry.Text.transform.position=entry.Start+Vector3.up*(Lifetime-entry.Remaining)*.14f;var color=entry.Tint;color.a=Mathf.Min(1,entry.Remaining/.22f);entry.Text.color=color;}
        }
#if UNITY_EDITOR
        public TextMesh FixtureLabel(int slot){return slot<0||slot>=Limit||entries[slot]==null?null:entries[slot].Text;}
        public Material FixtureMaterial {get{return material;}}
#endif
        // Retain the bounded geometry pool; map/menu/reset must leave no stale
        // world labels. Diagnostics restart with the context, source serial does not.
        public void Clear(){if(disposed)return;foreach(var entry in entries)if(entry!=null){entry.Remaining=0;if(entry.Text!=null)entry.Text.gameObject.SetActive(false);}cursor=0;Emitted=0;LastAmount=0;}
        public void Dispose(){if(disposed)return;disposed=true;Font.textureRebuilt-=FontRebuilt;if(root!=null){root.gameObject.SetActive(false);UnityEngine.Object.Destroy(root.gameObject);}if(material!=null)UnityEngine.Object.Destroy(material);}
        static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}static bool Finite(Vector3 v){return Finite(v.x)&&Finite(v.y)&&Finite(v.z);}
    }
}
#endif
