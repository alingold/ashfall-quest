using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Modules.Construction;
using Ashfall.Integration.ConstructionGeometry;
using Ashfall.Integration.ConstructionRuntime;
public static class R2Holdouts
{
 sealed class Trust:IWorldApproval,IWorldBuildPolicy,IGeometryAttestation,IAuthorityGate
 {
  public string Hash,Body="review-body";public bool Approved=true,CurrentMap=true,AllowActor=true;public SurfaceRole Role=SurfaceRole.Structure;
  public bool IsApproved(ContentIdentity c){return Approved&&c.PayloadSha256==Hash&&c.MapId=="review"&&c.Version=="review/1";}
  public bool ApproveMaterial(ContentIdentity c,LoadedSurface s){return IsApproved(c)&&s.MaterialPath=="review-material";}
  public bool AllowsSpawn(ContentIdentity c,string m,string t,Float3 p){return false;}
  public bool Resolve(ContentIdentity c,string s,string o,out string body,out SurfaceRole role){body=Body;role=Role;return IsApproved(c);}
  public bool Current(ContentIdentity c){return CurrentMap&&IsApproved(c);}
  public RuleResult Validate(CommandContext c){return AllowActor&&c.ActorId=="actor"?RuleResult.Accept():RuleResult.Reject("lifecycle");}
 }
 sealed class Occupants:IRuntimeSnapshot
 {
  public long Revision=1;public bool ChangeOnRead;public ProtectedBox[] Players=new ProtectedBox[0];public Float3[] Objectives=new Float3[0];
  public long Generation{get{return Revision;}}
  public bool IsCurrent(long r){return r==Revision;}
  public bool TrySnapshot(out ProtectedBox[] p,out Float3[] o){p=Players;o=Objectives;if(ChangeOnRead)Revision++;return true;}
 }
 static readonly FixtureHostClock Clock=new FixtureHostClock("s");
 static readonly Dictionary<RuntimeHost,ConstructionLease> Leases=new Dictionary<RuntimeHost,ConstructionLease>();
 static bool Claim(RuntimeHost h,string a,string hand,BuilderState pose,long expires){ConstructionLease l;bool ok=h.Claim(a,hand,pose,expires,out l);if(ok)Leases[h]=l;return ok;}
 static RuleResult Place(RuntimeHost h,ConstructionCommand c){ConstructionLease l;Leases.TryGetValue(h,out l);return h.Place(c,l);}
 static BuildDecision Preview(RuntimeHost h,string a,long now){Clock.Now=now;return h.Preview(a,Leases[h]);}
 static int pass,fail;
 static void Check(bool b,string name){Console.WriteLine((b?"PASS ":"FAIL ")+name);if(b)pass++;else fail++;}
 static float S=Geometry.Side; static float TranslateX;
 static Surface Deck(){return new Surface{id="deck",semanticOwner="owner",colliderRole="structure",materialPath="review-material",matrix=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,TranslateX,0,0,1},vertices=new float[]{-20,-.08f,-20,20,-.08f,-20,-20,-.08f,20,20,-.08f,20},normals=new float[]{0,1,0,0,1,0,0,1,0,0,1,0},indices=new[]{0,2,1,1,2,3},uv=new float[0],color=new float[]{.5f,.5f,.5f,1},roughness=.5f};}
 static AttestedWorld Bind(Trust t,Occupants o,bool pad,Func<int,string> layers,string volumeKind=null)
 {
  var rows=new List<SemanticRecord>();rows.Add(new SemanticRecord{sourceId="deck",ownerId="owner",kind="structure",evidence="controlled reviewer geometry"});
  if(pad)rows.Add(new SemanticRecord{sourceId="pad",ownerId="pad-owner",kind="interaction",evidence="controlled authored volume",boxes=new[]{new AuthoredBox{center=new[]{S/2,0f,S/2},size=new[]{S,1f,S}}}});
  if(volumeKind!=null){rows[0].kind=volumeKind;rows[0].boxes=new[]{new AuthoredBox{center=new[]{0f,0f,0f},size=new[]{1f,1f,1f}}};}
  var d=new MapDocument{schema=1,id="review",version="review/1",sourcePath="C:/review.gd",sourceSha256=new string('b',64),sourceRevision="review-r1",approval="accepted",surfaces=new[]{Deck()},spawns=new Spawn[0],pads=pad?new[]{new Pad{id="pad",semanticOwner="pad-owner",position=new[]{S/2,0f,-S/2},normal=new[]{0f,1f,0f},radius=1,speed=10}}:new Pad[0],semantics=rows.ToArray(),semanticsVersion="world-semantics/1"};
  if(volumeKind!=null)d.surfaces[0].colliderRole="trigger";
  byte[] bytes;using(var s=new MemoryStream()){new DataContractJsonSerializer(typeof(MapDocument)).WriteObject(s,d);bytes=s.ToArray();}t.Hash=WorldLoader.Hash(bytes);
  return new AttestedWorld(ApprovedWorld.Load(bytes,t.Hash,t),t,t,layers,t,o,false,FloorBurialPolicy.LocalAllSamples025);
 }
 static string Layers(int i){return i==8?"WorldStructure":i==9?"WorldInteraction":i==10?"PlayerActor":"WorldDecoration";}
 static RuntimeHost Host(Trust t,Occupants o){Clock.Now=0;return new RuntimeHost("s",3,"review-host",t,Bind(t,o,false,Layers),null,Clock);}
 static BuilderState Pose(float x){return new BuilderState(new Float3(x,1,S/2),new Float3(0,-1,0),new Float3(x,1,S/2),true,true);}
 static ConstructionCommand Cmd(int n,long tick){return new ConstructionCommand(new CommandContext("s","actor","review/"+n,tick,3,true),n);}
 static bool Throws(Action a){try{a();return false;}catch(ArgumentException){return true;}catch(InvalidDataException){return true;}}
 public static void Main()
 {
  var t=new Trust();var o=new Occupants();var h=Host(t,o);
  Check(Claim(h,"actor","left",Pose(S/2),20),"claim bounded pose");
  Check(Preview(h,"actor",21)==null,"host observes expiry at tick21");
  var r=Place(h,Cmd(0,20));Console.WriteLine("OBS expiry accepted="+r.Accepted+" panels="+h.Snapshot().Length+" charges="+h.Charges("actor"));
  Check(!r.Accepted&&h.Snapshot().Length==0&&h.Charges("actor")==999,"expired pose cannot resurrect via queued tick20");
  t=new Trust();o=new Occupants();h=Host(t,o);Claim(h,"actor","left",Pose(S/2),20);h.Release("actor");
  Check(!Place(h,Cmd(0,20)).Accepted,"explicit release prevents stale placement");
  Claim(h,"actor","left",Pose(S/2),20);t.AllowActor=false;
  Check(!Place(h,Cmd(1,20)).Accepted&&h.Snapshot().Length==0,"actual authority sibling death mutation rejects place");
  t.AllowActor=true;Check(Place(h,Cmd(2,20)).Accepted,"live authority control mutates actual service");
  Check(h.Snapshot().Length==1&&h.Charges("actor")==998&&h.Pending()!=null,"real panel resource outbox mutation");
  Check(!Place(h,Cmd(2,20)).Accepted&&h.Charges("actor")==998,"logical retry no second spend");
  Check(!h.Acknowledge(2)&&h.Acknowledge(1)&&h.Pending()==null,"ack preserves ordering");
  h.Close();Check(!Claim(h,"actor","left",Pose(S*1.5f),30)&&!Place(h,Cmd(3,20)).Accepted,"closed host rejects");
  t=new Trust();o=new Occupants();h=Host(t,o);Claim(h,"actor","left",Pose(S/2),20);o.ChangeOnRead=true;
  Check(!Place(h,Cmd(0,10)).Accepted&&h.Charges("actor")==999,"snapshot sibling generation mutation fails closed");
  o.ChangeOnRead=false;Check(Place(h,Cmd(0,10)).Accepted,"capture rejection leaves command reusable");
  t.CurrentMap=false;Claim(h,"actor","left",Pose(S*1.5f),20);
  Check(!Place(h,Cmd(1,11)).Accepted&&h.Snapshot().Length==1,"attestation sibling revocation rejects");
  t=new Trust();o=new Occupants();h=Host(t,o);Claim(h,"actor","left",Pose(S/2),20);o.Players=new[]{new ProtectedBox("actor",new Float3(S/2,0,S/2),new Float3(.2f,.8f,.2f))};
  Check(!Place(h,Cmd(0,10)).Accepted&&h.Charges("actor")==999,"requester whole body excluded from placement");
  o.Players=new ProtectedBox[0];o.Objectives=new[]{new Float3(S/2,0,S/2)};
  Check(!Place(h,Cmd(1,11)).Accepted,"objective snapshot mutation blocks");
  Check(Throws(delegate{Bind(new Trust(),new Occupants(),false,delegate(int i){return "wrong";});}),"explicit layer names required");
  Check(Throws(delegate{Bind(new Trust{Role=SurfaceRole.Unknown},new Occupants(),false,Layers);}),"unknown role rejected");
  Check(Throws(delegate{Bind(new Trust{Body="owner"},new Occupants(),false,Layers);}),"body distinct from semantic owner");
  t=new Trust();o=new Occupants();bool rejected=false;
  try{var w=Bind(t,o,true,Layers);h=new RuntimeHost("s",3,"review-host",t,w,null,Clock);Claim(h,"actor","left",Pose(S/2),20);r=Place(h,Cmd(0,10));Console.WriteLine("OBS authored pad accepted="+r.Accepted+" panels="+h.Snapshot().Length);}catch(ArgumentException){rejected=true;}catch(InvalidDataException){rejected=true;}
  Check(rejected,"unsupported authored pad volumes reject binding as reported");
  t=new Trust{Body="construction-body/review-host/build_3_1"};o=new Occupants();h=Host(t,o);Claim(h,"actor","left",Pose(S/2),20);r=Place(h,Cmd(0,10));Console.WriteLine("OBS body alias accepted="+r.Accepted+" panels="+h.Snapshot().Length);
  Check(r.Accepted&&BodyIdentity.Map(Bind(t,o,false,Layers).Identity,t.Body)!=BodyIdentity.Constructed(t.Body),"typed map body remains distinct and legitimate placement succeeds");

  foreach(bool alias in new[]{false,true})
  {
   t=new Trust{Role=SurfaceRole.TerrainSurface,Body=alias?"construction-body/review-host/build_3_1":"map-body"};o=new Occupants();var bound=Bind(t,o,false,Layers);h=new RuntimeHost("s",3,"review-host",t,bound,null,Clock);
   var wall=new Panel("wall",new Float3(S/2,-S/2-.081f,S*.4f),0,false,0);
   Check(bound.IsBuried(wall),"terrain control before placement alias="+alias);
   Claim(h,"actor","left",Pose(S/2),20);Check(Place(h,Cmd(0,10)).Accepted,"real placement for alias proof="+alias);
   bool buried=bound.IsBuried(wall);Console.WriteLine("OBS after-placement wall buried="+buried+" alias="+alias);
   Check(buried,"construction body skip must retain separate map terrain alias="+alias);
  }

  TranslateX=100;t=new Trust();o=new Occupants();var moved=Bind(t,o,false,Layers);h=new RuntimeHost("s",3,"moved-host",t,moved,null,Clock);
  Claim(h,"actor","left",Pose(S/2),30);Check(!Place(h,Cmd(0,10)).Accepted&&h.Charges("actor")==999,"World nonidentity transform removes original support");
  Claim(h,"actor","left",Pose(100+S/2),30);Check(Place(h,Cmd(1,11)).Accepted&&h.Charges("actor")==998,"World baked nonidentity transform supports actual moved placement");
  Check(!moved.HasStaticContact(new Panel("floor",new Float3(S/2,0,S/2),1,false,0),.045f),"transformed triangles remain finite at original location");
  TranslateX=0;
  foreach(var kind in new[]{"interaction","hazard"}) Check(Throws(delegate{Bind(new Trust(),new Occupants(),false,Layers,kind);}),"explicit unsupported surface boxes reject "+kind);
  Check(BodyIdentity.ConstructedScope("a/1",3,"b")!=BodyIdentity.ConstructedScope("a",3,"1/b"),"length-prefixed scope tuples injective");
  Check(BodyIdentity.ConstructedScope("s",3,"h")!=BodyIdentity.ConstructedScope("s",4,"h"),"epoch separates generated bodies");
  Check(BodyIdentity.Constructed("A")!=BodyIdentity.Constructed("a"),"ordinal case preserved");
  Check(Throws(delegate{BodyIdentity.Constructed(" ");}),"unsupported blank identity rejected");
  pass+=LifecycleGates.Run(delegate(FixtureHostClock clock){var trust=new Trust();return new RuntimeHost("s",3,"lifecycle-host",trust,Bind(trust,new Occupants(),false,Layers),null,clock);},delegate(float ignored){return Pose(S/2);});
  Console.WriteLine("RESULT "+pass+" PASS "+fail+" FAIL");Environment.ExitCode=fail==0?0:1;
 }
}

