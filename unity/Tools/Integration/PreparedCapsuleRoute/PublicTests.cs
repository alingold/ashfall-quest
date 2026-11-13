using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.MotionRoutes;
class PublicTests
{
 static int n;static void A(bool b,string s){n++;if(!b)throw new Exception("FAIL "+n+" "+s);}
 static CommandContext C(string a,string id,long t=0){return new CommandContext("s",a,id,t,7,true);}
 static CapsulePlan P(string id="pod",float x=0){return new CapsulePlan(id,0,0,new Float3(x,0,0),new Float3(x,1,0),new Float3(x,2,1),new Float3(x,0,2),new Float3(x,0,3),new Float3(0,0,-1),2);}
 static SyntheticCapsuleRoutes O(){var o=new SyntheticCapsuleRoutes("s",7);A(o.RegisterActor("a","body-a",1,1),"register a");A(o.RegisterActor("b","body-b",1,1),"register b");return o;}
 static ReservedCapsuleRoute R(SyntheticCapsuleRoutes o,string actor="a",string pod="pod",long expires=20){var p=P(pod);ReservedCapsuleRoute r;A(o.Reserve(actor,p,ReservedCapsuleRoute.Encode(p),expires,out r).Accepted,"reserve");return r;}
 static void Commit(PreparedRouteUpdate p){A(p.Validate().Accepted,"validate");p.CommitValidated();p.Abort();A(!p.Validate().Accepted,"consumed");}
 static void RefusedCommit(PreparedRouteUpdate p){bool refused=false;try{p.CommitValidated();}catch(InvalidOperationException){refused=true;}A(refused,"stale direct commit refused");p.Abort();}
 static void Main()
 {
  ReservedCapsuleRoute r;A(!SyntheticCapsuleRoutes.PrepareProduction(out r).Accepted&&r==null,"production refused");
  var o=O();r=R(o);A(r.ValidateRoute("a",r.Plan),"exact route");A(!r.ValidateRoute("a",P()),"same fields foreign route");A(!r.ValidateRoute("b",r.Plan),"foreign actor");A(r.BodyId=="body-a"&&r.Life==1&&r.Connection==1&&r.Epoch==7,"binding");
  var bytes=ReservedCapsuleRoute.Encode(P());bytes[bytes.Length-1]^=1;A(!o.Reserve("a",P(),bytes,20,out r).Accepted,"changed source");
  var far=P("far",21);A(!o.Reserve("a",far,ReservedCapsuleRoute.Encode(far),20,out r).Accepted,"bounded geometry");
  var blocked=P("wall",10);A(!o.Reserve("a",blocked,ReservedCapsuleRoute.Encode(blocked),20,out r).Accepted,"actual immutable structural geometry");
  r=R(o);PreparedRouteUpdate p;A(o.PrepareBegin(C("a","start"),r,out p).Accepted,"begin");p.Abort();p.Abort();A(!p.Validate().Accepted,"abort");A(o.PrepareBegin(C("a","start"),r,out p).Accepted,"retry same command");Commit(p);
  A(!o.PrepareBegin(C("a","start"),r,out p).Accepted,"begin replay");
  o.AdvanceClock(1);A(o.PrepareAdvance(C("a","advance",1),false,out p).Accepted,"advance");A(p.Update.State==CapsuleState.Flying&&p.Update.RootPosition.Y>0,"real plan trajectory");Commit(p);
  A(!o.PrepareAdvance(C("a","advance",1),false,out p).Accepted,"advance replay");
  var rb=R(o,"b","pod-b");A(o.PrepareBegin(C("b","bstart",1),rb,out p).Accepted,"b begin");Commit(p);
  A(o.PrepareResetOwner(C("a","reset",1),1,FlightLifecycle.Death,out p).Accepted,"reset");Commit(p);A(!r.ValidateRoute("a",r.Plan),"reset revokes a");A(rb.ValidateRoute("b",rb.Plan),"actor isolated");
  o.AdvanceClock(2);A(o.PrepareAdvance(C("b","bland",2),false,out p).Accepted,"b unaffected");Commit(p);A(!o.PrepareAdvance(C("a","dead",2),false,out p).Accepted,"dead a");
  A(o.PrepareResetOwner(C("a","respawn",2),2,FlightLifecycle.Respawn,out p).Accepted,"respawn");Commit(p);o.AdvanceClock(3);r=R(o);A(o.PrepareBegin(C("a","newride",3),r,out p).Accepted,"restart");Commit(p);
  o.AdvanceClock(5);A(o.PrepareAdvance(C("a","land",5),false,out p).Accepted,"land");A(p.Update.State==CapsuleState.Landed&&p.Update.RootPosition.Z==3,"actual landing");Commit(p);
  o=O();r=R(o);A(o.PrepareBegin(C("a","stale"),r,out p).Accepted,"prepare expiry");o.AdvanceClock(21);A(!r.ValidateRoute("a",r.Plan)&&!p.Validate().Accepted,"expiry");p.Abort();
  o=O();r=R(o);A(o.PrepareBegin(C("a","geometry"),r,out p).Accepted,"prepare geometry");o.ReplaceGeometry();A(!p.Validate().Accepted,"changed geometry");RefusedCommit(p);
  o=O();r=R(o);A(o.PrepareBegin(C("a","body"),r,out p).Accepted,"prepare body");A(o.ReplaceActor("a","body-new",1,2),"replace body connection");A(!r.ValidateRoute("a",r.Plan)&&!p.Validate().Accepted,"body/lease replaced");RefusedCommit(p);
  o=O();r=R(o);A(o.PrepareBegin(C("a","worldstale"),r,out p).Accepted,"prepare world");A(p.Validate().Accepted,"validated before revoke");o.RevokeWorld();RefusedCommit(p);A(!r.ValidateRoute("a",r.Plan),"world revoke");A(!o.PrepareBegin(C("a","revoked"),r,out p).Accepted,"revoked begin");
  o=O();r=R(o);A(o.PrepareBegin(C("a","leasestale"),r,out p).Accepted,"prepare lease");r.Revoke();RefusedCommit(p);A(!o.PrepareBegin(C("a","lease"),r,out p).Accepted,"lease revoked");
  o=O();r=R(o);A(!o.PrepareBegin(new CommandContext("s","a","bad",0,8,true),r,out p).Accepted,"epoch");A(!o.PrepareBegin(new CommandContext("s","a","bad",0,7,false),r,out p).Accepted,"client");
  o=O();r=R(o);A(o.PrepareBegin(C("a","ejectstart"),r,out p).Accepted,"eject begin");Commit(p);o.AdvanceClock(1);A(o.PrepareAdvance(C("a","eject",1),true,out p).Accepted,"eject");A(p.Update.State==CapsuleState.Released,"released");Commit(p);A(!o.PrepareAdvance(C("a","repeat-eject",1),true,out p).Accepted,"released no repeat");
  o=new SyntheticCapsuleRoutes("s",7);for(int i=0;i<256;i++){string a="actor"+i;A(o.RegisterActor(a,"body"+i,1,1),"capacity register");r=R(o,a,"pod"+i);A(o.PrepareBegin(C(a,"start"),r,out p).Accepted,"fleet capacity begin");Commit(p);}A(!o.RegisterActor("overflow","overflow",1,1),"fleet capacity reject");
  Console.WriteLine("PASS "+n);
 }
}



