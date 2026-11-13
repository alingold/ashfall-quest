#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Integration.Playable;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Native cutaway of the oracle's actual collision triangles. A narrow slab
    // removes foreground clutter without substituting an illustrative wall.
    public static class R25HeadclipVideo
    {
        static Camera side;
        static GameObject marker, rings, cutaway;
        static LineRenderer body;
        static Vector3 center, right, forward;
        static float slabHalfWidth;
        public static void Capture(R23VRRig rig, string output, string id, int frame, Vector3 site, R25HeadclipGeometry geometry)
        {
            string folder=Path.Combine(output,id);Directory.CreateDirectory(folder);
            if(side==null)
            {
                side=new GameObject("Headclip cutaway camera").AddComponent<Camera>();side.enabled=false;
                side.nearClipPlane=.05f;side.farClipPlane=30;side.orthographic=true;side.orthographicSize=3.1f;
                side.cullingMask=1<<2;side.clearFlags=CameraClearFlags.SolidColor;side.backgroundColor=new Color(.035f,.05f,.075f);
                var mat=new Material(Resources.Load<Shader>("R23VrUi"));mat.SetColor("_Color",new Color(1,.2f,.8f));mat.renderQueue=4060;
                marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="Eye center";
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());marker.layer=2;marker.transform.localScale=Vector3.one*.065f;
                marker.GetComponent<Renderer>().sharedMaterial=mat;
                rings=new GameObject("12 cm eye sphere");
                for(int axis=0;axis<3;axis++)
                {
                    var node=new GameObject("Sphere ring");node.layer=2;node.transform.SetParent(rings.transform,false);
                    var line=node.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=64;
                    line.startWidth=.012f;line.endWidth=.012f;line.sharedMaterial=mat;
                    for(int j=0;j<64;j++)
                    {
                        float a=j*Mathf.PI*2/64,r=rig.HeadCollision.EyeSafetyRadius;
                        Vector3 p=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0);
                        if(axis==1)p=new Vector3(p.x,0,p.y);if(axis==2)p=new Vector3(0,p.x,p.y);
                        line.SetPosition(j,p);
                    }
                }
                var nodeBody=new GameObject("Eye to logical feet");nodeBody.layer=2;body=nodeBody.AddComponent<LineRenderer>();
                body.positionCount=2;body.startWidth=.035f;body.endWidth=.035f;
                var bodyMat=new Material(mat);bodyMat.SetColor("_Color",new Color(.65f,.85f,1));bodyMat.renderQueue=4055;body.sharedMaterial=bodyMat;
            }
            if(frame==0)
            {
                forward=Vector3.ProjectOnPlane(rig.Head.transform.forward,Vector3.up).normalized;
                right=Vector3.Cross(Vector3.up,forward);
                center=rig.Head.transform.position+forward*1.1f-Vector3.up*.45f;
                side.orthographicSize=id=="logged-0-video"?4f:3.1f;
                slabHalfWidth=.30f;
                if(id.StartsWith("fixture-",StringComparison.Ordinal))
                {forward=Vector3.forward;right=Vector3.right;center=site-Vector3.up*.5f;slabHalfWidth=6f;}
                BuildCutaway(geometry);
                side.transform.position=slabHalfWidth>1?center+right*7-forward*5+Vector3.up*2.8f:center+right*9-forward*.4f+Vector3.up*1.5f;side.transform.LookAt(center);
            }
            marker.transform.position=rig.Head.transform.position;rings.transform.position=rig.Head.transform.position;
            body.SetPosition(0,rig.transform.position);body.SetPosition(1,rig.Head.transform.position);
            int mask=rig.Head.cullingMask;rig.Head.cullingMask=mask&~(1<<2);
            try { Render(rig.Head,Path.Combine(folder,"eye-"+frame.ToString("D4")+".png")); }
            finally {rig.Head.cullingMask=mask;}
            Render(side,Path.Combine(folder,"side-"+frame.ToString("D4")+".png"));
        }
        static Vector3 Local(Vector3 p)
        {p-=center;return new Vector3(Vector3.Dot(p,right),p.y,Vector3.Dot(p,forward));}
        static List<Vector3> Clip(List<Vector3> polygon,int axis,float sign,float limit)
        {
            var result=new List<Vector3>();if(polygon.Count==0)return result;
            Vector3 a=polygon[polygon.Count-1];float da=sign*a[axis]-limit;
            foreach(Vector3 b in polygon)
            {
                float db=sign*b[axis]-limit;
                if((da<=0)!=(db<=0))result.Add(Vector3.Lerp(a,b,da/(da-db)));
                if(db<=0)result.Add(b);a=b;da=db;
            }
            return result;
        }
        static void BuildCutaway(R25HeadclipGeometry geometry)
        {
            if(cutaway!=null)UnityEngine.Object.DestroyImmediate(cutaway);
            cutaway=new GameObject("Actual map cutaway (60 cm slab)");
            var vertices=new List<Vector3>[] {new List<Vector3>(),new List<Vector3>(),new List<Vector3>()};
            foreach(var t in geometry.Triangles)
            {
                var poly=new List<Vector3>{Local(t.a),Local(t.b),Local(t.c)};
                for(int axis=0;axis<3;axis++)
                {float limit=axis==0?slabHalfWidth:axis==1?3.5f:5.6f;poly=Clip(poly,axis,1,limit);poly=Clip(poly,axis,-1,limit);}
                int group=Mathf.Abs(t.normal.y)>.98f?0:Vector3.Dot(t.normal,forward)*t.normal.y>0?1:2;
                for(int i=1;i+1<poly.Count;i++)
                {vertices[group].Add(poly[0]);vertices[group].Add(poly[i]);vertices[group].Add(poly[i+1]);}
            }
            Color[] colors={new Color(.3f,.4f,.5f),new Color(.95f,.7f,.24f),new Color(.18f,.7f,.85f)};
            for(int g=0;g<3;g++)
            {
                var node=new GameObject("Clipped collision surfaces "+g);node.layer=2;node.transform.SetParent(cutaway.transform,false);
                var mesh=new Mesh();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                var world=new Vector3[vertices[g].Count];var indices=new int[world.Length];
                for(int i=0;i<world.Length;i++){Vector3 p=vertices[g][i];world[i]=center+right*p.x+Vector3.up*p.y+forward*p.z;indices[i]=i;}
                mesh.vertices=world;mesh.triangles=indices;mesh.RecalculateBounds();
                node.AddComponent<MeshFilter>().sharedMesh=mesh;
                var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.color=colors[g];mat.SetFloat("_Cull",0);
                node.AddComponent<MeshRenderer>().sharedMaterial=mat;
            }
        }
        static void Render(Camera camera,string path)
        {
            var target=RenderTexture.GetTemporary(640,360,24);var old=camera.targetTexture;var active=RenderTexture.active;
            float aspect=camera.aspect;Texture2D pixels=null;
            try
            {
                camera.targetTexture=target;camera.aspect=16f/9;camera.Render();RenderTexture.active=target;
                pixels=new Texture2D(640,360,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,640,360),0,0);pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally {camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)UnityEngine.Object.Destroy(pixels);}
        }
    }
}
#endif
