using System;
using System.IO;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.HostRecovery;
partial class Tests
{
 static Session NewSession(){return new Session("bundle","content",new SessionRules("map","respawn","maphash",0,8,true,false,true,true,1),facts,facts,facts,facts,facts);}
 static HealthService NewHealth(){return new HealthService("bundle",0,0,true,facts,facts,facts);}
 static void BusyEvidence(Action action,string label){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(InvalidDataException){rejected=true;}Check(rejected,label);}
 static PartialOwnerEvidence Verify(Session s,Session.CheckpointClockReceipt sr,HealthService h,HealthService.CheckpointClockReceipt hr)
 {return PartialOwnerEvidence.Verify(s,sr,sr.CopyArchive(),h,hr,hr.CopyArchive(),hr.CopyRetainedRecords(),C("host","evidence",100),B().Owner);}
 static int Main(string[] args){try{
  facts.Now=4;var s=NewSession();var h=NewHealth();string token;
  Check(s.Join(C("a","join",1),new JoinRequest(Protocol.Name,Protocol.Schema,"content","Alice",null,false),out token).Accepted,"join");
  Check(h.Register(C("host","register",1),"a",-1,false).Accepted,"register");
  h.DrainTransitions();
  var sr=s.CaptureClockEvidence(B().Owner);var hr=h.CaptureClockEvidence(C("host","capture",100),B().Owner);
  Check(sr.RecordedTime==4&&hr.RecordedTime==4&&hr.EventSequence==1,"actual private admission clocks");
  var proof=Verify(s,sr,h,hr);Check(proof.SessionRecordedTime==4&&proof.HealthRecordedTime==4,"two-owner partial diagnostics");
  bool blocked=false;try{proof.RestoreCoherentBundle();}catch(NotSupportedException e){blocked=e.Message.Contains("PARTIAL");}Check(blocked,"full coherent restore always refused");
  var round=new Ashfall.Integration.MatchRounds.RoundController(rules,facts,"host","bundle",0,facts,32);Check(round.StartRound(C("host","round",1),4).Accepted,"bundle round");
  var inventory=new Ashfall.Modules.InventoryItems.Inventory(catalog,facts,"bundle","a",0,false);
  var pickups=new Ashfall.Modules.Pickups.PickupRules("bundle",0,60,facts,facts,facts,facts,4096,64,8192);
  var image=CheckpointBundle.CapturePartial(B(),h,new[]{h.Read("a")},new HealthTransition[0],s,round,catalog,inventory,new Ashfall.Integration.InventoryRecovery.WeaponCheckpointMember[0],pickups);
  var bundle=V(image,RecoveryWire.Hash(image),B());
  Check(PartialOwnerEvidence.VerifyBundlePartial(bundle,B(),s,sr,round,h,hr)!=null,"actual bundle bound to owner receipts");
  Refuse(()=>PartialOwnerEvidence.VerifyBundlePartial(bundle,B(2),s,sr,round,h,hr),"bundle generation substitution");
  var future=new Ashfall.Integration.MatchRounds.RoundController(rules,facts,"host","bundle",0,facts,32);Check(future.StartRound(C("host","future",1),10).Accepted,"actual future round");
  Refuse(()=>PartialOwnerEvidence.VerifyBundlePartial(bundle,B(),s,sr,future,h,hr),"actual Round archive substitution");
  var second=NewHealth();Check(second.Register(C("host","second",1),"a",-1,false).Accepted,"alternate actual records");
  var alternative=CheckpointBundle.CapturePartial(B(),h,new[]{second.Read("a")},second.DrainTransitions(),s,round,catalog,inventory,new Ashfall.Integration.InventoryRecovery.WeaponCheckpointMember[0],pickups);
  var alternativeBundle=V(alternative,RecoveryWire.Hash(alternative),B());
  Refuse(()=>PartialOwnerEvidence.VerifyBundlePartial(alternativeBundle,B(),s,sr,round,h,hr),"same-ID alternate retained archive rejected");
  facts.Now=10;Check(Verify(s,sr,h,hr)!=null,"external observation did not rewrite recorded owner time");
  Check(s.CaptureClockEvidence(B().Owner).RecordedTime==4&&h.CaptureClockEvidence(C("host","capture",100),B().Owner).RecordedTime==4,"capture is not admission");
  Check(h.Synchronize(C("host","advance",2)).Accepted,"real Health admission advances clock");
  var later=h.CaptureClockEvidence(C("host","capture",100),B().Owner);
  Refuse(()=>Verify(s,sr,h,later),"real mixed recorded time rejected despite same external Now");
  Refuse(()=>Verify(s,sr,h,hr),"stale owner receipt rejected");
  facts.Now=4;var h2=NewHealth();Check(h2.Register(C("host","register",1),"a",-1,false).Accepted,"second identical Health");
  h2.DrainTransitions();
  Refuse(()=>hr.Verify(h2,C("host","capture",100),B().Owner,hr.CopyArchive(),hr.CopyRetainedRecords()),"identical bytes foreign producer rejected");
  var empty=NewSession();var er=empty.CaptureClockEvidence(B().Owner);var h2r=h2.CaptureClockEvidence(C("host","capture",100),B().Owner);
  Refuse(()=>Verify(empty,er,h2,h2r),"empty Session actual census rejected");
  var wrongMember=NewSession();Check(wrongMember.Join(C("b","join-b",1),new JoinRequest(Protocol.Name,Protocol.Schema,"content","Bob",null,false),out token).Accepted,"actual wrong canonical member at same time");
  var wrongReceipt=wrongMember.CaptureClockEvidence(B().Owner);
  Check(wrongReceipt.RecordedTime==h2r.RecordedTime,"membership negative has equal clocks");
  Refuse(()=>Verify(wrongMember,wrongReceipt,h2,h2r),"actual equal-time wrong canonical membership refused");
  Refuse(()=>sr.Verify(empty,B().Owner,sr.CopyArchive()),"foreign Session producer rejected");
  var identical=NewSession();Check(identical.Join(C("a","join",1),new JoinRequest(Protocol.Name,Protocol.Schema,"content","Alice",null,false),out token).Accepted,"identical separate Session");
  Check(RecoveryWire.Hash(identical.CaptureCheckpoint(B().Owner))==sr.Digest,"separate Session archive identical");
  Refuse(()=>sr.Verify(identical,B().Owner,sr.CopyArchive()),"identical Session archive foreign producer refused");
  var alias=sr.CopyCanonicalMembers();alias[0]="evil";Check(sr.CopyCanonicalMembers()[0]=="a","Session census clone");
  alias=h2r.CopyCanonicalMembers();alias[0]="evil";Check(h2r.CopyCanonicalMembers()[0]=="a","Health census clone");
  var bytes=sr.CopyArchive();bytes[0]^=1;Refuse(()=>sr.Verify(s,B().Owner,bytes),"Session archive substitution");
  bytes=h2r.CopyRetainedRecords();bytes[0]^=1;Refuse(()=>h2r.Verify(h2,C("host","capture",100),B().Owner,h2r.CopyArchive(),bytes),"archive substitution");
  foreach(var b in new[]{new CheckpointBinding(B().Owner.Build,B().Owner.Catalog,"bundle","a",1,0,0,1),new CheckpointBinding(B().Owner.Build,B().Owner.Catalog,"bundle","a",0,1,0,1),new CheckpointBinding(B().Owner.Build,B().Owner.Catalog,"bundle","a",0,0,1,1),new CheckpointBinding(B().Owner.Build,B().Owner.Catalog,"bundle","a",0,0,0,2)})
  {var binding=b;Refuse(()=>sr.Verify(s,binding,sr.CopyArchive()),"epoch/life/connection/sequence substitution");}
  var original=sr.CopyArchive();for(int i=0;i<original.Length;i++){int n=i;var cut=new byte[n];Array.Copy(original,cut,n);Refuse(()=>sr.Verify(s,B().Owner,cut),"Session truncation "+n);}
  original=h2r.CopyRetainedRecords();for(int i=0;i<original.Length;i++){var cut=new byte[i];Array.Copy(original,cut,i);Refuse(()=>h2r.Verify(h2,C("host","capture",100),B().Owner,h2r.CopyArchive(),cut),"records truncation");}
  HealthService.PreparedAdvance advance;Check(h2.PrepareAdvance(C("host","pending",3),out advance).Accepted&&advance.Validate(),"actual validated Health token");
  BusyEvidence(()=>h2.CaptureClockEvidence(C("host","capture",100),B().Owner),"Health pending refuses evidence");advance.Abort();Check(h2.CaptureClockEvidence(C("host","capture",100),B().Owner)!=null,"Health Abort releases");
  Session.PreparedJoin join;Check(empty.PrepareJoinTrusted(new CommandContext("bundle","a","sessions-host/4",4,0,true),new JoinRequest(Protocol.Name,Protocol.Schema,"content","Alice",null,false),out join).Accepted&&empty.ValidateJoin(join).Accepted,"actual validated Session token");
  BusyEvidence(()=>empty.CaptureClockEvidence(B().Owner),"Session pending refuses evidence");empty.AbortJoin(join);Check(empty.CaptureClockEvidence(B().Owner)!=null,"Session Abort releases");
  // Cold process reads the exact owner archives produced in the warm process.
  string path=Path.Combine(args[1],"evidence-session.bin"),hp=Path.Combine(args[1],"evidence-health.bin");
  if(args[0]=="warm"){File.WriteAllBytes(path,sr.CopyArchive());File.WriteAllBytes(hp,h2r.CopyArchive());}
  var cold=Session.RestoreCheckpoint(File.ReadAllBytes(path),B().Owner,facts,facts,facts,facts,facts);HealthService coldHealth;
  Check(HealthService.TryRestoreCheckpoint(C("host","cold",100),B().Owner.Build,RecoveryWire.Hash(File.ReadAllBytes(hp)),File.ReadAllBytes(hp),0,true,facts,facts,facts,out coldHealth),"actual cold Health factory");
  var cr=cold.CaptureClockEvidence(B().Owner);var ch=coldHealth.CaptureClockEvidence(C("host","capture",100),B().Owner);
  Check(Verify(cold,cr,coldHealth,ch)!=null,"cold local partial evidence");Refuse(()=>sr.Verify(cold,B().Owner,cr.CopyArchive()),"cold incarnation cannot reuse warm receipt");
  Check(sr.Digest==RecoveryWire.Hash(File.ReadAllBytes(path)),"warm archive unchanged");
  Console.WriteLine("PASS owner evidence "+count+" observations "+args[0]+" (includes exhaustive truncation offsets)");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
