using System;
using System.IO;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Modes;
using Ashfall.Integration.InventoryRecovery;
using Ashfall.Integration.HostRecovery;
using Ashfall.Integration.MatchRecovery;
using RoundController=Ashfall.Integration.MatchRounds.RoundController;
class Tests
{
 sealed class Facts:IAuthorityGate,IHealthWorld,IInteractionQuery,IInventoryTransactions,ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement,IModeRoster,IPickupActors,IPickupTransactions
 {
  public double Now{get;set;}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
  public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("authority");}
  public bool TryGetPose(string id,out RescuePose pose){pose=default(RescuePose);return false;}
  public bool HasLineOfSight(string a,string b,Float3 p,Float3 q,float range){return true;}
  public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] a){return RuleResult.Accept();}
  sealed class Receipt:IInventoryReceipt{public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
  public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt receipt){receipt=new Receipt();return RuleResult.Accept();}
  public string Issue(){return "fixture-secret";}public bool CanManage(CommandContext c){return c.ActorId=="host";}
  public SpawnSite[] Sites(string map,bool warmup){return new[]{new SpawnSite("floor",new Float3(1,0,2),-1,true,false)};}
  public bool IsSafe(string map,string actor,Float3 feet){return true;}
  public bool TryGet(string id,out ModeActor a){a=new ModeActor(id,id,0,"alive",true,false);return id=="a"||id=="b";}
  public ModeActor[] Snapshot(){ModeActor a,b;TryGet("a",out a);TryGet("b",out b);return new[]{a,b};}
  public bool TryRead(string id,out ActorFrame a){a=null;return false;}
  public bool TryCommit(Transfer t){return false;}public bool TryStageStore(Transfer t,out IStagedStore s){s=null;return false;}
  public bool TryPlaceDrop(string a,string i,out Float3 p){p=default(Float3);return false;}public ShieldProcessing TryAddShield(string a,float n){return ShieldProcessing.Failed;}
 }
 static int count;static Facts facts=new Facts{Now=4};static ItemCatalog catalog=SourceCatalog.Create();static ModeRules rules=new ModeRules("respawn",0,10,100,2);
 static CommandContext C(string a,string id,long tick){return new CommandContext("bundle",a,id,tick,0,true);}
 static BundleBinding B(long generation=1,double time=4){return new BundleBinding(new CheckpointBinding(new string('b',64),ColdInventoryCheckpoint.CatalogFingerprint(catalog),"bundle","a",0,0,0,1),new string('a',64),generation,1,0,time);}
 static void Check(bool ok,string label){count++;if(!ok)throw new Exception(label);}
 static void Refuse(Action a,string label){bool rejected=false;try{a();}catch(InvalidDataException){rejected=true;}catch(IOException){rejected=true;}catch(ArgumentException){rejected=true;}Check(rejected,label);}
 static ValidatedPartialBundle V(byte[] b,string hash,BundleBinding binding){return CheckpointBundle.ValidatePartial(b,hash,binding,1,1,catalog,facts,facts,facts,facts,0,true,rules,facts,32,facts,facts,facts,facts,facts,facts);}
 static byte[] Capture(){var h=new HealthService("bundle",0,0,true,facts,facts,facts);Check(h.Register(C("host","register",1),"a",-1,false).Accepted,"health register");var s=new Session("bundle","content",new SessionRules("map","respawn","maphash",0,8,true,false,true,true,1),facts,facts,facts,facts,facts);string token;Check(s.Join(C("a","join",1),new JoinRequest(Protocol.Name,Protocol.Schema,"content","Alice",null,false),out token).Accepted,"Session join");var round=new RoundController(rules,facts,"host","bundle",0,facts,32);Check(round.StartRound(C("host","start",1),4).Accepted,"start round");Check(round.RecordKnockdown(C("host","death",2),new Knockdown("a","b",1),4).Accepted,"score");var inv=new Inventory(catalog,facts,"bundle","a",0,false);Check(inv.Apply(C("a","admit",1),InventoryOperation.Admit,new ItemInstance("w",catalog.Weapon(0).Id,1),null).Accepted,"actual inventory weapon");var weapon=new WeaponHandling("bundle","a",0,WeaponMechanism.ServiceRifle,new FiniteAmmoReserve(100,31),facts);var p=new PickupRules("bundle",0,60,facts,facts,facts,facts,4096,64,8192);Check(p.Register("banana1","banana","source/banana",new Float3(0,0,2)),"actual Pickup source");
 var members=new[]{new WeaponCheckpointMember("w","grant/provenance",weapon)};
 Check(inv.Apply(C("a","admit-banana",2),InventoryOperation.Admit,new ItemInstance("inventory-banana","banana",1),null).Accepted,"actual consumable inventory");
 Inventory.PreparedRemoval pending;Check(inv.PrepareRemoval(C("a","remove",3),"inventory-banana",out pending)&&pending.Validate(),"validated actual reservation");
 bool refused=false;try{CheckpointBundle.CapturePartial(B(),h,new[]{h.Read("a")},new HealthTransition[0],s,round,catalog,inv,members,p);}catch(InvalidDataException){refused=true;}Check(refused,"prepared capture refuses");pending.Abort();
 return CheckpointBundle.CapturePartial(B(),h,new[]{h.Read("a")},h.DrainTransitions(),s,round,catalog,inv,members,p);}
 static byte[] MutateOwner(byte[] image,int owner,bool trailing){
  byte[][] parts=new byte[5][];string[] names=new string[5];using(var r=RecoveryWire.Reader(image)){r.ReadInt32();r.ReadInt32();RecoveryWire.Text(r);B().Owner.Check(r);RecoveryWire.Text(r);r.ReadInt64();r.ReadInt64();r.ReadInt64();r.ReadDouble();r.ReadInt32();for(int i=0;i<5;i++){names[i]=RecoveryWire.Text(r);RecoveryWire.Text(r);parts[i]=RecoveryWire.Blob(r);}}
  if(trailing){byte[] value=new byte[parts[owner].Length+1];Array.Copy(parts[owner],value,parts[owner].Length);parts[owner]=value;}else parts[owner][0]^=127;
  using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(0x31425248);w.Write(1);RecoveryWire.Text(w,CheckpointBundle.Codec);B().Owner.Write(w);RecoveryWire.Text(w,B().HealthCombatPublicationDigest);w.Write(1L);w.Write(1L);w.Write(0L);w.Write(4.0);w.Write(5);for(int i=0;i<5;i++){RecoveryWire.Text(w,names[i]);RecoveryWire.Text(w,RecoveryWire.Hash(parts[i]));RecoveryWire.Blob(w,parts[i]);}w.Flush();return m.ToArray();}
 }
 static void InspectActualOwners(byte[] image){
  byte[][] parts=new byte[5][];using(var r=RecoveryWire.Reader(image)){r.ReadInt32();r.ReadInt32();RecoveryWire.Text(r);B().Owner.Check(r);RecoveryWire.Text(r);r.ReadInt64();r.ReadInt64();r.ReadInt64();r.ReadDouble();Check(r.ReadInt32()==5,"fixed owner count");for(int i=0;i<5;i++){RecoveryWire.Text(r);RecoveryWire.Text(r);parts[i]=RecoveryWire.Blob(r);}RecoveryWire.End(r);}
  HealthService health;Check(HealthService.TryRestoreCheckpoint(C("host","restore",100),B().Owner.Build,RecoveryWire.Hash(parts[0]),parts[0],0,true,facts,facts,facts,out health),"actual Health cold factory");Check(health.Read("a").Id=="a"&&health.Read("a").Health==100,"actual Health retained");Check(!health.Register(C("host","register",2),"a",-1,false).Accepted,"actual Health replay");
  Session session;RoundController round;MatchOwnerCheckpoint.Reconstruct(parts[2],RecoveryWire.Hash(parts[2]),new MatchOwnerBinding(B().Owner,B().HealthCombatPublicationDigest,1,0,4),rules,facts,"host",32,facts,facts,facts,facts,facts,out session,out round);string token;
  Check(session.Join(C("a","join",2),null,out token).Reason=="replay","actual cold Session replay");Check(round.PersonalScore("b")==1,"actual cold score total");Check(!round.RecordKnockdown(C("host","fresh",3),new Knockdown("a","b",1),4).Accepted,"actual cold death ledger");
  QuarantinedInventory inv;Check(ColdInventoryCheckpoint.TryRestore(parts[3],new TrustedCheckpointReceipt(B().Owner,RecoveryWire.Hash(parts[3]),1),catalog,facts,facts,out inv),"actual cold Inventory factory");Check(inv.Inventory.Items.Length==2&&inv.Reserve("w").Available==31,"actual weapon reserve and membership");Check(!inv.Inventory.Apply(C("a","admit-banana",3),InventoryOperation.Remove,null,"inventory-banana").Accepted,"actual cold Inventory replay");
  var pickup=PickupCheckpointCodec.Restore(parts[4],B().Owner,facts,facts,facts,facts);CanonicalPickupSource source;Check(pickup.IsQuarantined&&pickup.TryReadCanonicalSource("banana1",out source),"actual pickup source and quarantine");
 }
 static int Main(string[] args){try{
  string path=Path.Combine(args[1],"bundle.bin");byte[] image;
  if(args[0]=="capture"){image=Capture();using(var f=new FileStream(path,FileMode.CreateNew))f.Write(image,0,image.Length);Console.WriteLine("capture "+image.Length+" bytes "+RecoveryWire.Hash(image));}
  else image=File.ReadAllBytes(path);
  string hash=RecoveryWire.Hash(image);
  if(args[0]=="mutant-missing-binding"){Refuse(()=>V(image,hash,B(2)),"mixed generation accepted");return 0;}
  if(args[0]=="mutant-missing-digest"){Refuse(()=>V(image,new string('f',64),B()),"wrong receipt accepted");return 0;}
  InspectActualOwners(image);var result=V(image,hash,B());Check(result.Digest==hash,"actual owner codecs validate");var copy=result.CopyOriginalBytes();copy[0]^=1;Check(result.Digest==hash,"output immutable");
  Refuse(()=>V(image,new string('f',64),B()),"trusted digest substitution");Refuse(()=>V(image,hash,B(2)),"mixed generation");Refuse(()=>V(image,hash,B(1,5)),"mixed time");facts.Now=5;Refuse(()=>V(image,hash,B()),"runtime clock mismatch");facts.Now=4;
  byte[] trail=new byte[image.Length+1];Array.Copy(image,trail,image.Length);Refuse(()=>V(trail,RecoveryWire.Hash(trail),B()),"unknown trailing field");
  for(int i=0;i<image.Length;i++){byte[] cut=new byte[i];Array.Copy(image,cut,i);Refuse(()=>V(cut,RecoveryWire.Hash(cut),B()),"crash cut "+i);}
  for(int i=0;i<image.Length;i+=17){byte[] bad=(byte[])image.Clone();bad[i]^=127;Refuse(()=>V(bad,RecoveryWire.Hash(bad),B()),"malformed "+i);}
  for(int i=0;i<5;i++)for(int j=0;j<2;j++){byte[] altered=MutateOwner(image,i,j==1);Refuse(()=>V(altered,RecoveryWire.Hash(altered),B()),"owner malformed with recomputed hashes");}
  byte[] huge=new byte[CheckpointBundle.MaximumBytes+1];Refuse(()=>V(huge,RecoveryWire.Hash(huge),B()),"size cap");
  bool blocked=false;try{CheckpointBundle.RestoreWholeEffect();}catch(NotSupportedException e){blocked=e.Message.Contains("Combat");}Check(blocked,"whole effect refuses");Check(File.ReadAllBytes(path).Length==image.Length&&RecoveryWire.Hash(File.ReadAllBytes(path))==hash,"original physical bytes preserved");Console.WriteLine("PASS "+count+" observations (includes exhaustive byte cuts)");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
