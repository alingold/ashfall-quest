using System;
using Ashfall.Modules.Avatar;
public static class AvatarLifecycleR6Tests
{
    static int checks;
    static void Need(bool value){checks++;if(!value)throw new Exception("R6 assertion "+checks);}
    static void Reject(Action action){try{action();}catch(AvatarValidationException){checks++;return;}throw new Exception("Expected geometry rejection");}
    public static void Main()
    {
        foreach(var part in AvatarR4Fixtures.Assembly().CopyParts())if(part.Label=="LeftHand"||part.Label=="RightHand") {
            var s=VerifiedSkinReader.Read(part);
            NormalMapGeometry.Validate(s.CopyVertices(),s.CopyNormals(),s.CopyUv(),s.CopyIndices());checks++;
            var uv=s.CopyUv();Array.Clear(uv,0,uv.Length);
            Reject(delegate{NormalMapGeometry.Validate(s.CopyVertices(),s.CopyNormals(),uv,s.CopyIndices());});
        }
        var v=new float[]{0,0,0,1,0,0,0,1,0};var n=new float[]{0,0,1,0,0,1,0,0,1};var u=new float[]{0,0,1,0,0,1};var ix=new[]{0,1,2};
        NormalMapGeometry.Validate(v,n,u,ix);checks++;
        NormalMapGeometry.Validate(v,n,u,new[]{0,2,1});checks++;
        NormalMapGeometry.Validate(v,n,u,new[]{0,0,0,0,1,2});checks++;
        Reject(delegate{NormalMapGeometry.Validate(v,n,new float[6],ix);});
        Reject(delegate{NormalMapGeometry.Validate(new float[9],n,u,ix);});
        Reject(delegate{NormalMapGeometry.Validate(v,n,u,new[]{0,1,3});});
        Reject(delegate{NormalMapGeometry.Validate(v,new float[9],u,ix);});
        var invalid=(float[])u.Clone();invalid[0]=float.NaN;
        Reject(delegate{NormalMapGeometry.Validate(v,n,invalid,ix);});
        invalid=(float[])u.Clone();invalid[0]=float.PositiveInfinity;
        Reject(delegate{NormalMapGeometry.Validate(v,n,invalid,ix);});
        Reject(delegate{NormalMapGeometry.Validate(null,n,u,ix);});
        Need(u[2]==1&&v[3]==1&&ix[1]==1);
        Console.WriteLine(checks+" R6 CPU assertions PASS; native lifetime remains coordinator-only");
    }
}
