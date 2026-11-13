#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.UI;
namespace Ashfall.Editor.Playable
{
    // Editor-only, bounded capture of the production UI from the actual head camera.
    [InitializeOnLoad]
    public static class R25DetailCapture
    {
        const string Key = "Ashfall.Detail.Output";
        static IEnumerator work;
        static float deadline;
        static object waiting;
        static int lastFrame = -1;
        static R25DetailCapture() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            string output = R23PlayableBuild.Argument("-ashfallDetailOutput");
            if (String.IsNullOrEmpty(output) || Directory.Exists(output)) { EditorApplication.Exit(2); return; }
            Directory.CreateDirectory(output);
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            R23PlayableBuild.Xr().InitManagerOnStart = false;
            SessionState.SetString(Key, output);
            EditorApplication.isPlaying = true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if (SessionState.GetString(Key, "").Length == 0) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            { deadline = Time.realtimeSinceStartup + 600; work = CapturePages(); EditorApplication.update += Pump; }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Pump; work = null;
                string output = SessionState.GetString(Key, ""); SessionState.EraseString(Key);
                R23PlayableBuild.Xr().InitManagerOnStart = true; AssetDatabase.SaveAssets();
                EditorApplication.Exit(File.Exists(Path.Combine(output, "success.txt")) ? 0 : 2);
            }
        }
        static void Pump()
        {
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("600 second capture deadline");
                // Editor update runs faster than play frames; honor coroutine waits.
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                var delay = waiting as CustomYieldInstruction;
                if (delay != null && delay.keepWaiting) return;
                if (work != null)
                {
                    if (!work.MoveNext()) { work = null; EditorApplication.isPlaying = false; }
                    else waiting = work.Current;
                }
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(SessionState.GetString(Key, ""), "error.txt"), e.ToString());
                Debug.LogException(e); work = null; EditorApplication.isPlaying = false;
            }
        }
        static IEnumerator CapturePages()
        {
            var camera=new GameObject("Detail verification camera").AddComponent<Camera>();
            camera.fieldOfView=70;camera.nearClipPlane=.02f;camera.farClipPlane=1000;camera.cullingMask=1;
            var record=new System.Text.StringBuilder();
            // Load production maps directly, avoiding unrelated gameplay initialization.
            foreach(string id in new[]{"archive:afterlife-gods-alamo-hank","archive:badlands-gods-pitdevil","archive:the-white-house-gods-osprey-kat"})
            {
                var go=new GameObject("Detail production map");
                var map=go.AddComponent<Ashfall.Integration.Playable.World.R23PlayableMap>();map.BindViewCamera(camera);map.Load(id);
                while(!map.IsLoaded){if(map.LoadError!=null)throw new Exception(map.LoadError);yield return null;}
                Physics.SyncTransforms();
                var renderers=go.GetComponentsInChildren<MeshRenderer>();
                record.AppendLine(id+" renderers="+renderers.Length);
                var materials=new System.Collections.Generic.Dictionary<Material,float>();
                foreach(var r in renderers)foreach(var m in r.sharedMaterials)if(m!=null&&m.HasProperty("_R25DetailStrength")&&!materials.ContainsKey(m))materials.Add(m,m.GetFloat("_R25DetailStrength"));
                string prefix=id.Substring(8);
                // Choose real, large surface triangles by actual bound detail-map family.
                foreach(string kind in new[]{"terrain","wall","ground"})
                {
                    Vector3 point=Vector3.zero,normal=Vector3.up;float best=-1;string materialName="";
                    foreach(var r in renderers)
                    {
                        var mf=r.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)continue;
                        var mesh=mf.sharedMesh;
                        if(!mesh.isReadable)mesh=Readable(mesh);
                        var vertices=mesh.vertices;var normals=mesh.normals;
                        for(int sub=0;sub<mesh.subMeshCount;sub++)
                        {
                            var mat=r.sharedMaterials[sub];
                            if(!materials.ContainsKey(mat)||materials[mat]<=0)continue;
                            var detail=mat.GetTexture("_R25DetailMap");string dn=detail==null?"":detail.name;
                            if(kind=="ground"?mat.name!="sg":kind=="terrain"?(!dn.Contains("rock")&&!dn.Contains("sand")):(!dn.Contains("concrete")&&!dn.Contains("wood")))continue;
                            var indices=mesh.GetTriangles(sub);
                            for(int t=0;t<indices.Length;t+=3)
                            {
                                var a=mf.transform.TransformPoint(vertices[indices[t]]);var b=mf.transform.TransformPoint(vertices[indices[t+1]]);var c=mf.transform.TransformPoint(vertices[indices[t+2]]);
                                var cross=Vector3.Cross(b-a,c-a);var n=cross.normalized;var center=(a+b+c)/3;
                                if(normals.Length==vertices.Length)n=mf.transform.TransformDirection(normals[indices[t]]+normals[indices[t+1]]+normals[indices[t+2]]).normalized;
                                if(kind=="wall"&&Mathf.Abs(n.y)>.15f)continue;
                                if(kind=="wall")
                                {
                                    float bottom=Mathf.Min(a.y,Mathf.Min(b.y,c.y)),top=Mathf.Max(a.y,Mathf.Max(b.y,c.y));
                                    if(top-bottom<2)continue;
                                    float level=bottom+1.6f;
                                    var cuts=new System.Collections.Generic.List<Vector3>();
                                    var corners=new[]{a,b,c,a};
                                    for(int e=0;e<3;e++)if((corners[e].y<=level&&corners[e+1].y>=level)||(corners[e].y>=level&&corners[e+1].y<=level))
                                    {float delta=corners[e+1].y-corners[e].y;if(Mathf.Abs(delta)>.0001f)cuts.Add(Vector3.Lerp(corners[e],corners[e+1],(level-corners[e].y)/delta));}
                                    if(cuts.Count<2)continue;center=(cuts[0]+cuts[1])*.5f;
                                }
                                if(kind=="ground"&&Mathf.Abs(n.y)<.9f)continue;
                                if(Mathf.Abs(n.y)>.7f&&n.y<0)n=-n;
                                if(Mathf.Abs(n.y)<.7f && Vector3.Dot(n,map.Spawn-center)<0)n=-n;
                                // Reject points hidden behind other geometry even at arm's length.
                                bool visible=true;
                                foreach(float testDistance in new[]{.4f,2f,15f})
                                {
                                    RaycastHit hit;
                                    if(!Physics.Raycast(center+n*testDistance,-n,out hit,testDistance+.1f,1,QueryTriggerInteraction.Ignore)||Vector3.Distance(hit.point,center)>.15f){visible=false;break;}
                                }
                                if(!visible)continue;
                                float area=cross.magnitude;
                                float score=area/(1+Vector3.Distance(center,map.Spawn)*.01f);
                                if(kind=="terrain" && dn.Contains("rock"))score*=1000;
                                if(kind=="wall")score=Mathf.Min(area,100)/(1+Vector3.SqrMagnitude(center-map.Spawn)*.01f);
                                if(score>best){best=score;point=center;normal=n;materialName=mat.name;}
                            }
                        }
                    }
                    foreach(var copy in snapshots)UnityEngine.Object.Destroy(copy);snapshots.Clear();
                    if(best<0)throw new Exception(id+" "+kind+" unavailable");
                    foreach(float distance in new[]{.4f,2f,15f,35f})
                    {
                        Vector3 eye=point+normal*distance;
                        // Vertical surfaces are sampled at a player-height point on the triangle.
                        // Keep eye and wall target level; record actual camera position.
                        if(kind=="ground"){eye=point+Vector3.up*1.6f+Vector3.forward*distance;}
                        camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(point-eye,Mathf.Abs(normal.y)>.9f?Vector3.forward:Vector3.up));
                        foreach(bool on in new[]{false,true})
                        {
                            foreach(var m in materials)m.Key.SetFloat("_R25DetailStrength",on?m.Value:0);
                            yield return null;
                            SaveCamera(camera,prefix+"-"+kind+"-"+distance.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+(on?"-on":"-off"));
                        }
                        record.AppendLine(id+" "+kind+" distance="+distance+" material="+materialName+" eye="+eye+" target="+point+" normal="+normal);
                    }
                }
                UnityEngine.Object.Destroy(go);yield return null;
            }
            UnityEngine.Object.Destroy(camera.gameObject);
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"views.txt"),record.ToString());
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"success.txt"),"Actual maps; matched materials, camera, lighting; strength alone toggled. No gameplay or headset verification.");
        }
        static readonly System.Collections.Generic.List<Mesh> snapshots=new System.Collections.Generic.List<Mesh>();
        static Mesh Readable(Mesh source)
        {
            using(var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(source))
            {
                var mesh=data[0];
                var vertices=new Unity.Collections.NativeArray<Vector3>(mesh.vertexCount,Unity.Collections.Allocator.Temp);
                var normals=new Unity.Collections.NativeArray<Vector3>(mesh.vertexCount,Unity.Collections.Allocator.Temp);
                mesh.GetVertices(vertices);mesh.GetNormals(normals);
                var result=new Mesh();result.indexFormat=source.indexFormat;result.vertices=vertices.ToArray();result.normals=normals.ToArray();result.subMeshCount=mesh.subMeshCount;
                vertices.Dispose();normals.Dispose();
                for(int i=0;i<mesh.subMeshCount;i++)
                {
                    var indices=new Unity.Collections.NativeArray<int>(mesh.GetSubMesh(i).indexCount,Unity.Collections.Allocator.Temp);
                    mesh.GetIndices(indices,i);result.SetTriangles(indices.ToArray(),i);indices.Dispose();
                }
                snapshots.Add(result);return result;
            }
        }
        static void SaveCamera(Camera camera,string name)
        {
            var target = RenderTexture.GetTemporary(1600,1000,24);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(1600,1000,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(SessionState.GetString(Key,""),name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target); if(pixels!=null)UnityEngine.Object.Destroy(pixels);
            }
        }
    }
}
#endif

