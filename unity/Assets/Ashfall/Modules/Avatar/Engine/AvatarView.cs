#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ashfall.Modules.Avatar
{
    [ExecuteAlways]
    public sealed class AvatarView : MonoBehaviour, IDisposable
    {
        bool disposed;
        public event Action PresentationInvalidated;
        void InvalidatePresentation()
        {
            var handlers=PresentationInvalidated;
            if(handlers!=null)foreach(Action handler in handlers.GetInvocationList())
                try{handler();}catch(Exception error){Debug.LogException(error,this);}
        }
        GameObject visual;
        readonly Dictionary<string,SkinBinding> skins=new Dictionary<string,SkinBinding>(StringComparer.Ordinal);
        readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
        readonly Dictionary<string,Transform> parts=new Dictionary<string,Transform>(StringComparer.Ordinal);
        readonly Dictionary<string,Matrix4x4> mounts=new Dictionary<string,Matrix4x4>(StringComparer.Ordinal);

        public void Assemble(AvatarAssembly assembly, bool allowPilot)
        { Assemble(assembly,allowPilot,null); }
        public void Assemble(AvatarAssembly assembly,bool allowPilot,AvatarRenderReceipts receipts)
        {
            RequireAdmission();
            if(assembly==null) throw new ArgumentNullException();
            if(assembly.ProductionApproved&&receipts==null)throw new InvalidOperationException("Production requires material receipts");
            if(!assembly.ProductionApproved && !allowPilot) throw new InvalidOperationException("Pilot requires explicit opt-in");
            var staged=new GameObject("Avatar-"+assembly.StyleId); staged.SetActive(false);
            var pending=new Dictionary<string,Transform>(StringComparer.Ordinal);
            var pendingMounts=new Dictionary<string,Matrix4x4>(StringComparer.Ordinal);
            var pendingSkins=new Dictionary<string,SkinBinding>(StringComparer.Ordinal);
            var pendingResources=new List<UnityEngine.Object>();
            try {
                staged.transform.SetParent(transform,false);
                foreach(var part in assembly.CopyParts()) {
                    // No caller-supplied prefab/resolver can substitute for verified bytes.

                    var mount=new GameObject(part.Label); mount.transform.SetParent(staged.transform,false);
                    GameObject instance;
                    if(part.Label=="LeftHand" || part.Label=="RightHand")
                    { SkinBinding binding; instance=VerifiedSkinFactory.Create(part,VerifiedSkinReader.Read(part,receipts==null?null:receipts.Dependencies(part.Label)),mount.transform,pendingResources,out binding,receipts==null?null:receipts.Material(assembly,part)); pendingSkins.Add(part.Label,binding); }
                    else instance=VerifiedRigidMeshFactory.Create(part,VerifiedMeshReader.Read(part),mount.transform,pendingResources);
                    ValidateInertHierarchy(instance);
                    pending.Add(part.Label,mount.transform);
                    float[] values=part.CopyMount(); var matrix=new Matrix4x4();
                    for(int r=0;r<4;r++) for(int c=0;c<4;c++) matrix[r,c]=values[r*4+c];
                    pendingMounts.Add(part.Label,matrix);
                    Apply(mount.transform,matrix);
                }
            } catch { Release(staged); foreach(var r in pendingResources) Release(r); throw; }
            InvalidatePresentation();
            if(visual!=null) { visual.SetActive(false); Release(visual); }
            foreach(var r in resources) Release(r); resources.Clear(); resources.AddRange(pendingResources);
            visual=staged; parts.Clear(); mounts.Clear(); skins.Clear();
            foreach(var pair in pendingSkins) skins.Add(pair.Key,pair.Value);
            foreach(var pair in pending) parts.Add(pair.Key,pair.Value);
            foreach(var pair in pendingMounts) mounts.Add(pair.Key,pair.Value);
            // Visibility is explicitly enabled only after a complete valid presentation frame.
        }

        public void HidePresentation(){if(visual!=null)visual.SetActive(false);}

        public void PresentHandPose(string label,ISkinPose pose)
        {
            RequireAdmission();
            if(visual==null) throw new InvalidOperationException("No assembled avatar");
            visual.SetActive(false); SkinBinding binding;
            if(pose==null || !skins.TryGetValue(label,out binding)) throw new InvalidOperationException("Missing hand pose/binding");
            binding.Apply(pose); // Remains hidden until a valid full Present frame.
        }
        // Poses are world-space render poses from Player/remote presentation. This never
        // writes tracked head/hands, locomotion transforms, collision, aim or network state.
        public void Present(IDictionary<string,Matrix4x4> worldPoses, bool trackingAvailable, bool localFirstPerson)
        { Present(worldPoses,trackingAvailable,localFirstPerson,null); }
        public void Present(IDictionary<string,Matrix4x4> worldPoses,bool trackingAvailable,bool localFirstPerson,IEnumerable<string> suppressedParts)
        {
            RequireAdmission();
            if(visual==null) return;
            visual.SetActive(false);
            if(!trackingAvailable || worldPoses==null) return;
            var suppressed=new HashSet<string>(StringComparer.Ordinal);
            if(suppressedParts!=null)foreach(string label in suppressedParts){if(label==null||!parts.ContainsKey(label))return;suppressed.Add(label);}
            // Full frame is required; never expose a partially updated avatar.
            var frame=new Dictionary<string,Matrix4x4>(StringComparer.Ordinal);
            foreach(var pair in parts) {
                Matrix4x4 pose;
                if(!worldPoses.TryGetValue(pair.Key,out pose)) return;
                Matrix4x4 local=visual.transform.worldToLocalMatrix*pose*mounts[pair.Key];
                if(!ValidTrs(local)) return;
                frame.Add(pair.Key,local);
            }
            foreach(var pair in parts) {
                Apply(pair.Value,frame[pair.Key]);
                pair.Value.gameObject.SetActive(!suppressed.Contains(pair.Key)&&(pair.Key!="AvatarHead" || !localFirstPerson));
            }
            visual.SetActive(true);
        }
        static bool ValidTrs(Matrix4x4 m)
        {
            var values=new float[16];
            for(int r=0;r<4;r++) for(int c=0;c<4;c++) values[r*4+c]=m[r,c];
            return AvatarMath.ValidTrs(values);
        }
        public static void ValidateInertHierarchy(GameObject root)
        {
            if(root==null) throw new ArgumentNullException("root");
            var contained=new HashSet<Transform>(root.GetComponentsInChildren<Transform>(true));
            foreach(var component in root.GetComponentsInChildren<Component>(true)) {
                if(component==null) throw new InvalidOperationException("Missing script/component");
                Type type=component.GetType();
                if(type!=typeof(Transform) && type!=typeof(MeshFilter) &&
                   type!=typeof(MeshRenderer) && type!=typeof(SkinnedMeshRenderer))
                    throw new InvalidOperationException("Unsupported render component: "+type.FullName);
                var skin=component as SkinnedMeshRenderer;
                if(skin!=null) {
                    if(skin.rootBone==null || !contained.Contains(skin.rootBone))
                        throw new InvalidOperationException("External/null skin root");
                    foreach(var bone in skin.bones)
                        if(bone==null || !contained.Contains(bone)) throw new InvalidOperationException("External/null skin bone");
                }
            }
        }

        static void Apply(Transform t, Matrix4x4 m)
        { t.localPosition=m.GetColumn(3); t.localRotation=m.rotation; t.localScale=m.lossyScale; }
        static void Release(UnityEngine.Object value) { if(Application.isPlaying)Destroy(value);else DestroyImmediate(value); }
        void RequireAdmission()
        {
            if(disposed) throw new ObjectDisposedException("AvatarView");
            // Unity does not promise destruction callbacks for never-active components.
            // Reject before allocating rather than activating a tracked/locomotion owner.
            if(this==null || !isActiveAndEnabled || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
                throw new InvalidOperationException("AvatarView requires an enabled active scene host");
#if UNITY_EDITOR
            if(UnityEditor.EditorUtility.IsPersistent(this) ||
               UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject)!=null)
                throw new InvalidOperationException("AvatarView prefab ownership is unsupported");
#endif
        }
        void ReleaseVisual()
        {
            var old=visual; visual=null;
            parts.Clear(); mounts.Clear(); skins.Clear();
            if(old!=null) { old.SetActive(false); Release(old); }
            foreach(var r in resources) if(r!=null) Release(r);
            resources.Clear();
        }
        // A disabled owner retains no native presentation resources. Re-enable requires
        // an explicit Assemble. This also releases transient resources before reload.
        void OnDisable() { InvalidatePresentation(); ReleaseVisual(); }
        public void Dispose() { if(disposed)return; disposed=true; InvalidatePresentation(); ReleaseVisual(); }
        void OnDestroy() { Dispose(); }
    }
}
#endif


