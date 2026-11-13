using System;
namespace Ashfall.Modules.Avatar
{
    public static class AvatarMath
    {
        public const string Version = "Avatar-r2/bounded-mesh-1";
        public const double OrthogonalityTolerance = 0.00001;
        // Render SI bounds, below float norm/determinant overflow.
        public const double MinScale = 0.0001, MaxScale = 10000, MaxPosition = 1000000;
        public static bool Bounded(float value, double limit)
        { return !Single.IsNaN(value) && !Single.IsInfinity(value) && Math.Abs((double)value)<=limit; }
        public static bool ValidTrs(float[] m)
        {
            if(m==null || m.Length!=16) return false;
            foreach(float v in m) if(!Bounded(v,MaxPosition)) return false;
            if(m[12]!=0 || m[13]!=0 || m[14]!=0 || m[15]!=1) return false;
            var n=new double[9];
            for(int c=0;c<3;c++) {
                double x=m[c], y=m[4+c], z=m[8+c];
                double len=Math.Sqrt(x*x+y*y+z*z);
                if(len<MinScale || len>MaxScale) return false;
                n[c]=x/len; n[3+c]=y/len; n[6+c]=z/len;
            }
            for(int c=0;c<3;c++) for(int d=c+1;d<3;d++)
                if(Math.Abs(n[c]*n[d]+n[3+c]*n[3+d]+n[6+c]*n[6+d])>=OrthogonalityTolerance) return false;
            return n[0]*(n[4]*n[8]-n[5]*n[7])-n[1]*(n[3]*n[8]-n[5]*n[6])+n[2]*(n[3]*n[7]-n[4]*n[6])>0;
        }
    }
}
