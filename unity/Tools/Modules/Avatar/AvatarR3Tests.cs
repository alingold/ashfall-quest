using System;
using System.IO;
using System.Globalization;
using Ashfall.Modules.Avatar;
class AvatarR3Tests
{
    static int checks;
    static void Check(bool b,string name){if(!b)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Near(float a,float b,float eps=0.0001f){if(Math.Abs(a-b)>eps)throw new Exception("Expected "+b+", got "+a);}
    static void Reject(Action a,string name){try{a();}catch(AvatarValidationException){Check(true,name);return;}throw new Exception("Accepted "+name);}
    static PreparedPart Part(byte[] bytes,string node,string slots="default") {
        var asset=new VerifiedAsset(new AssetRecord{Id="test",AbsolutePath=Path.GetFullPath("test.gltf"),Sha256=ManifestXml.Sha(bytes)},(byte[])bytes.Clone());
        return new PreparedPart(new PartRecord{Label="LeftHand",Node=node,MaterialSlots=slots,Mount=SkinMatrix.Identity()},asset);
    }
    static PreparedPart Part(string file,string node){return Part(File.ReadAllBytes(file),node);}
    sealed class Deps:ISkinDependencies {
        public byte[] bytes=File.ReadAllBytes("rig.bin");public bool bad,changedClosure; public string ExpectedClosureSha256 {get {return File.ReadAllText("external.closure");}}
        public VerifiedAsset Resolve(string uri){if(uri!="rig.bin")throw new Exception("Unexpected URI");var b=(byte[])bytes.Clone();if(bad)b[0]^=1;return new VerifiedAsset(new AssetRecord{Sha256=ManifestXml.Sha(changedClosure?b:bytes)},b);}
    }
    static void Main(){
        foreach(string side in new[]{"L","R"}){
            string[] e=File.ReadAllLines(side+".expected");string name=e[0];var p=Part(side+".gltf",name);var s=VerifiedSkinReader.Read(p);var g=VerifiedSkinReader.Read(Part(side+".glb",name));
            Check(s.CopyVertices().Length/3==Int32.Parse(e[1])&&s.CopyIndices().Length==Int32.Parse(e[2])&&s.CopyJoints().Length==Int32.Parse(e[3])&&s.CopyParents().Length==Int32.Parse(e[4]),side+" real structure counts");
            for(int k=0;k<3;k++)Near(s.CopyVertices()[k],Single.Parse(e[5+k],CultureInfo.InvariantCulture));
            for(int k=0;k<3;k++)Check(s.CopyIndices()[k]==Int32.Parse(e[8+k]),side+" independent once-only winding "+k);
            var vs=s.CopyVertices();var gs=g.CopyVertices();for(int k=0;k<vs.Length;k++)Near(vs[k],gs[k],0);Check(true,side+" glTF -> GLB geometry round trip");
            var rest=s.Deform(s.CopyLocalMatrices());float max=0;for(int k=0;k<vs.Length;k++)max=Math.Max(max,Math.Abs(vs[k]-rest[k]));Console.WriteLine(side+" max rest error "+max);Check(max<0.0001,side+" real inverse bind rest pose");
            var pose=s.CopyLocalMatrices();var joints=s.CopyJoints();int joint=joints[2];var delta=SkinMatrix.Identity();delta[0]=0;delta[1]=-1;delta[4]=1;delta[5]=0;pose[joint]=SkinMatrix.Multiply(pose[joint],delta);
            var bent=s.Deform(pose);float motion=0;for(int k=0;k<bent.Length;k++)motion=Math.Max(motion,Math.Abs(bent[k]-rest[k]));Check(motion>0.001,side+" genuine finger pose deforms mesh");
            var gb=g.Deform(pose);for(int k=0;k<gb.Length;k++)Near(gb[k],bent[k],0.000001f);Check(true,side+" posed GLB round trip");
            var weights=s.CopyWeights();for(int v=0;v<weights.Length/4;v++)Near(weights[v*4]+weights[v*4+1]+weights[v*4+2]+weights[v*4+3],1,0.000001f);Check(true,side+" normalized four influences");
            var copy=s.CopyLocalMatrices();copy[0][0]=999;var ix=s.CopyIndices();ix[0]=999999;Check(s.CopyLocalMatrices()[0][0]!=999&&s.CopyIndices()[0]!=999999,side+" deep immutable arrays");
            Reject(()=>s.RequireBinding(Part(side+".gltf","other")),side+" node substitution");Reject(()=>s.RequireBinding(Part(File.ReadAllBytes(side+".gltf"),name,"other")),side+" material substitution");
            foreach(string kind in new[]{"cycle","joint","weight","range","uri","duplicate","sparse","missing","stride","normalized","scheme","encoded","root","zero","nan"})Reject(()=>VerifiedSkinReader.Read(Part(side+"-"+kind+".gltf",name)),side+" "+kind+" negative");
        }
        var a=VerifiedSkinReader.Read(Part("analytic.gltf","mesh"));var orig=a.CopyVertices();var rp=a.Deform(a.CopyLocalMatrices());for(int i=0;i<orig.Length;i++)Near(rp[i],orig[i]);Check(true,"analytic translated mesh and inverse bind rest");
        var ap=a.CopyLocalMatrices();ap[1]=new float[]{0,-1,0,1,1,0,0,0,0,0,1,0,0,0,0,1};var output=a.Deform(ap);
        // vertex (2,0,2): joint1 rotates (1,0,4) about its origin to (0,1,4), then mesh inverse.
        Near(output[3],1.25f);Near(output[4],0.75f);Near(output[5],-2);Check(true,"hand calculated two-joint weighted bend");
        var nan=a.CopyLocalMatrices();nan[0][3]=Single.NaN;Reject(()=>a.Deform(nan),"NaN pose rejects");
        var normalized=VerifiedSkinReader.Read(Part("normalized.gltf","mesh"));Near(normalized.CopyWeights()[0],64f/255);Near(normalized.CopyWeights()[1],191f/255);var nr=normalized.Deform(normalized.CopyLocalMatrices());for(int i=0;i<nr.Length;i++)Near(nr[i],orig[i]);Check(true,"UNORM byte weights decode and rest skinning");
        var dep=new Deps();var external=VerifiedSkinReader.Read(Part("external.gltf","mesh"),dep);Near(external.CopyVertices()[0],orig[0]);Check(true,"external immutable dependency");
        dep.bad=true;Reject(()=>VerifiedSkinReader.Read(Part("external.gltf","mesh"),dep),"external hash substitution");dep.changedClosure=true;Reject(()=>VerifiedSkinReader.Read(Part("external.gltf","mesh"),dep),"valid alternate asset fails closure receipt");Reject(()=>VerifiedSkinReader.Read(Part("external.gltf","mesh")),"missing dependency rejects");
        var glb=File.ReadAllBytes("analytic.glb");for(int i=0;i<glb.Length;i++){var prefix=new byte[i];Buffer.BlockCopy(glb,0,prefix,0,i);try{VerifiedSkinReader.Read(Part(prefix,"mesh"));throw new Exception("Accepted truncation "+i);}catch(AvatarValidationException){}catch(System.Text.DecoderFallbackException){}}
        Check(true,"all GLB truncated prefixes reject");
        Console.WriteLine("TOTAL "+checks+" checks; actual Unity API/render/hardware UNKNOWN");
    }
}



