using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.World;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Combat;
using Ashfall.Modules.Flight;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration;
using Ashfall.Integration.InventoryTransfers;
using Ashfall.Integration.RuntimeLoop;
partial class Scenario : IAuthorityGate,IHealthWorld,ICombatEvidence,IInteractionQuery,IPickupActors,ITransferEligibility,IDropPlanner,IWeaponLifetimeAuthority,IFlightWorld,IRuntimeEvents,IWorldApproval,IWorldBuildPolicy,ISpawnQuery,Ashfall.Modules.Sessions.ISessionClock,Ashfall.Modules.Sessions.ISessionManagement,Ashfall.Modules.Sessions.IReconnectTokens,Ashfall.Modules.Sessions.ISpawnWorld,Ashfall.Integration.MatchDirector.IMatchClock,Ashfall.Integration.MatchDirector.IKnockdownAttribution
{
    static int checks;long tick,serial;public double Now{get;set;}
    public long Tick{get{return tick;}} public bool CanManage(CommandContext c){return c.ActorId=="host";} public string Issue(){return "token-"+(++serial);} public Ashfall.Modules.Sessions.SpawnSite[] Sites(string map,bool warmup){return new[]{new Ashfall.Modules.Sessions.SpawnSite("spawn",new Float3(0,.12f,0),-1,true,false)};} public bool IsSafe(string map,string actor,Float3 feet){return feet.IsFinite;} public string KillerFor(HealthTransition e){return null;}
    public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
    readonly Dictionary<string,HitEvidence> facts=new Dictionary<string,HitEvidence>();
    readonly HashSet<long> audio=new HashSet<long>(),ui=new HashSet<long>();bool rejectSink;
    public RuleResult Validate(CommandContext c){return c.IsAuthority&&c.SessionId=="s"&&c.AuthorityEpoch>=0&&c.AuthorityEpoch<=1&&(c.ActorId=="a"||c.ActorId=="b"||c.ActorId=="host")?RuleResult.Accept():RuleResult.Reject("auth");}
    public bool TryGetPose(string id,out RescuePose p){p=new RescuePose{Tracked=true,PaddlesActive=true,Desktop=true,Facing=true,Left=new Float3(0,1,0),Right=new Float3(0,1,0),Chest=new Float3(0,1,0)};return true;}
    public bool TryResolve(CommandContext c,string id,out HitEvidence e){return facts.TryGetValue(id,out e);}
    public bool HasLineOfSight(string a,string b,Float3 o,Float3 t,float range){return b=="a"||b=="b"||b=="banana-sem";}
    bool IPickupActors.TryRead(string actor,out Ashfall.Modules.Pickups.ActorFrame f){f=new Ashfall.Modules.Pickups.ActorFrame{Allowed=true,HandEligible=true,TransferSafe=true,Desktop=true,Head=new Float3(0,1,0),Hand=new Float3(0,1,0),Forward=new Float3(0,0,1)};return true;}
    sealed class EmptyBatch:IDropBatch {bool done;public EvictedInstance[] Placements{get{return new EvictedInstance[0];}}public bool StillValid{get{return !done;}}public bool Commit(){if(done)return false;done=true;return true;}public void Abort(){done=true;}} public bool Prepare(CommandContext c,ItemInstance[] members,out IDropBatch batch){batch=null;if(members.Length!=0)return false;batch=new EmptyBatch();return true;}
    public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int rounds){return Validate(c);}
    public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return Validate(c);}
    public bool ClearPadContact(string id,Float3 f,Float3 c){return id=="pad";}public bool ClearCapsuleExit(string id,Float3 p){return false;}
    public RuleResult Deliver(HealthTransition e){audio.Add(e.Sequence);if(rejectSink)return RuleResult.Reject("UI offline");ui.Add(e.Sequence);return RuleResult.Accept();}
    public bool IsApproved(ContentIdentity i){return i.MapId=="synthetic-loop"&&i.Disposition=="accepted";}
    public bool ApproveMaterial(ContentIdentity i,LoadedSurface s){return s.MaterialPath=="synthetic";}
    public bool AllowsSpawn(ContentIdentity i,string mode,string team,Float3 p){return p.IsFinite;}
    public bool TryFloor(Float3 c,float up,float depth,out Float3 floor,out Float3 normal){floor=new Float3(c.X,0,c.Z);normal=new Float3(0,1,0);return true;}
    public bool HasStandingRoom(Float3 p,float r,float h){return true;}public bool IsHazard(Float3 p){return false;}
    sealed class Root:IActorRoot,IRuntimeActorBinding
    {
        public string Id;public string ConnectionOverride=null;public HealthService Health;
        public string SessionId{get{return "s";}} public string CanonicalActorId{get{return Id;}} public string ConnectionId{get{return ConnectionOverride??Id;}}
        public long AuthorityEpoch{get{return BindingEpoch;}} public long LifeGeneration{get{return Health.Read(Id).LifeGeneration;}} public long ConnectionGeneration{get{return Health.Read(Id).ConnectionGeneration;}} public long RootGeneration{get{return Generation;}}
        public Float3 Position{get;set;}public int Commits;public bool Block;public bool InvalidHeading=false;public long Generation=1,BindingEpoch=1;
        public MotionEnvironment Environment{get{return new MotionEnvironment{Grounded=Position.Y<=.12f,HeadingRadians=InvalidHeading?float.NaN:0};}}
        public RuleResult Validate(RootMotionProposal p){return Block?RuleResult.Reject("synthetic wall"):RuleResult.Accept();}
        public Float3 Commit(long tick,RootMotionProposal p,Float3 v){Commits++;Position=new Float3(Position.X+p.Translation.X,Math.Max(.12f,Position.Y+p.Translation.Y),Position.Z+p.Translation.Z);return Position.Y<=.12f&&v.Y<0?new Float3(v.X,0,v.Z):v;}
    }
    sealed class Reserve:IAmmoReserve{int n=20;public int Available{get{return n;}}public int Take(int x){int k=Math.Min(n,x);n-=k;return k;}public int Refund(int x){n+=x;return x;}}
    CommandContext C(string actor,string id=null){return new CommandContext("s",actor,id??"fixture-"+(++serial),tick,1,true);}
    static void Check(bool v,string n){if(!v)throw new Exception(n);checks++;Console.WriteLine("PASS "+n);}
    HealthService health;CombatHealthBridge combat;HostLoop loop;TransferBridge items;SessionWeapons weapons;Root a,b;
    Ashfall.Integration.RuntimeLoop.ActorFrame A(string id,RuntimeAction action=RuntimeAction.None,string key=null,bool focused=true,bool trigger=false,bool reload=false,float move=0,bool jump=false,string target=null)
    {return new Ashfall.Integration.RuntimeLoop.ActorFrame{Actor=id,Action=action,InstanceOrHit=key,Target=target,Device=new DeviceSample{Focused=focused,MoveY=move,Jump=jump,Reload=reload,Right=new HandSample{Trigger=trigger?1:0}}};}
    ActorResult[] Step(Ashfall.Integration.RuntimeLoop.ActorFrame fa,Ashfall.Integration.RuntimeLoop.ActorFrame fb,bool accepted=true)
    {tick++;Now=tick*.1;ActorResult[] r;var result=loop.Tick(C("host"),.1f,new[]{fb,fa},out r);Check(result.Accepted==accepted,"tick "+tick+" "+result.Reason);return r;}
    void Run()
    {
        byte[] actualPilot=File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"reference-map.json"));bool actualDenied=false;try{ApprovedWorld.Load(actualPilot,WorldLoader.Hash(actualPilot),this);}catch(InvalidDataException){actualDenied=true;}Check(actualDenied,"real Paintball pilot rejected by actual approval boundary");
        var doc=new MapDocument{schema=1,id="synthetic-loop",version="1",sourcePath=@"C:\synthetic\loop.json",sourceSha256=new string('a',64),sourceRevision="synthetic-only",approval="accepted",indoor=true,capsules=false,
        surfaces=new[]{new Surface{id="floor",semanticOwner="floor",colliderRole="structure",materialPath="synthetic",matrix=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1},vertices=new float[]{-10,0,-10,10,0,-10,0,0,10},normals=new float[]{0,1,0,0,1,0,0,1,0},uv=new float[0],indices=new[]{0,1,2},color=new float[]{1,1,1,1},roughness=1}},
        spawns=new[]{new Spawn{id="spawn",mode="revive",team="shared",position=new float[]{0,0,0},forward=new float[]{0,0,-1},nativeMarker=true}},pads=new Pad[0],semanticsVersion="world-semantics/1",semantics=new[]{new SemanticRecord{sourceId="floor",ownerId="floor",kind="structure",evidence="synthetic"}}};
        byte[] bytes;using(var m=new MemoryStream()){new DataContractJsonSerializer(typeof(MapDocument)).WriteObject(m,doc);bytes=m.ToArray();}
        string hash=WorldLoader.Hash(bytes);var plan=ApprovedWorld.Load(bytes,hash,this).PrepareBuild(this,this,i=>i==8?"WorldStructure":i==9?"WorldInteraction":i==10?"PlayerActor":"WorldDecoration");
        var spawns=plan.SafeSpawns("revive","shared",1.75f,this,this);Check(spawns.Length==1,"real World safe spawn projection");
        doc.approval="pilot";using(var m=new MemoryStream()){new DataContractJsonSerializer(typeof(MapDocument)).WriteObject(m,doc);bytes=m.ToArray();}
        bool denied=false;try{ApprovedWorld.Load(bytes,WorldLoader.Hash(bytes),this);}catch(InvalidDataException){denied=true;}Check(denied,"actual map quarantine rejected");
        var geometry=new WorldGeometry(plan);Check(!geometry.HasLineOfSight("missing",new Float3(0,1,0),new Float3(0,-1,0),3),"actual World triangle occlusion query");
        health=new HealthService("s",1,2,false,this,this,this,60,1,0);Check(health.Register(C("host"),"a",0,false).Accepted&&health.Register(C("host"),"b",0,false).Accepted,"two actual health actors");
        combat=new CombatHealthBridge(health,this,this,this,this,"s",1);items=new TransferBridge("s",1,10,this,this,this,this,this);
        Check(items.AddActor("a")&&items.AddActor("b"),"two actual inventory actors");weapons=new SessionWeapons("s",10,this,this);Check(items.BindWeapons(weapons),"actual weapon binding");
        Check(weapons.Grant(C("a"),new ItemInstance("rifle","longwatch",1),WeaponMechanism.PrecisionRifle,3,new Reserve()).Accepted,"real weapon grant");Check(items.AdmitExistingWeapon(C("a"),new ItemInstance("rifle","longwatch",1),weapons).Accepted,"real weapon inventory admission");
        loop=new HostLoop("s",1,"host",this,health,combat,items,this);a=new Root{Id="a",Health=health,Position=spawns[0].Position};b=new Root{Id="b",Health=health,Position=new Float3(2,.12f,0)};
        Check(loop.AddActor("a",a).Accepted&&!loop.AddActor("b",a).Accepted&&loop.AddActor("b",b).Accepted,"distinct root ownership");
        Step(A("a"),A("b"));var r=Step(A("a",move:1,jump:true),A("b"));Check(a.Position.Z>0&&a.Position.Y>.12f&&b.Position.X==2&&r[0].Step.Jumped,"real move jump and second actor isolation");
        int count=a.Commits;ActorResult[] ignored;Check(!loop.Tick(C("host"),.1f,new[]{A("a"),A("b")},out ignored).Accepted&&a.Commits==count,"tick replay before mutation");
        tick++;Check(!loop.Tick(C("host"),.1f,new[]{A("a"),A("a")},out ignored).Accepted&&a.Commits==count,"duplicate actor batch before mutation");tick--;
        Step(A("a",RuntimeAction.Equip,"rifle"),A("b"));weapons.Selection(items.Inventory("a"),tick);
        Step(A("a"),A("b"));Step(A("a"),A("b"));Step(A("a"),A("b"));
        r=Step(A("a",RuntimeAction.Fire,"rifle",trigger:true),A("b"));Check(r[0].Action.Accepted&&weapons.Inspect("rifle").Loaded==2,"real fire decrements ammo");
        r=Step(A("a",RuntimeAction.Reload,"rifle",reload:true),A("b"));Check(r[0].Action.Accepted&&weapons.Inspect("rifle").Phase==ReloadPhase.Ready,"real desktop bolt reload");
        int ammo=weapons.Inspect("rifle").Loaded;r=Step(A("a",RuntimeAction.Fire,"rifle",focused:false,trigger:true),A("b"));Check(!r[0].Action.Accepted&&weapons.Inspect("rifle").Loaded==ammo,"focus loss disarms before fire");
        r=Step(A("a",RuntimeAction.Fire,"rifle",trigger:true),A("b"));Check(!r[0].Action.Accepted,"held focus regain remains disarmed");Step(A("a"),A("b"));
        Check(items.Register("banana","banana","banana-sem",new Float3(0,1,1)),"real pickup register");r=Step(A("a",RuntimeAction.Reserve,"banana"),A("b"));Check(r[0].Action.Accepted,"real pickup reserve");r=Step(A("a",RuntimeAction.Collect,"banana"),A("b"));Check(r[0].Action.Accepted&&items.Inspect("banana").Owner=="a","real pickup collect "+r[0].Action.Reason);r=Step(A("a",RuntimeAction.Equip,"banana"),A("b"));Check(r[0].Action.Accepted&&items.Inventory("a").SelectedInstanceId=="banana","real pickup selection");
        a.Position=new Float3(0,.12f,0);var pad=new LaunchPad("pad",new Float3(),new Float3(0,1,0),1,10);Check(loop.LaunchPad(C("a","pad"),pad,this,Now).Accepted,"real pad contacts and flight launch");r=Step(A("a"),A("b"));Check(r[0].Step.Velocity.Y>8,"pad velocity handed to sole gravity owner");
        var lease=combat.Equip(C("host"),"damage-rifle","a","kestrel",1,tick);var ev=combat.PublishEvent(C("host"),1,tick+100,DamageSource.CurrentActorIntent,DamageKind.Bullet,lease,tick);var hit=combat.PublishHit(C("host"),"hit",ev,"sub","b",0,tick);
        facts.Add("hit",new HitEvidence{Identity=hit,ActorInputReady=true,FriendlyFire=true,BaseDamage=250,DamageMultiplier=1,HeadRatio=1,Origin=new Float3(0,1,0),ViewOrigin=new Float3(0,1,0),Point=new Float3(0,1,1),MaximumRange=100});
        r=Step(A("a",RuntimeAction.Damage,"hit"),A("b"));Check(r[0].Action.Accepted&&health.Read("b").Life==LifeState.Downed,"actual CombatHealthBridge downs target "+r[0].Action.Reason+" life="+health.Read("b").Life);long revision=health.Read("b").Revision;r=Step(A("a",RuntimeAction.Damage,"hit"),A("b"));Check(r[0].Action.Accepted&&health.Read("b").Revision==revision,"hit redelivery no duplicate damage");
        for(int i=0;i<64;i++)Step(A("a",RuntimeAction.Charge),A("b"));r=Step(A("a",RuntimeAction.Revive,target:"b"),A("b"));Check(r[0].Action.Accepted&&health.Read("b").Health==50&&health.Read("b").Life==LifeState.Alive,"actual charged revive");
        count=a.Commits;Step(A("a"),A("b"),false);Check(a.Commits==count,"revive requires explicit new lifetime binding");
        loop=new HostLoop("s",1,"host",this,health,combat,items,this);Check(loop.AddActor("a",a).Accepted&&loop.AddActor("b",b).Accepted,"explicit revived lifetime rebind");
        a.Block=true;count=a.Commits;r=Step(A("a",move:1),A("b"));Check(!r[0].Motion.Accepted&&a.Commits==count,"collision rejection no root commit");a.Block=false;
        rejectSink=true;Check(health.SetConnected(C("host"),"b",health.Read("b").Revision,true).Accepted,"real publication producer");Step(A("a"),A("b"),false);rejectSink=false;Check(loop.Drain().Accepted,"drain committed tick without repeating actors");Step(A("a"),A("b"));Check(audio.SetEquals(ui),"audio/UI routing retries preserve transition identities");
        r=Step(A("a",RuntimeAction.Respawn),A("b"));Check(!r[0].Action.Accepted&&r[0].Action.Reason.StartsWith("UNKNOWN"),"missing coordinated respawn fails closed");
        Console.WriteLine("TOTAL "+checks+" PASS 0 FAIL; UNKNOWN coordinated-respawn, prepared-transactions, full-flight-feedback, actual-content, Unity-API/runtime, audio-listening/UI-render, XR/Quest/WAN");
    }

    void SessionComposition()
    {
        tick=0;Now=0;
        var session=new Ashfall.Modules.Sessions.Session("s","content",new Ashfall.Modules.Sessions.SessionRules("synthetic-loop","respawn","maphash",0,4,true,false,true,true,1),this,this,this,this,this);
        var director=new Ashfall.Integration.MatchDirector.Director(session,new Ashfall.Modules.Modes.ModeRules("respawn",0,0,0,1),"host",this,this,this,128,null,new Ashfall.Integration.MatchDirector.SessionHealthProjection(session,this,"host"),null);
        foreach(string id in new[]{"a","b"}){string token;var join=new CommandContext("s",id,"join-"+id,0,session.Epoch,true);Check(session.Join(join,new Ashfall.Modules.Sessions.JoinRequest(Protocol.Name,1,"content",id,null,false),out token).Accepted,"actual Sessions join "+id);Check(director.ObserveMember(id).Accepted,"actual director roster "+id);}
        Check(director.BeginLoading().Accepted&&session.Epoch==1,"actual mode/session loading epoch");
        foreach(string id in new[]{"a","b"})Check(session.Loaded(C(id),"maphash").Accepted,"actual map loaded "+id);
        Check(session.WorldReady(C("host"),"maphash").Accepted&&session.Activate(C("host")).Accepted,"actual session activates");
        var h=new HealthService("s",1,0,true,this,this,this,60,1,0);
        foreach(string id in new[]{"a","b"}){Check(session.Spawn(C(id)).Accepted,"actual Sessions spawn "+id);Check(h.Register(C("host"),id,-1,false).Accepted,"actual health spawn binding "+id);}
        var bridge=new CombatHealthBridge(h,this,this,this,this,"s",1);
        var transfers=new TransferBridge("s",1,10,this,this,this,this,this);transfers.AddActor("a");transfers.AddActor("b");
        var lifecycle=new Ashfall.Integration.LifecycleTransactions.Coordinator(h,session,new LifecycleIdentity(session),new UnsupportedLifecycleRoot());
        Check(lifecycle.ProjectCurrent(C("host"),"a",Now+1)&&session.ProjectedLife("a").HealthRevision==h.Read("a").Revision,"actual lifecycle coordinator projects current Health into Session");
        var composed=new HostLoop("s",1,"host",this,h,bridge,transfers,this,session,director);var ra=new Root{Id="a",Health=h,Position=session.Get("a").Position};var rb=new Root{Id="b",Health=h,Position=session.Get("b").Position};Check(composed.AddActor("a",ra).Accepted&&composed.AddActor("b",rb).Accepted,"actual spawn positions bound");
        ActorResult[] result;tick=1;Now=.1;ra.ConnectionOverride="b";
        Check(!composed.Tick(C("host"),.1f,new[]{A("b"),A("a")},out result).Accepted&&ra.Commits==0&&rb.Commits==0,"stale Session canonical connection rejects before roots");ra.ConnectionOverride=null;
        Check(composed.Tick(C("host"),.1f,new[]{A("b"),A("a")},out result).Accepted,"host tick routes actual registration to director");
        Check(director.LifeSequence("a")==1&&director.LifeSequence("b")==1,"two actual director life bindings");Check(director.StartLoadedRound().Accepted,"actual Modes round starts");
        tick=2;Now=.2;Check(composed.Tick(C("host"),.1f,new[]{A("a",move:1),A("b")},out result).Accepted,"host tick actual director heartbeat");
        Check(composed.RecordPosition(C("a")).Accepted&&session.Get("a").Position.Z==ra.Position.Z,"actual Sessions position uses root port");
        Check(h.ApplyLoss(C("host"),"a",h.Read("a").Revision,100,100,false).Accepted,"actual respawn mode death");
        tick=3;Now=.3;Check(composed.Tick(C("host"),.1f,new[]{A("a"),A("b")},out result).Accepted&&session.Get("a").Life==Ashfall.Modules.Sessions.SessionLife.Eliminated,"actual death outbox eliminates session member");
        Now=2;tick=20;Check(session.Spawn(C("a")).Accepted&&h.Spawn(C("host"),"a",h.Read("a").Revision,100,100).Accepted,"actual due Sessions and Health respawn APIs (not atomic root lifecycle)");
        countR3(ra,rb,composed,C("host"));
        ra.Position=session.Get("a").Position;rb.Position=session.Get("b").Position;
        composed=new HostLoop("s",1,"host",this,h,bridge,transfers,this,session,director);Check(composed.AddActor("a",ra).Accepted&&composed.AddActor("b",rb).Accepted,"respawn explicitly binds new placed lifetime");
        Check(composed.Tick(C("host"),.1f,new[]{A("a"),A("b")},out result).Accepted&&director.LifeSequence("a")==2,"actual respawn transition reaches director");
    }
    void countR3(Root first,Root second,HostLoop value,CommandContext c){int roots=first.Commits+second.Commits;ActorResult[] output;Check(!value.Tick(c,.1f,new[]{A("a"),A("b")},out output).Accepted&&first.Commits+second.Commits==roots,"old respawn binding refuses without root effects");}
    // External authentication/root fixture ports. No runtime authentication or placement approval.
    sealed class LifecycleIdentity:Ashfall.Integration.LifecycleTransactions.IAuthenticatedLifecycleIdentity
    {
        readonly Ashfall.Modules.Sessions.Session session;
        public LifecycleIdentity(Ashfall.Modules.Sessions.Session s){session=s;}
        public bool Validate(CommandContext c,string connection,string canonical,long generation){var m=session.Get(connection);return c.ActorId=="host"&&m!=null&&m.CanonicalActorId==canonical&&generation>0;}
    }
    sealed class UnsupportedLifecycleRoot:Ashfall.Integration.LifecycleTransactions.ILifecycleRootPort
    {public bool Prepare(Ashfall.Integration.LifecycleTransactions.RootPlacement p,out Ashfall.Integration.LifecycleTransactions.IPreparedRootPlacement token){token=null;return false;}}
    static int Main(string[] args){try{var scenario=new Scenario();scenario.Run();scenario.SessionComposition();Console.WriteLine("FINAL "+checks+" PASS 0 FAIL; 8 UNKNOWN groups; complete=false; strict=2");return args.Length>0?2:0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
}





