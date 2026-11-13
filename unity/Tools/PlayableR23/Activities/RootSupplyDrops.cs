using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Integration.Playable.Activities;
class RootSupplyDrops
{
 static int count;static void Check(bool value,string why){count++;if(!value)throw new Exception(why);}
 sealed class World:IConstructionWorld
 {public bool HasStaticContact(Panel p,float t){return false;}public bool IsBuried(Panel p){return false;}public string Clearance(Panel p,string a){return "";}public bool HasLineOfSight(string a,Float3 p,Float3 q){return true;}}
 static void Main()
 {
  var session=new R23ConstructionSession(new World());Check(session.Balance==999,"actual source initial charge");
  Check(session.WithdrawSupply(6,1)==6&&session.Balance==993,"whole source box withdraws six from actual owner");
  Check(session.GrantSupply("pickup089:drop-full",6,2)==6&&session.Balance==999,"recollect full dropped source box");
  Check(session.GrantSupply("pickup089:drop-full",6,2)==0&&session.Balance==999,"same world box never grants twice");
  Check(session.WithdrawSupply(-1,3)==0&&session.Balance==999,"invalid request does not mutate balance");
  for(int i=0;i<166;i++)Check(session.WithdrawSupply(6,4+i)==6,"finite real owner withdrawal "+i);
  Check(session.Balance==3&&session.WithdrawSupply(6,170)==3&&session.Balance==0,"last partial box contains exact remainder");
  Check(session.WithdrawSupply(6,171)==0&&session.Balance==0,"empty owner cannot create a box");
  Check(session.GrantSupply("pickup089:drop-partial",3,172)==3&&session.Balance==3,"actual partial drop can be collected");
  Check(session.GrantSupply("pickup089:drop-partial",3,173)==0&&session.Balance==3,"partial receipt remains consumed");
  Check(session.GrantSupply("pickup089:oversize",7,174)==0&&session.GrantSupply("pickup089:empty",0,174)==0&&session.Balance==3,"invalid box amounts refuse without grant");
  Check(session.WithdrawSupply(1,171)==0&&session.Balance==3,"old owner tick refuses withdrawal");
  Check(session.WithdrawSupply(6,174)==3&&session.Balance==0,"accepted source remainder remains conserved after refusal");
  Console.WriteLine("PASS "+count+" real construction-owner full/partial supply drop, recollection, replay and refusal cases; no engine.");
 }
}
