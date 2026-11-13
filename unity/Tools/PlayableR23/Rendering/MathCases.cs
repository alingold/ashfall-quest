using System;
using Ashfall.Integration.Playable.Rendering;
static class MathCases
{
    static int cases;
    static void Check(bool value, string message) { cases++; if (!value) throw new Exception(message); }
    static double Unbiased(double input)
    { return ((input * (.22 * input + .10 * .30) + .20 * .01) / (input * (.22 * input + .30) + .20 * .30)) - .01 / .30; }
    static void Main()
    {
        foreach(float input in new float[]{-1,0,.00001f,.18f,.5f,1,4,64})
        foreach(float exposure in new float[]{.5f,1})
        foreach(float white in new float[]{1,4})
        {
            double expected=Unbiased(Math.Max(0,(double)input*exposure)*2)/Unbiased((double)white*2);
            Check(Math.Abs(R23GodotFilmicMath.Linear(input,exposure,white)-expected)<2e-6,"independent bias/white normalization");
        }
        foreach(float input in new float[]{0,.0001f,.001f,.0031308f,.18f,1,4,64})
        {
            float encoded=R23GodotFilmicMath.SrgbEncode(input);
            Check(Math.Abs(R23GodotFilmicMath.SrgbDecode(encoded)-input)<Math.Max(2e-6,input*2e-6),"extended HDR encoding roundtrip");
            float display=R23GodotFilmicMath.GodotDisplayEncode(input);
            Check(Math.Abs(R23GodotFilmicMath.SrgbEncode(R23GodotFilmicMath.SrgbDecode(display))-display)<2e-6,"one final URP OETF precompensation");
        }
        Check(R23GodotFilmicMath.Linear(0)==0,"black");
        Check(R23GodotFilmicMath.Linear(1)==1,"source white1 maps to1");
        Check(R23GodotFilmicMath.Linear(4)>1,"source white1 preserves above-white result until display clip");
        Check(R23GodotFilmicMath.Linear(4)!=R23GodotFilmicMath.Linear(1),"preclipped LDR cannot recover HDR distinction");
        Check(Math.Abs(R23GodotFilmicMath.GodotDisplayEncode(.001f)-R23GodotFilmicMath.SrgbEncode(.001f))>.005,"source near-black transfer differs from IEC linear segment");
        Check(!R23GodotFilmicMath.ValidParameters(1,0),"undefined white0 refused");
        Check(!R23GodotFilmicMath.ValidParameters(Single.NaN,1),"nonfinite exposure refused");
        Check(!R23GodotFilmicMath.ValidParameters(1,Single.PositiveInfinity),"nonfinite white refused");
        Check(R23GodotFilmicMath.Linear(.5f,0,1)==0,"valid exposure0 black");
        Console.WriteLine(cases+" actual C# FILMIC/domain cases PASS; no engine or GPU invoked.");
    }
}
