using System;
using System.IO;
namespace Ashfall.Integration.MapNative
{
    // Decoder preflight, not a JPEG entropy decoder or content approval. Only baseline,
    // single interleaved scan, 8-bit grayscale/YCbCr; profiles/orientation/CMYK reject.
    public static class BoundedJpeg
    {
        static void Need(bool b){if(!b)throw new InvalidDataException("unsupported or corrupt bounded JPEG");}
        static int U(byte[] b,int p){return (b[p]<<8)|b[p+1];}
        public static long Inspect(byte[] b,int maxDimension,out int width,out int height)
        {
            width=height=0;Need(b!=null&&b.Length>=4&&b.Length<=16777216&&maxDimension>0&&maxDimension<=4096);
            Need(b[0]==255&&b[1]==216);int p=2,components=0;bool frame=false,scan=false,quant=false,huffman=false;int[] ids=null;
            while(p<b.Length){
                Need(b[p++]==255);while(p<b.Length&&b[p]==255)p++;Need(p<b.Length);int marker=b[p++];
                if(marker==217){Need(scan&&p==b.Length);return checked((long)width*height*4);}
                Need(!scan&&marker!=0&&marker!=216&&marker!=1&&!(marker>=208&&marker<=215)&&b.Length-p>=2);
                int n=U(b,p);Need(n>=2&&n<=b.Length-p);int s=p+2,end=p+n;
                if(marker==192){
                    Need(!frame&&n>=11&&b[s]==8);height=U(b,s+1);width=U(b,s+3);components=b[s+5];
                    Need(width>0&&height>0&&width<=maxDimension&&height<=maxDimension&&(components==1||components==3)&&n==8+3*components);
                    ids=new int[components];for(int i=0;i<components;i++){int k=s+6+3*i;ids[i]=b[k];for(int j=0;j<i;j++)Need(ids[j]!=ids[i]);int h=b[k+1]>>4,v=b[k+1]&15;Need(h>=1&&h<=4&&v>=1&&v<=4&&b[k+2]<=3);}frame=true;
                }else if(marker==219){Need(n>=67);quant=true;}
                else if(marker==196){Need(n>=20);huffman=true;}
                else if(marker==221){Need(n==4);}
                else if(marker==224){Need(n==16&&b[s]==74&&b[s+1]==70&&b[s+2]==73&&b[s+3]==70&&b[s+4]==0&&b[s+12]==0&&b[s+13]==0);}
                else if(marker==218){
                    Need(frame&&quant&&huffman&&n==6+2*components&&b[s]==components);
                    for(int i=0;i<components;i++){Need(b[s+1+2*i]==ids[i]&&(b[s+2+2*i]>>4)<=3&&(b[s+2+2*i]&15)<=3);}
                    Need(b[end-3]==0&&b[end-2]==63&&b[end-1]==0);scan=true;p=end;bool entropy=false;
                    while(p<b.Length){if(b[p++]!=255){entropy=true;continue;}Need(p<b.Length);int m=b[p++];if(m==0){entropy=true;continue;}if(m>=208&&m<=215)continue;if(m==217){Need(entropy&&p==b.Length);return checked((long)width*height*4);}Need(false);}Need(false);
                }else Need(false);
                p=end;
            }
            throw new InvalidDataException("JPEG missing terminal scan");
        }
    }
    public sealed class SourceSamplerR4
    {
        public readonly int WrapS,WrapT;public readonly bool Mipmaps,Trilinear,Point;
        public SourceSamplerR4(int mag,int min,int wrapS,int wrapT)
        {
            if((wrapS!=10497&&wrapS!=33071&&wrapS!=33648)||(wrapT!=10497&&wrapT!=33071&&wrapT!=33648))throw new InvalidDataException("sampler wrap");
            // Unity has one within-level filter, so mixed min/mag and nearest-mip-linear
            // are refused rather than approximated. glTF absent filters are unspecified.
            if(!((mag==9728&&(min==9728||min==9984))||(mag==9729&&(min==9729||min==9987))))throw new InvalidDataException("sampler cannot be represented exactly by Unity Texture filterMode");
            WrapS=wrapS;WrapT=wrapT;Point=mag==9728;Mipmaps=min==9984||min==9987;Trilinear=min==9987;
        }
        public long DecodedBytes(int width,int height)
        {
            if(width<1||height<1||width>4096||height>4096)throw new InvalidDataException("texture shape");
            long n=0;do{n=checked(n+(long)width*height*4);if(!Mipmaps||width==1&&height==1)return n;width=Math.Max(1,width/2);height=Math.Max(1,height/2);}while(true);
        }
    }
}
