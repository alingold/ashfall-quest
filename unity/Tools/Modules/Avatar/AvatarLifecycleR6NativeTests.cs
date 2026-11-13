#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Modules.Avatar;
// Coordinator only: include alongside unchanged R4 fixtures/harness. cwd=run02/work.
// No native execution by the implementation leaf.
public static class AvatarLifecycleR6NativeTests
{
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    static int[] Census(){return new[]{Resources.FindObjectsOfTypeAll<Mesh>().Length,Resources.FindObjectsOfTypeAll<Material>().Length,Resources.FindObjectsOfTypeAll<Texture2D>().Length};}
    static void Same(int[] before){var after=Census();for(int i=0;i<before.Length;i++)Need(before[i]==after[i],"Native resource census changed at "+i);}
    static void Rejected(Action action){try{action();}catch(InvalidOperationException){return;}throw new Exception("Inactive/unsupported admission accepted");}
    static void Disposed(Action action){try{action();}catch(ObjectDisposedException){return;}throw new Exception("Disposed admission accepted");}
    static void Assemble(AvatarView view){var a=AvatarR4Fixtures.Assembly();view.Assemble(a,true,AvatarR4Fixtures.Receipts(a));}
    public static void EditorLifetime()
    {
        Need(!Application.isPlaying,"Editor gate only");
        // Unchanged full old harness still required; failed swaps and repeat replacement
        // must retain their original expectations. Legacy never-active gate now rejects.
        AvatarEngineR4Tests.Run();
        var foreign=new Mesh();var foreignMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var foreignTexture=new Texture2D(2,2);
        try {
            int[] before=Census();
            var inactive=new GameObject("R6-never-active");inactive.SetActive(false);
            try {
                var view=inactive.AddComponent<AvatarView>();
                Rejected(delegate{Assemble(view);});Same(before);
                Need(!inactive.activeSelf&&inactive.transform.childCount==0,"Admission activated/allocated host");
                view.Dispose();view.Dispose();
            } finally {UnityEngine.Object.DestroyImmediate(inactive);}
            Same(before);
            var host=new GameObject("R6-active");
            try {
                var view=host.AddComponent<AvatarView>();
                view.enabled=false;Rejected(delegate{Assemble(view);});Same(before);
                view.enabled=true;Assemble(view);view.enabled=false;Same(before);
                Rejected(delegate{Assemble(view);});view.enabled=true;
                Assemble(view);host.SetActive(false);Same(before);
                Rejected(delegate{Assemble(view);});host.SetActive(true);
                Assemble(view);UnityEngine.Object.DestroyImmediate(view);Same(before);
                view=host.AddComponent<AvatarView>();Assemble(view);view.Dispose();view.Dispose();Same(before);
                Disposed(delegate{Assemble(view);});
                Disposed(delegate{view.Present(null,false,false);});
                Need(host.activeSelf,"Cleanup changed host activation");
            } finally {UnityEngine.Object.DestroyImmediate(host);}
            Same(before);
            Need(foreign!=null&&foreignMaterial!=null&&foreignTexture!=null,"Cleanup destroyed foreign resources");
        } finally {UnityEngine.Object.DestroyImmediate(foreign);UnityEngine.Object.DestroyImmediate(foreignMaterial);UnityEngine.Object.DestroyImmediate(foreignTexture);}
        Debug.Log("AvatarLifecycleR6 Editor lifetime PASS (inactive admission deliberately changed)");
    }
    public static void NormalBasis()
    {
        var host=new GameObject("R6-basis");
        try {
            var a=AvatarR4Fixtures.Assembly();host.AddComponent<AvatarView>().Assemble(a,true,AvatarR4Fixtures.Receipts(a));
            int gloves=0;
            foreach(var renderer in host.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                gloves++;var mesh=renderer.sharedMesh;var n=mesh.normals;var t=mesh.tangents;
                Need(t.Length==mesh.vertexCount,"Tangent count");
                for(int i=0;i<t.Length;i++) {
                    var tangent=new Vector3(t[i].x,t[i].y,t[i].z);var bitangent=Vector3.Cross(n[i],tangent)*t[i].w;
                    Need(Finite(t[i].x)&&Finite(t[i].y)&&Finite(t[i].z)&&Finite(t[i].w),"Finite tangent");
                    Need(Math.Abs(tangent.sqrMagnitude-1)<.002f&&Math.Abs(n[i].sqrMagnitude-1)<.002f&&Math.Abs(bitangent.sqrMagnitude-1)<.004f,"Unit basis");
                    Need(Math.Abs(Vector3.Dot(n[i],tangent))<.002f&&Math.Abs(Vector3.Dot(n[i],bitangent))<.002f&&Math.Abs(t[i].w)==1,"Orthonormal handed basis");
                }
                string label=renderer.transform.IsChildOf(host.transform.GetChild(0).Find("LeftHand"))?"LeftHand":"RightHand";
                foreach(var part in a.CopyParts())if(part.Label==label) {
                    var source=VerifiedSkinReader.Read(part);var indices=source.CopyIndices();var actual=mesh.triangles;
                    Need(indices.Length==actual.Length,"Source index length");for(int i=0;i<actual.Length;i++)Need(actual[i]==indices[i],"Winding converted twice");
                }
                Need(renderer.sharedMaterial.IsKeywordEnabled("_NORMALMAP")&&renderer.sharedMaterial.GetTexture("_BumpMap")!=null,"Authored normal map retained");
            }
            Need(gloves==2,"Both real gloves");
            // Analytic winding/UV holdouts independently establish handedness convention.
            foreach(bool mirrored in new[]{false,true}) {
                var mesh=new Mesh();try {
                    mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.normals=new[]{Vector3.forward,Vector3.forward,Vector3.forward};
                    mesh.uv=new[]{Vector2.zero,mirrored?Vector2.left:Vector2.right,Vector2.up};mesh.triangles=new[]{0,1,2};mesh.RecalculateTangents();
                    foreach(var t in mesh.tangents)Need(Math.Abs(t.w-(mirrored?-1:1))<.0001f,"Mirrored UV handedness");
                } finally {UnityEngine.Object.DestroyImmediate(mesh);}
            }
        } finally {UnityEngine.Object.DestroyImmediate(host);}
        Debug.Log("AvatarLifecycleR6 normal basis PASS; rendered normal-map quality OPEN");
    }
    static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
    // Root must drive this enumerator in PlayMode, yielding through real player frames.
    // A synchronous batch invocation is not deferred-destruction evidence.
    public static IEnumerator DeferredPlayMode()
    {
        Need(Application.isPlaying,"PlayMode required");
        var warm=new GameObject("R6-warm");Assemble(warm.AddComponent<AvatarView>());UnityEngine.Object.Destroy(warm);yield return null;yield return null;
        int[] before=Census();var host=new GameObject("R6-deferred");
        try {
            var view=host.AddComponent<AvatarView>();Assemble(view);view.enabled=false;
            Need(host.activeSelf,"Disable toggled host");yield return null;yield return null;Same(before);
            view.enabled=true;Assemble(view);view.Dispose();view.Dispose();
            yield return null;yield return null;Same(before);
            Disposed(delegate{Assemble(view);});
        } finally {UnityEngine.Object.Destroy(host);}
        yield return null;yield return null;Same(before);
        Debug.Log("AvatarLifecycleR6 deferred PlayMode PASS");
    }
#if UNITY_EDITOR
    // Coordinator supplies a prefab asset or open prefab-stage host; no fixture writes
    // prefab assets, scenes or shared bootstrap. Rejection must precede native allocation.
    public static void RejectPrefabHost(AvatarView prefabView)
    {int[] before=Census();Rejected(delegate{Assemble(prefabView);});Same(before);}
    const string ReloadKey="ASHFALL.AvatarLifecycleR6.reload-owned";
    public static void BeforeDomainReload()
    {
        Need(!Application.isPlaying,"Editor reload gate");
        var host=new GameObject("R6-reload-owned");var view=host.AddComponent<AvatarView>();Assemble(view);
        var ids=new List<string>();
        // Capture the actual ownership ledger, including decoded ORM source textures
        // which are not bound to a renderer after channel separation.
        var field=typeof(AvatarView).GetField("resources",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var owned=(List<UnityEngine.Object>)field.GetValue(view);
        foreach(var value in owned)ids.Add(value.GetEntityId().ToString());
        Need(ids.Count>0,"Empty ownership ledger");
        UnityEditor.SessionState.SetString(ReloadKey,string.Join("|",ids.ToArray()));
        // Coordinator now requests an actual domain reload, then invokes AfterDomainReload.
    }
    public static void AfterDomainReload()
    {
        string saved=UnityEditor.SessionState.GetString(ReloadKey,"");Need(saved.Length>0,"Missing real pre-reload receipt");
        var ids=new HashSet<string>(saved.Split('|'));
        foreach(var value in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            Need(!ids.Contains(value.GetEntityId().ToString()),"Owned native resource survived reload");
        UnityEditor.SessionState.EraseString(ReloadKey);
        // Explicit fixture cleanup only; this is never production/global cleanup.
        foreach(var view in Resources.FindObjectsOfTypeAll<AvatarView>())if(view.gameObject.name=="R6-reload-owned")UnityEngine.Object.DestroyImmediate(view.gameObject);
        Debug.Log("AvatarLifecycleR6 actual domain reload PASS");
    }
#endif
}
#endif
