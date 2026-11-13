#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Ashfall.Modules.Presentation;
namespace Ashfall.Integration.PersonalSettings.Engine
{
    // Root attests authored label materials/font/depth/outline and canonical anchors.
    // Binding actual objects supplies provenance, never an asset-approval callback.
    public sealed class NativeTargetFeedbackView : ITargetFeedbackPort,IPresentationTransientView,IDisposable
    {
        sealed class Anchor { public Transform Transform;public Vector3 Fixed;public bool Legacy;public int Index=-1; }
        readonly Dictionary<string,Anchor> targets=new Dictionary<string,Anchor>(StringComparer.Ordinal);
        readonly TextMesh[] labels=new TextMesh[NativeTargetFeedbackPool.Limit];
        readonly Renderer[] renderers=new Renderer[NativeTargetFeedbackPool.Limit];
        readonly Vector3[] starts=new Vector3[NativeTargetFeedbackPool.Limit];
        readonly Vector3[] scales=new Vector3[NativeTargetFeedbackPool.Limit];
        readonly HudModel hud;
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        NativeTargetFeedbackPool pool;
        Transform resourceRoot;
        Camera camera;
        object reset;
        int count;
        bool bound,disposed;
        public NativeTargetFeedbackView(HudModel hud) { if(hud==null)throw new ArgumentNullException("hud");this.hud=hud;reset=hud.TransientResetIdentity; }
        void Lane() { if(thread!=Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("Native presentation main lane"); }
        void Live() { if(disposed)throw new ObjectDisposedException("NativeTargetFeedbackView"); }
        // Compatibility path: caller supplies one existing authored label per target.
        // It has bounded capacity and fixed authored anchor placement without a camera.
        public void BindTarget(string canonicalTarget,TextMesh existingApprovedLabel)
        {
            Lane();Live();
            if(bound||String.IsNullOrEmpty(canonicalTarget)||targets.ContainsKey(canonicalTarget)||existingApprovedLabel==null||count>=NativeTargetFeedbackPool.Limit)throw new ArgumentException("target label");
            for(int i=0;i<count;i++)if(labels[i]==existingApprovedLabel)throw new ArgumentException("aliased target label");
            var renderer=existingApprovedLabel.GetComponent<Renderer>();if(renderer==null)throw new ArgumentException("label renderer");
            Clear();targets.Add(canonicalTarget,new Anchor {Transform=existingApprovedLabel.transform,Fixed=existingApprovedLabel.transform.position,Legacy=true,Index=count});
            Store(count++,existingApprovedLabel,renderer);pool=new NativeTargetFeedbackPool(count);
        }
        // All resources must be preallocated under this exact root; no Instantiate or
        // material clone per event. Root must reserve these resources exclusively.
        public void BindResources(Transform existingRoot,Camera existingCamera,TextMesh[] existingLabels)
        {
            Lane();Live();
            if(bound||count!=0||existingRoot==null||existingCamera==null||existingLabels==null||existingLabels.Length<1||existingLabels.Length>NativeTargetFeedbackPool.Limit)throw new ArgumentException("resources");
            for(int i=0;i<existingLabels.Length;i++) {
                var t=existingLabels[i];
                if(t==null||t.transform==existingRoot||!t.transform.IsChildOf(existingRoot)||t.GetComponent<Renderer>()==null)throw new ArgumentException("root label provenance");
                for(int j=0;j<i;j++)if(t==existingLabels[j])throw new ArgumentException("aliased pool label");
            }
            for(int i=0;i<existingLabels.Length;i++)Store(i,existingLabels[i],existingLabels[i].GetComponent<Renderer>());
            count=existingLabels.Length;pool=new NativeTargetFeedbackPool(count);resourceRoot=existingRoot;camera=existingCamera;bound=true;
        }
        void Store(int i,TextMesh label,Renderer renderer) { labels[i]=label;renderers[i]=renderer;scales[i]=label.transform.localScale;label.text="";renderer.enabled=false; }
        public void BindTargetAnchor(string canonicalTarget,Transform existingCanonicalDamageAnchor)
        {
            Lane();Live();if(!bound||String.IsNullOrEmpty(canonicalTarget)||targets.Count>=64||existingCanonicalDamageAnchor==null)throw new ArgumentException("canonical anchor");
            for(int i=0;i<count;i++)if(existingCanonicalDamageAnchor==labels[i].transform)throw new ArgumentException("moving label is not anchor");
            targets.Add(canonicalTarget,new Anchor {Transform=existingCanonicalDamageAnchor});
        }
        bool ResourcesLive()
        {
            if(bound&&(resourceRoot==null||camera==null||!resourceRoot.gameObject.activeInHierarchy||!camera.isActiveAndEnabled))return false;
            for(int i=0;i<count;i++)if(labels[i]==null||renderers[i]==null||!labels[i].gameObject.activeInHierarchy||(bound&&!labels[i].transform.IsChildOf(resourceRoot)))return false;
            return count>0;
        }
        void ResetIfNeeded() { if(!hud.Visible||!System.Object.ReferenceEquals(reset,hud.TransientResetIdentity)||!ResourcesLive())Clear();reset=hud.TransientResetIdentity; }
        public void Emit(string target,float amount,bool shieldHit)
        {
            Lane();if(disposed)return;ResetIfNeeded();Anchor anchor;
            if(!hud.Visible||!ResourcesLive()||target==null||!targets.TryGetValue(target,out anchor)||anchor.Transform==null||!anchor.Transform.gameObject.activeInHierarchy)return;
            Vector3 start=anchor.Legacy?anchor.Fixed:anchor.Transform.position;
            if(!Finite(start))return;
            float distance=0;Vector3 right=Vector3.zero;
            if(bound) { distance=Vector3.Distance(camera.transform.position,start);right=camera.transform.right.normalized;if(float.IsNaN(distance)||float.IsInfinity(distance)||!Finite(right)||right.sqrMagnitude<.5f)return; }
            int index=pool.Emit(target,amount,shieldHit,anchor.Index);if(index<0)return;
            var e=pool.Read(index);
            if(bound)start+=right*(Math.Max(.55f,distance*.014f)+.04f*(e.Pattern%2))+Vector3.up*(.10f+.12f*e.Pattern);
            starts[index]=start;labels[index].transform.position=start;
            if(bound)labels[index].transform.localScale=scales[index]*Math.Max(.0015f,Math.Min(.025f,distance*.00060f));
            Draw(index);
        }
        static bool Finite(Vector3 v) { return !float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z); }
        void Draw(int i)
        {
            var e=pool.Read(i);Anchor a;
            if(!e.Active||!targets.TryGetValue(e.Target,out a)||a.Transform==null||!a.Transform.gameObject.activeInHierarchy) { pool.Retire(i);labels[i].text="";renderers[i].enabled=false;return; }
            labels[i].text=e.Text;
            labels[i].color=e.ShieldHit?new Color(78/255f,216/255f,1,e.Alpha):new Color(1,240/255f,173/255f,e.Alpha);
            labels[i].transform.position=starts[i]+Vector3.up*e.Rise;
            if(bound)labels[i].transform.rotation=camera.transform.rotation;
            renderers[i].enabled=true;
        }
        public void Refresh(float deltaSeconds)
        {
            Lane();if(float.IsNaN(deltaSeconds)||float.IsInfinity(deltaSeconds)||deltaSeconds<0)throw new ArgumentException("delta");if(disposed)return;
            ResetIfNeeded();if(pool==null||!hud.Visible||!ResourcesLive())return;
            pool.Refresh(deltaSeconds);for(int i=0;i<count;i++)Draw(i);
        }
        public void Clear()
        {
            Lane();if(pool!=null)pool.Clear();
            for(int i=0;i<count;i++){if(labels[i]!=null)labels[i].text="";if(renderers[i]!=null)renderers[i].enabled=false;}
            reset=hud.TransientResetIdentity;
        }
        public void Dispose() { Lane();if(disposed)return;Clear();if(pool!=null)pool.Dispose();targets.Clear();Array.Clear(labels,0,labels.Length);Array.Clear(renderers,0,renderers.Length);camera=null;resourceRoot=null;disposed=true; }
    }
}
#endif
