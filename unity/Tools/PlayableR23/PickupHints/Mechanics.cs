using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.PickupHints;
static class PickupHintCases
{
    static int count;
    static void Need(bool okay,string message){count++;if(!okay)throw new Exception(message);}
    static double Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
    static double Length(Float3 a){return Math.Sqrt(Dot(a,a));}
    static Float3 Cross(Float3 a,Float3 b){return new Float3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
    static void Main()
    {
        Float3[] v;int[] indices;R23PickupHintGeometry.Build(out v,out indices);
        Need(v.Length==576&&indices.Length==864,"24 exact closed source boxes in one ring mesh");
        foreach(int i in indices)if(i<0||i>=v.Length)throw new Exception("source ring index bound");
        Need(R23PickupHintGeometry.FontPixels==30&&R23PickupHintGeometry.PixelSize==.0015f&&R23PickupHintGeometry.LabelHeight==.24f,"source label30px/pixel scale and vertical anchor");
        for(int segment=0;segment<24;segment++)
        {
            double angle=Math.PI*2*segment/24,tx=-Math.Sin(angle),ty=Math.Cos(angle),nx=Math.Cos(angle),ny=Math.Sin(angle);double lx=0,ly=0,lz=0,minT=1,maxT=-1,minN=1,maxN=-1,minZ=1,maxZ=-1;
            for(int j=0;j<24;j++)
            {
                var p=v[segment*24+j];if(!p.IsFinite)throw new Exception("source ring nonfinite vertex");lx+=p.X;ly+=p.Y;lz+=p.Z;
                double t=p.X*tx+p.Y*ty,n=p.X*nx+p.Y*ny;minT=Math.Min(minT,t);maxT=Math.Max(maxT,t);minN=Math.Min(minN,n);maxN=Math.Max(maxN,n);minZ=Math.Min(minZ,p.Z);maxZ=Math.Max(maxZ,p.Z);
            }
            Need(Math.Abs(lx/24-nx*.17)<.0000001&&Math.Abs(ly/24-ny*.17)<.0000001&&Math.Abs(lz)<.0000001,"source segment center at .17m circle");
            Need(Math.Abs(maxT-minT-.041)<.0000001&&Math.Abs(maxN-minN-.007)<.0000001&&Math.Abs(maxZ-minZ-.007)<.0000001,"source tangent bar .041 by .007 by .007 metres");
            for(int tri=0;tri<12;tri++)
            {
                int at=segment*36+tri*3;var a=v[indices[at]];var b=v[indices[at+1]];var c=v[indices[at+2]];var ab=new Float3(b.X-a.X,b.Y-a.Y,b.Z-a.Z);var ac=new Float3(c.X-a.X,c.Y-a.Y,c.Z-a.Z);
                Need(Length(Cross(ab,ac))>.000048,"all source box surfaces are real nondegenerate triangles");
            }
        }
        foreach(var input in new[]{new Float3(0,0,1),new Float3(1,0,0),new Float3(-1,0,0),new Float3(.3f,.5f,.8f),new Float3(0,1,0),new Float3(0,-1,0),new Float3(.0005f,1,0),new Float3(.0005f,-1,0)})
        {
            Float3 x,y,z;Need(R23PickupHintGeometry.Upright(input,out x,out y,out z),"source camera-facing upright basis exists");
            Need(Math.Abs(Length(x)-1)<.000001&&Math.Abs(Length(y)-1)<.000001&&Math.Abs(Length(z)-1)<.000001,"source basis orthonormal vectors");
            Need(Math.Abs(Dot(x,y))<.000001&&Math.Abs(Dot(x,z))<.000001&&Math.Abs(Dot(y,z))<.000001,"source x-first Gram-Schmidt has no skew");
            Need(Dot(Cross(x,y),z)>.99999,"proper billboard basis");
            if(Math.Abs(input.Y)>Math.Sqrt(input.X*input.X+input.Y*input.Y+input.Z*input.Z)*.999999)Need(x.X==1&&x.Y==0&&x.Z==0,"near-vertical source fallback is world-right, not headset roll");
            else Need(y.Y>=0&&Math.Abs(x.Y)<.000001,"world-upright strips head roll while retaining view pitch");
        }
        Float3 a1,b1,c1;Need(!R23PickupHintGeometry.Upright(new Float3(),out a1,out b1,out c1),"missing orientation cannot publish cue");Need(!R23PickupHintGeometry.Upright(new Float3(float.NaN,0,1),out a1,out b1,out c1),"invalid orientation cannot publish cue");
        Console.WriteLine("PASS "+count+" source pickup-ring geometry/upright/label-scale cases; CPU only.");
    }
}
