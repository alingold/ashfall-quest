using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
public static class AdmissionTests
{
 static int count;
 static void Check(bool value){count++;if(!value)throw new Exception("admission observation "+count);}
 public static int Main()
 {
  var root=new Float3(1,2,3);var head=new Float3(1.3f,3.7f,3.2f);
  for(int i=-30;i<=30;i++)
  {
   var p=RootMotionSafety.Plan(root,head,new Float3(.1f,-.02f,.2f),i*.1f,true);
   Check(NativeRootAdmission.Valid(p,.02f));
   p.PivotCorrection=new Float3(p.PivotCorrection.X+.01f,p.PivotCorrection.Y,p.PivotCorrection.Z);Check(!NativeRootAdmission.Valid(p,.02f));
  }
  var good=RootMotionSafety.Plan(root,head,new Float3(),0,true);
  foreach(float s in new[]{float.NaN,float.PositiveInfinity,-1f,0f,.10001f})Check(!NativeRootAdmission.Valid(good,s));
  var bad=good;bad.Translation=new Float3(11,0,0);Check(!NativeRootAdmission.Valid(bad,.02f));
  bad=good;bad.YawRadians=4;Check(!NativeRootAdmission.Valid(bad,.02f));
  bad=good;bad.RootPosition=new Float3(float.PositiveInfinity,0,0);Check(!NativeRootAdmission.Valid(bad,.02f));
  bad=good;bad.HeadPosition=new Float3(float.NaN,0,0);Check(!NativeRootAdmission.Valid(bad,.02f));
  bad=good;bad.Translation=new Float3(.1f,0,0);bad.OwnsTranslation=false;Check(!NativeRootAdmission.Valid(bad,.02f));
  bad=good;bad.RootPosition=new Float3(float.MaxValue,0,0);bad.HeadPosition=new Float3(float.MaxValue,1,0);bad.Translation=new Float3(float.MaxValue,0,0);Check(!NativeRootAdmission.Valid(bad,.02f));
  bad=good;bad.RootPosition=new Float3(-float.MaxValue,0,-float.MaxValue);bad.HeadPosition=new Float3(float.MaxValue,1,float.MaxValue);bad.YawRadians=1.5f;Check(!NativeRootAdmission.Valid(bad,.02f));Check(NativeRootAdmission.Valid(good,.1f));Check(NativeRootAdmission.Valid(good,float.Epsilon));
  Console.WriteLine("PASS "+count+" actual public admission observations");return 0;
 }
}


