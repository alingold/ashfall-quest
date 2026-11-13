using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Source BoxMesh(.52,.7,.22), CylinderMesh(.075,.075,.012).
    // No collision shape in source or in this port. UVs omitted: no texture bound.
    public sealed class R24RescueTrainingDummyGeometry
    {
        public readonly Float3[] Positions,Normals;public readonly int[] Indices;
        R24RescueTrainingDummyGeometry(List<Float3> p,List<Float3> n,List<int> t){Positions=p.ToArray();Normals=n.ToArray();Indices=t.ToArray();}
        static Float3 Add(Float3 a,Float3 b){return R23WeaponPoseMath.Add(a,b);}static Float3 Mul(Float3 a,float b){return R23WeaponPoseMath.Scale(a,b);}
        public static R24RescueTrainingDummyGeometry Body()
        {
            var p=new List<Float3>();var n=new List<Float3>();var t=new List<int>();
            Face(p,n,t,new Float3(1,0,0),new Float3(0,1,0),new Float3(0,0,1),.26f,.35f,.11f);
            Face(p,n,t,new Float3(-1,0,0),new Float3(0,-1,0),new Float3(0,0,1),.26f,.35f,.11f);
            Face(p,n,t,new Float3(0,1,0),new Float3(0,0,1),new Float3(1,0,0),.35f,.11f,.26f);
            Face(p,n,t,new Float3(0,-1,0),new Float3(0,0,-1),new Float3(1,0,0),.35f,.11f,.26f);
            Face(p,n,t,new Float3(0,0,1),new Float3(1,0,0),new Float3(0,1,0),.11f,.26f,.35f);
            Face(p,n,t,new Float3(0,0,-1),new Float3(-1,0,0),new Float3(0,1,0),.11f,.26f,.35f);
            return new R24RescueTrainingDummyGeometry(p,n,t);
        }
        static void Face(List<Float3> p,List<Float3> n,List<int> t,Float3 normal,Float3 u,Float3 v,float d,float a,float b)
        {int at=p.Count;foreach(var xy in new[]{new[]{-1,-1},new[]{1,-1},new[]{1,1},new[]{-1,1}}){p.Add(Add(Mul(normal,d),Add(Mul(u,xy[0]*a),Mul(v,xy[1]*b))));n.Add(normal);}foreach(int i in new[]{0,1,2,0,2,3})t.Add(at+i);}
        public static R24RescueTrainingDummyGeometry Pad()
        {
            const int radial=64,rings=4;var p=new List<Float3>();var n=new List<Float3>();var t=new List<int>();
            // Cylinder after source Rx(PI/2) and one Z reflection: axis -Z.
            for(int y=0;y<=rings+1;y++)for(int j=0;j<=radial;j++){float angle=(float)(j*Math.PI*2/radial),x=(float)Math.Cos(angle),v=(float)Math.Sin(angle);p.Add(new Float3(x*.075f,v*.075f,-.006f+.012f*y/(rings+1)));n.Add(new Float3(x,v,0));}
            for(int y=0;y<=rings;y++)for(int j=0;j<radial;j++){int a=y*(radial+1)+j,b=a+1,c=a+radial+1,d=c+1;foreach(int i in new[]{a,b,c,b,d,c})t.Add(i);}
            for(int sign=-1;sign<=1;sign+=2){int at=p.Count;p.Add(new Float3(0,0,sign*.006f));n.Add(new Float3(0,0,sign));for(int j=0;j<=radial;j++){float a=(float)(j*Math.PI*2/radial);p.Add(new Float3((float)Math.Cos(a)*.075f,(float)Math.Sin(a)*.075f,sign*.006f));n.Add(new Float3(0,0,sign));}for(int j=0;j<radial;j++){t.Add(at);t.Add(at+1+j+(sign>0?0:1));t.Add(at+1+j+(sign>0?1:0));}}
            return new R24RescueTrainingDummyGeometry(p,n,t);
        }
    }
}
