using System;
using System.IO;
using System.Globalization;

// Isolated conversion candidate; does not define or modify shared game contracts.
namespace Ashfall.ToolchainProbe
{
    public struct Vec
    {
        public readonly double X, Y, Z;
        public Vec(double x, double y, double z)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y) || double.IsNaN(z) || double.IsInfinity(z))
                throw new ArgumentException("Coordinates must be finite");
            X = x; Y = y; Z = z;
        }
        public static Vec Sub(Vec a, Vec b) { return new Vec(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
        public static Vec Cross(Vec a, Vec b) { return new Vec(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X); }
        public static double Dot(Vec a, Vec b) { return a.X*b.X+a.Y*b.Y+a.Z*b.Z; }
        public Vec Unit() { double n = Math.Sqrt(Dot(this,this)); if(n < 1e-12) throw new ArgumentException("Zero normal"); return new Vec(X/n,Y/n,Z/n); }
    }
    public static class Conversion
    {
        public static Vec Reflect(Vec v) { return new Vec(v.X,v.Y,-v.Z); }
        // Row-major 3x3 basis, operating on column vectors; S R S also preserves scale/shear.
        public static double[] Basis(double[] m)
        {
            if(m == null || m.Length != 9) throw new ArgumentException("Expected 3x3 basis");
            double[] result = new double[9];
            for(int r=0;r<3;r++) for(int c=0;c<3;c++)
            {
                double v = m[r*3+c];
                if(double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentException("Nonfinite basis");
                result[r*3+c] = v * (r==2 ? -1 : 1) * (c==2 ? -1 : 1);
            }
            return result;
        }
        public static Vec Apply(double[] m, Vec v) { return new Vec(m[0]*v.X+m[1]*v.Y+m[2]*v.Z,m[3]*v.X+m[4]*v.Y+m[5]*v.Z,m[6]*v.X+m[7]*v.Y+m[8]*v.Z); }
        public static Vec Normal(double[] m, Vec v)
        {
            double a=m[0],b=m[1],c=m[2],d=m[3],e=m[4],f=m[5],g=m[6],h=m[7],i=m[8];
            double det=a*(e*i-f*h)-b*(d*i-f*g)+c*(d*h-e*g);
            if(Math.Abs(det)<1e-12) throw new ArgumentException("Singular basis");
            return new Vec(((e*i-f*h)*v.X+(f*g-d*i)*v.Y+(d*h-e*g)*v.Z)/det,
                ((c*h-b*i)*v.X+(a*i-c*g)*v.Y+(b*g-a*h)*v.Z)/det,
                ((b*f-c*e)*v.X+(c*d-a*f)*v.Y+(a*e-b*d)*v.Z)/det).Unit();
        }
        public static int[] Winding(int[] indices)
        {
            if(indices==null || indices.Length%3!=0) throw new ArgumentException("Triangle triplets required");
            int[] result=(int[])indices.Clone();
            for(int j=0;j<result.Length;j+=3) { if(result[j]<0 || result[j+1]<0 || result[j+2]<0) throw new ArgumentException("Negative index"); int t=result[j+1]; result[j+1]=result[j+2]; result[j+2]=t; }
            return result;
        }
    }
    public static class Probe
    {
        static int checks;
        static void Check(bool ok,string name) { if(!ok) throw new Exception("FAIL: "+name); checks++; Console.WriteLine("PASS: "+name); }
        static bool Near(Vec a,Vec b) { return Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)+Math.Abs(a.Z-b.Z)<1e-9; }
        static void Reject(Action action,string name) { bool rejected=false; try { action(); } catch(ArgumentException) { rejected=true; } Check(rejected,name); }
        public static int Main(string[] args)
        {
            try
            {
                Check(Near(Conversion.Reflect(new Vec(0,0,-1)),new Vec(0,0,1)),"Godot forward becomes Unity forward without scaling");
                Vec p=new Vec(1.25,-3,7.5);
                Check(Near(Conversion.Reflect(Conversion.Reflect(p)),p),"point round trip");
                double[] basis={0.8,-0.6,0,0.6,0.8,0,0,0,1};
                Check(Near(Conversion.Apply(Conversion.Basis(basis),Conversion.Reflect(p)),Conversion.Reflect(Conversion.Apply(basis,p))),"basis conjugation commutes with transformed point");
                double[] arbitrary={2,0.3,0.5,0.1,3,0.7,0.2,0.4,4};
                Check(Near(Conversion.Apply(Conversion.Basis(arbitrary),Conversion.Reflect(p)),Conversion.Reflect(Conversion.Apply(arbitrary,p))),"nonuniform scale and shear preserved");
                Random random=new Random(20261104);
                bool randomized=true;
                for(int k=0;k<1000;k++) { Vec v=new Vec(random.NextDouble(),random.NextDouble(),random.NextDouble()); randomized &= Near(Conversion.Apply(Conversion.Basis(arbitrary),Conversion.Reflect(v)),Conversion.Reflect(Conversion.Apply(arbitrary,v))); }
                Check(randomized,"1000 deterministic conversion samples");
                Vec tangent=new Vec(1,1,0), normal=new Vec(1,-1,0);
                Vec tn=Conversion.Normal(arbitrary,normal);
                Check(Math.Abs(Vec.Dot(Conversion.Apply(arbitrary,tangent),tn))<1e-9,"inverse transpose normal remains perpendicular under shear");
                Check(Math.Abs(Vec.Dot(tn,tn)-1)<1e-9,"normal renormalized");
                Vec[] triangle={new Vec(0,0,0),new Vec(1,0,0),new Vec(0,1,1)};
                int[] indices={0,1,2}; int[] reversed=Conversion.Winding(indices);
                Vec geometric=Vec.Cross(Vec.Sub(Conversion.Reflect(triangle[reversed[1]]),Conversion.Reflect(triangle[0])),Vec.Sub(Conversion.Reflect(triangle[reversed[2]]),Conversion.Reflect(triangle[0]))).Unit();
                Vec expected=Conversion.Reflect(Vec.Cross(Vec.Sub(triangle[1],triangle[0]),Vec.Sub(triangle[2],triangle[0])).Unit());
                Check(Near(geometric,expected),"single winding reversal agrees with reflected normal");
                Check(indices[1]==1 && indices[2]==2,"source indices stay immutable");
                Reject(delegate { new Vec(double.NaN,0,0); },"NaN rejected");
                Reject(delegate { new Vec(0,double.PositiveInfinity,0); },"infinity rejected");
                Reject(delegate { Conversion.Basis(new double[8]); },"malformed basis rejected");
                Reject(delegate { Conversion.Normal(new double[9],normal); },"singular normal transform rejected");
                Reject(delegate { Conversion.Winding(new int[]{0,1}); },"partial triangle rejected");
                Reject(delegate { Conversion.Winding(new int[]{0,-1,2}); },"negative index rejected");
                Console.WriteLine("RESULT: "+checks+" checks passed; neutral .NET Framework executable only. Unity/import/XR/build/network/audio acceptance UNTESTED.");
                return 0;
            }
            catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
