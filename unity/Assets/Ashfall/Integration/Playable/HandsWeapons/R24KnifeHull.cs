using System;using System.Collections.Generic;using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Godot consumes a convex point cloud; Unity consumes a triangle mesh before
    // convex cooking. Triangulate the SAME cloud as a closed outward hull. An
    // arbitrary triangle fan is not a closed mesh and loses that input contract.
    public static class R24KnifeHull
    {
        struct Face{public int A,B,C;public Face(int a,int b,int c){A=a;B=b;C=c;}}
        struct Edge{public int A,B,Count;public Edge(int a,int b){A=a;B=b;Count=1;}}
        const double Epsilon=.0000001;
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}static double Dot(Float3 a,Float3 b){return(double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z;}static Float3 Cross(Float3 a,Float3 b){return new Float3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}static double Length2(Float3 a){return Dot(a,a);}
        static Face Outward(int a,int b,int c,List<Float3> points,Float3 inside){var n=Cross(Sub(points[b],points[a]),Sub(points[c],points[a]));return Dot(n,Sub(inside,points[a]))>0?new Face(a,c,b):new Face(a,b,c);}
        static void AddEdge(Dictionary<long,Edge> edges,int a,int b){long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);Edge e;if(edges.TryGetValue(key,out e)){e.Count++;edges[key]=e;}else edges.Add(key,new Edge(a,b));}
        public static bool TryBuild(Float3[] cloud,out Float3[] vertices,out int[] triangles){vertices=new Float3[0];triangles=new int[0];if(cloud==null||cloud.Length<4||cloud.Length>4096)return false;var p=new List<Float3>();foreach(var point in cloud){if(!point.IsFinite)return false;bool same=false;foreach(var prior in p)if(Length2(Sub(point,prior))<Epsilon*Epsilon){same=true;break;}if(!same)p.Add(point);}if(p.Count<4)return false;
            int a=0,b=-1,c=-1,d=-1;double far=0;for(int i=1;i<p.Count;i++){double n=Length2(Sub(p[i],p[a]));if(n>far){far=n;b=i;}}if(b<0||far<Epsilon*Epsilon)return false;var ab=Sub(p[b],p[a]);far=0;for(int i=0;i<p.Count;i++){double n=Length2(Cross(ab,Sub(p[i],p[a])));if(n>far){far=n;c=i;}}if(c<0||far<Epsilon*Epsilon*Length2(ab))return false;var normal=Cross(ab,Sub(p[c],p[a]));far=0;for(int i=0;i<p.Count;i++){double n=Math.Abs(Dot(normal,Sub(p[i],p[a])));if(n>far){far=n;d=i;}}if(d<0||far<Epsilon*Math.Sqrt(Length2(normal)))return false;
            var inside=new Float3((p[a].X+p[b].X+p[c].X+p[d].X)*.25f,(p[a].Y+p[b].Y+p[c].Y+p[d].Y)*.25f,(p[a].Z+p[b].Z+p[c].Z+p[d].Z)*.25f);var faces=new List<Face>{Outward(a,b,c,p,inside),Outward(a,d,b,p,inside),Outward(a,c,d,p,inside),Outward(b,d,c,p,inside)};
            for(int i=0;i<p.Count;i++){if(i==a||i==b||i==c||i==d)continue;var visible=new List<int>();var edges=new Dictionary<long,Edge>();for(int j=0;j<faces.Count;j++){var f=faces[j];var n=Cross(Sub(p[f.B],p[f.A]),Sub(p[f.C],p[f.A]));if(Dot(n,Sub(p[i],p[f.A]))>Epsilon*Math.Sqrt(Length2(n))){visible.Add(j);AddEdge(edges,f.A,f.B);AddEdge(edges,f.B,f.C);AddEdge(edges,f.C,f.A);}}if(visible.Count==0)continue;for(int j=visible.Count-1;j>=0;j--)faces.RemoveAt(visible[j]);foreach(var edge in edges.Values)if(edge.Count==1){var n=Cross(Sub(p[edge.B],p[edge.A]),Sub(p[i],p[edge.A]));if(Length2(n)>Epsilon*Epsilon*Epsilon*Epsilon)faces.Add(Outward(edge.A,edge.B,i,p,inside));}}
            var map=new Dictionary<int,int>();var v=new List<Float3>();var indices=new List<int>();foreach(var f in faces)foreach(int index in new[]{f.A,f.B,f.C}){int mapped;if(!map.TryGetValue(index,out mapped)){mapped=v.Count;map.Add(index,mapped);v.Add(p[index]);}indices.Add(mapped);}if(v.Count<4||faces.Count<4)return false;vertices=v.ToArray();triangles=indices.ToArray();return true;
        }
    }
}
