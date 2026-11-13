using System;
using Ashfall.Integration.PersonalSettings.Engine;
public static class PoolTests
{
 static int n;
 static void Check(bool b,string name) { n++;if(!b)throw new Exception(name); }
 static void Throws(Action a,string name) { bool b=false;try{a();}catch(ArgumentException){b=true;}Check(b,name); }
 public static int Main() { try {
 Throws(()=>new NativeTargetFeedbackPool(0),"zero capacity");Throws(()=>new NativeTargetFeedbackPool(49),"source bound");
 var p=new NativeTargetFeedbackPool(48);
 int a=p.Emit("t",.01f,true);Check(p.Read(a).Text=="1","positive minimum");
 int b=p.Emit("t",2.5f,false);Check(a!=b&&p.Read(a).Active&&p.Read(b).Text=="3","concurrent midpoint away");
 Check(p.Read(a).ShieldHit&&!p.Read(b).ShieldHit,"event colors discriminant");
 p.Refresh(.20f);Check(Math.Abs(p.Read(a).Rise-.028f)<.00001f,"source rise");
 p.Refresh(.44f);Check(Math.Abs(p.Read(a).Alpha-.21f/.22f)<.00001f,"source fade");
 p.Refresh(.20f);Check(p.Read(a).Active,"alive before .85");p.Refresh(.02f);Check(!p.Read(a).Active&&!p.Read(b).Active,"source expiry");
 a=p.Emit("a",1,false);p.Refresh(.4f);b=p.Emit("b",9,false);p.Refresh(.5f);Check(!p.Read(a).Active&&p.Read(b).Active&&p.Read(b).Text=="9","independent clocks");
 Check(p.Emit(null,1,false)==-1&&p.Emit("",1,false)==-1,"unbound identity");
 foreach(float f in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})Check(p.Emit("x",f,false)==-1,"reject invalid damage");
 Throws(()=>p.Refresh(-1),"negative time");Throws(()=>p.Refresh(float.NaN),"nan time");Throws(()=>p.Refresh(float.PositiveInfinity),"infinite time");
 p.Clear();for(int i=0;i<48;i++)Check(!p.Read(i).Active,"clear");
 for(int i=0;i<48;i++)p.Emit("t"+i,i+1,false);int replaced=p.Emit("new",99,false);Check(replaced==0&&p.Read(1).Target=="t1"&&p.Read(0).Target=="new","oldest eviction");
 p.Clear();p.Refresh(0);for(int i=0;i<48;i++)Check(!p.Read(i).Active,"no reset reappearance");
 p.Emit("big",float.MaxValue,false);Check(p.Read(0).Text!="0"&&!p.Read(0).Text.Contains("E"),"finite large text");
 p.Dispose();p.Dispose();Check(p.Emit("retired",1,false)==-1,"terminal dispose");for(int i=0;i<48;i++)Check(!p.Read(i).Active,"dispose clears");
 Console.WriteLine("PASS "+n+" assertions external public pool consumer");return 0;
 } catch(Exception e){Console.WriteLine(e);return 1;} }
}
