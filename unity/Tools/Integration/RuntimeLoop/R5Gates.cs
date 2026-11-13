using System;
using Ashfall.Integration.RuntimeLoop;
partial class Scenario
{
 public void RunR5(){SetupR2();tick=1;Now=.1;Ashfall.Integration.RuntimeLoop.ActorResult[] result;
 var first=A("a",RuntimeAction.Fire,null,true,true);first.MotionMode=RuntimeMotionMode.PlayerFlightClimb;
 var image=health.CreateCheckpoint(C("host"),new string('a',64));
 Check(!loop.Tick(C("host"),.1f,new[]{first,A("b")},out result).Accepted,"R5 full composition rejects uncertified root");
 Check(loop.LastDisposition==TickDisposition.RejectedBeforeEffects&&a.Commits+b.Commits==0,"R5 rejection before either root");
 Check(Convert.ToBase64String(image)==Convert.ToBase64String(health.CreateCheckpoint(C("host"),new string('a',64))),"R5 rejection preserves exact Health image");
 first.MotionMode=RuntimeMotionMode.Capsule;Check(!loop.Tick(C("host"),.1f,new[]{first,A("b")},out result).Accepted,"R5 capsule cannot borrow legacy root");
 first.MotionMode=(RuntimeMotionMode)99;Check(!loop.Tick(C("host"),.1f,new[]{first,A("b")},out result).Accepted,"R5 unknown mode rejects");
 Check(loop.Tick(C("host"),.1f,new[]{A("a"),A("b")},out result).Accepted&&a.Commits+b.Commits==2,"R5 same tick corrected mode input retry");
 Console.WriteLine("R5 loop complete "+checks+" PASS; no full motion activation");}
}
class LoopR5Tests{static int Main(){try{new Scenario().RunR5();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}}
