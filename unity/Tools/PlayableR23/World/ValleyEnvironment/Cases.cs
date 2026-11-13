using System;
using System.IO;
using System.Security.Cryptography;
using Ashfall.Integration.Playable.World;
static class Cases
{
    static double Half(ushort bits){int e=(bits>>10)&31,m=bits&1023;return e==0?m*Math.Pow(2,-24):(1024+m)*Math.Pow(2,e-25);}
    static void Require(bool value,string name){if(!value)throw new Exception(name);}
    static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Require(rejected,"bad range accepted");}
    static int Main(string[] args)
    {
        try{
            byte[] packed=new byte[32*512*4],decoded=new byte[packed.Length*2];
            for(int e=0;e<32;e++)for(int m=0;m<512;m++){int i=e*512+m;uint word=(uint)(m|(m<<9)|(m<<18)|(e<<27));for(int b=0;b<4;b++)packed[i*4+b]=(byte)(word>>(b*8));}
            R23ValleyHdrCodec.ExpandRange(packed,decoded,0,packed.Length/4);
            for(int e=0;e<32;e++)for(int m=0;m<512;m++){int i=e*512+m;for(int c=0;c<3;c++)Require(Half((ushort)(decoded[i*8+c*2]|decoded[i*8+c*2+1]<<8))==m*Math.Pow(2,e-24),"RGBE9995 not lossless");Require(decoded[i*8+6]==0&&decoded[i*8+7]==0x3c,"alpha not one");}
            Console.WriteLine("PASS all 16,384 shared-exponent/mantissa combinations expand exactly to half, alpha1");
            Reject(delegate{R23ValleyHdrCodec.ExpandRange(new byte[3],new byte[6],0,0);});Reject(delegate{R23ValleyHdrCodec.ExpandRange(new byte[4],new byte[7],0,0);});Reject(delegate{R23ValleyHdrCodec.ExpandRange(new byte[4],new byte[8],-1,1);});Reject(delegate{R23ValleyHdrCodec.ExpandRange(new byte[4],new byte[8],0,2);});
            Console.WriteLine("PASS malformed HDR/range inputs rejected");
            byte[] actual=File.ReadAllBytes(args[0]),half=new byte[actual.Length*2];for(int start=0;start<actual.Length/4;start+=32768)R23ValleyHdrCodec.ExpandRange(actual,half,start,Math.Min(32768,actual.Length/4-start));
            using(var hash=SHA256.Create())Console.WriteLine("EXPANDED_SHA256="+BitConverter.ToString(hash.ComputeHash(half)).Replace("-","").ToLowerInvariant());
            return 0;
        }catch(Exception error){Console.WriteLine(error);return 1;}
    }
}
