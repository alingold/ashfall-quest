using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Activities;
static class PreviewCases
{
    static int count;
    static void Check(bool okay,string message){count++;if(!okay)throw new Exception(message);}
    static float Length(Float3 a){return(float)Math.Sqrt(a.X*a.X+a.Y*a.Y+a.Z*a.Z);}
    static Float3 Minus(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
    static bool Near(Float3 a,Float3 b,float tolerance){return Length(Minus(a,b))<tolerance;}
    static void Main()
    {
        var recipe=new R23BuildLightningRecipe();var start=new Float3(.3f,1.2f,.2f);var end=new Float3(.3f,1.2f,5.2f);
        Check(recipe.Build(start,end,true,0),"source ribbons built");
        Check(recipe.Vertices.Count==recipe.Colors.Count&&recipe.Vertices.Count%12==0&&recipe.Vertices.Count<4096,"bounded ten crossed-ribbon triangle output");
        Check(recipe.FirstStrand.Count>2&&Near(recipe.FirstStrand[0],start,.000001f)&&Near(recipe.FirstStrand[recipe.FirstStrand.Count-1],end,.000001f),"source bolt endpoint anchors");
        float minimum=Math.Min(.4572f*.5f,Math.Max(.025f,5f*(float)Math.PI*2/(12f*2.4f)));float sum=0;int resets=0;
        foreach(float step in recipe.Spacings){Check(step>0&&step<=.457201f,"original bounded random interval");sum+=step;if(sum>4.99999f){Check(Math.Abs(sum-5)<.00001f,"one strand full aim-ray distance");sum=0;resets++;}else Check(step>=minimum-.000001f,"minimum random interval except final knot");}
        Check(resets==10,"ten actual full strands");
        int green=0,dark=0,white=0;foreach(uint rgb in recipe.Colors){if(rgb==0x58ff44)green++;else if(rgb==0x050706)dark++;else if(rgb==0xffffff)white++;else Check(false,"unrelated source color");}
        Check(green>0&&dark>0&&white>0&&green>dark*2&&green>white*2,"six green/two dark/two white source strand palette");
        for(int i=0;i<recipe.Vertices.Count;i+=12)
        {
            float width=Length(Minus(recipe.Vertices[i],recipe.Vertices[i+1]));Check(Math.Abs(width-.008f)<.000002f,"8 mm full ribbon width");
            foreach(int j in new[]{0,1,2,3,4,5,6,7,8,9,10,11})Check(recipe.Vertices[i+j].IsFinite,"all source ribbon vertices finite");
        }
        var frozen=recipe.Vertices.ToArray();recipe.Build(start,end,true,0);Check(recipe.Vertices.Count==frozen.Length,"deterministic topology");
        for(int i=0;i<frozen.Length;i++)Check(Near(frozen[i],recipe.Vertices[i],.0000001f),"original seeded recipe repeats");
        recipe.Build(start,end,true,80);bool changed=false;for(int i=0;i<frozen.Length;i++)if(!Near(frozen[i],recipe.Vertices[i],.000001f)){changed=true;break;}Check(changed,"source 6 Hz time interpolation animates knots");
        Check(Near(recipe.FirstStrand[0],start,.000001f)&&Near(recipe.FirstStrand[recipe.FirstStrand.Count-1],end,.000001f),"animated endpoints remain pinned");
        recipe.Build(start,end,true,166.66665);var before=recipe.Vertices.ToArray();recipe.Build(start,end,true,166.66668);float max=0;
        for(int i=0;i<before.Length;i++)max=Math.Max(max,Length(Minus(before[i],recipe.Vertices[i])));Check(max<.00002f,"smooth random tick crossing");
        foreach(var target in new[]{new Float3(.3f,5.2f,.2f),new Float3(.3f,-2.8f,.2f),new Float3(-3.7f,1.2f,.2f),new Float3(2.3f,2.2f,-2.8f)})
        {Check(recipe.Build(start,target,true,1200),"vertical/negative/reflected source direction");Check(Near(recipe.FirstStrand[recipe.FirstStrand.Count-1],target,.000001f),"source reflected endpoint exact");}
        Check(!recipe.Build(start,end,false,0)&&recipe.Vertices.Count==0&&recipe.FirstStrand.Count==0,"cancel immediately clears visual recipe");
        Check(!recipe.Build(start,start,true,0),"zero aim length hidden");Check(!recipe.Build(start,new Float3(100,100,100),true,0),"out-of-source-range bounded");
        Check(!recipe.Build(new Float3(float.NaN,0,0),end,true,0),"nonfinite hand rejected");Check(!recipe.Build(start,end,true,double.NaN),"nonfinite clock rejected");
        Console.WriteLine("PASS "+count+" source construction lightning geometry/animation cases; CPU only.");
    }
}
