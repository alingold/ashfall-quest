using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Integration.Playable.PickupHints
{
    // weapon_inventory.gd::_create_aim_ring and upright_ui.gd::view_basis.
    public static class R23PickupHintGeometry
    {
        public const int Segments=24,FontPixels=30;
        public const float Radius=.17f,BarLength=.041f,BarWidth=.007f,LabelHeight=.24f,PixelSize=.0015f;
        public static void Build(out Float3[] positions,out int[] indices)
        {
            var vertices=new List<Float3>(576);var triangles=new List<int>(864);
            for(int segment=0;segment<Segments;segment++)
            {
                double angle=Math.PI*2*segment/Segments,rotation=angle+Math.PI*.5;
                float cx=(float)(Math.Cos(angle)*Radius),cy=(float)(Math.Sin(angle)*Radius),cos=(float)Math.Cos(rotation),sin=(float)Math.Sin(rotation);
                for(int axis=0;axis<3;axis++)for(int side=-1;side<=1;side+=2)
                {
                    int first=vertices.Count;
                    for(int corner=0;corner<4;corner++)
                    {
                        float[] p=new float[3];p[axis]=side*(axis==0?BarLength:BarWidth)*.5f;
                        int u=(axis+1)%3,v=(axis+2)%3;
                        p[u]=(corner==0||corner==3?-1:1)*(u==0?BarLength:BarWidth)*.5f;
                        p[v]=(corner<2?-1:1)*(v==0?BarLength:BarWidth)*.5f;
                        vertices.Add(new Float3(cx+p[0]*cos-p[1]*sin,cy+p[0]*sin+p[1]*cos,p[2]));
                    }
                    // Source box surface after the one local Z reflection.
                    int[] order=side>0?new[]{0,1,2,0,2,3}:new[]{0,2,1,0,3,2};
                    foreach(int i in order)triangles.Add(first+i);
                }
            }
            positions=vertices.ToArray();indices=triangles.ToArray();
        }
        public static bool Upright(Float3 viewForward,out Float3 right,out Float3 up,out Float3 forward)
        {
            right=up=forward=new Float3();if(!viewForward.IsFinite||Length(viewForward)<.000001f)return false;
            var z=Unit(viewForward);var x=Cross(new Float3(0,1,0),z);
            x=Dot(x,x)<.000001f?new Float3(1,0,0):Unit(x);
            var y=Unit(Cross(z,x));
            // Godot Basis.orthonormalized uses x-first Gram-Schmidt. This matters
            // at the source's near-vertical deterministic world-right fallback.
            y=Unit(Sub(y,Mul(x,Dot(x,y))));z=Unit(Sub(Sub(z,Mul(x,Dot(x,z))),Mul(y,Dot(y,z))));
            right=x;up=y;forward=z;return right.IsFinite&&up.IsFinite&&forward.IsFinite;
        }
        static Float3 Cross(Float3 a,Float3 b){return new Float3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
        static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        static float Length(Float3 a){return(float)Math.Sqrt(Dot(a,a));}
        static Float3 Unit(Float3 a){return Mul(a,1/Length(a));}
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        static Float3 Mul(Float3 a,float scale){return new Float3(a.X*scale,a.Y*scale,a.Z*scale);}
    }
}
