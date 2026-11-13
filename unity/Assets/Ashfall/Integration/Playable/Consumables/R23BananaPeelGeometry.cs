using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Consumables
{
    // Exact banana_peel_086.gd quarter-shell equation, then one Z/winding conversion.
    public static class R23BananaPeelGeometry
    {
        public const int Rings=25,Edges=7,VerticesPerStrip=24*6*6;
        public static Float3[] BuildStrip(int index,float amount)
        {
            if(index<0||index>3||Single.IsNaN(amount)||Single.IsInfinity(amount))throw new ArgumentOutOfRangeException();
            amount=Math.Max(0,Math.Min(1,amount));var rings=new Float3[Rings,Edges];
            double arc=amount*Math.PI*1.18,middle=index*Math.PI*.5+Math.PI*.25;
            for(int step=0;step<Rings;step++)
            {
                double t=step/24.0;
                double radial=arc>.00001?.22*(1-Math.Cos(arc*t))/Math.Max(arc,.00001):0;
                double height=arc>.00001?.22*Math.Sin(arc*t)/Math.Max(arc,.00001):.22*t;
                double centerX=.050*Math.Pow(2*t-1,2),r=.0255*(.15+.85*Math.Pow(Math.Sin(Math.PI*t),.45));
                for(int edge=0;edge<Edges;edge++)
                {
                    double angle=index*Math.PI*.5+edge/6.0*Math.PI*.5;
                    rings[step,edge]=new Float3((float)(centerX+Math.Cos(middle)*radial+Math.Cos(angle)*r),(float)(-.11+height),(float)(-Math.Sin(middle)*radial-Math.Sin(angle)*r));
                }
            }
            var vertices=new Float3[VerticesPerStrip];int at=0;
            for(int row=0;row<24;row++)for(int column=0;column<6;column++)
            {
                // Source 0,2,1 /0,3,2 is reflected once to Unity front-face winding.
                Float3 a=rings[row,column],b=rings[row,column+1],c=rings[row+1,column+1],d=rings[row+1,column];
                vertices[at++]=a;vertices[at++]=b;vertices[at++]=c;vertices[at++]=a;vertices[at++]=c;vertices[at++]=d;
            }
            return vertices;
        }
    }
}
