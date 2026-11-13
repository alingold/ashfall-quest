using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Avatar;
using Ashfall.Modules.Player;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.AvatarPose;
public static class PoseIntegrationTests
{
    static int count;static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
    static void Reject(Action a,string name){bool rejected=false;try{a();}catch(AvatarValidationException){rejected=true;}Check(rejected,name);}
    sealed class Authority:IAuthorityGate,ISessionManagement{public RuleResult Validate(CommandContext c){return RuleResult.Accept();}public bool CanManage(CommandContext c){return true;}}
    sealed class Clock:ISessionClock{public double Now{get{return 0;}}}
    sealed class Tokens:IReconnectTokens{int n;public string Issue(){return "token"+(++n);}}
    sealed class World:ISpawnWorld{public SpawnSite[] Sites(string id,bool warm){return new SpawnSite[0];}public bool IsSafe(string map,string actor,Float3 p){return false;}}
    sealed class UnusedBoundary:Ashfall.Modules.InventoryItems.IInventoryBoundary{public RuleResult Validate(CommandContext c,Ashfall.Modules.InventoryItems.InventoryOperation o,Ashfall.Modules.InventoryItems.ItemInstance i,Ashfall.Modules.InventoryItems.ItemInstance[] outgoing){throw new Exception("Fixture must not use eligibility as a success stub");}}
    static CommandContext C(string actor,long tick){return new CommandContext("s",actor,"cmd"+tick,tick,0,true);}
    public static void Main()
    {
        AvatarR4Fixtures.Source bytes;var manifest=AvatarR4Fixtures.Manifest(out bytes);
        var assembly=AvatarAssembler.Prepare(manifest,bytes,null,AvatarUse.UnreviewedPilot);
        var profile=SourceBodyProfile.QuestFixed("camo",new string('a',64));
        var identity=new PoseIdentity("s","a","a",0,1,1,assembly,manifest,profile);
        VerifiedSkin left=null,right=null;foreach(var p in assembly.CopyParts()){if(p.Label=="LeftHand")left=VerifiedSkinReader.Read(p);if(p.Label=="RightHand")right=VerifiedSkinReader.Read(p);}
        var lg=AvatarR4Fixtures.Grip(left,"left");var rg=AvatarR4Fixtures.Grip(right,"right");
        var open=lg.Update(0,0,"free","free","",1);var rightOpen=rg.Update(0,0,"free","free","",1);
        var held=lg.Update(1,1,"firing","pistol","magnum",1);
        var a=left.Deform(open.CopyLocalMatrices());var b=left.Deform(held.CopyLocalMatrices());bool changed=false;for(int i=0;i<a.Length;i++)changed|=Math.Abs(a[i]-b[i])>1e-5;Check(changed,"actual SourceGripPose -> actual VerifiedSkin.Deform");
        var poses=new float[16][];for(int i=0;i<16;i++)poses[i]=SkinMatrix.Identity();
        var frame=new PoseFrame(identity,1,poses,left,held,right,rightOpen,true);poses[0][3]=50;Check(frame.CopyWorld()[0][3]==0,"owned world snapshot");
        var copy=frame.CopyWorld();copy[0][3]=20;Check(frame.CopyWorld()[0][3]==0,"defensive world copy");
        poses[0]=SkinMatrix.Identity();poses[15][0]=float.NaN;Reject(()=>new PoseFrame(identity,2,poses,left,held,right,rightOpen,false),"nonfinite last part");
        poses[15]=SkinMatrix.Identity();poses[15][1]=.5f;Reject(()=>new PoseFrame(identity,2,poses,left,held,right,rightOpen,false),"shear");
        poses[15]=new float[17];Reject(()=>new PoseFrame(identity,2,poses,left,held,right,rightOpen,false),"shape preclone");poses[15]=SkinMatrix.Identity();
        Reject(()=>new PoseFrame(identity,2,new float[100000][],left,held,right,rightOpen,false),"frame bound before clone");
        var authority=new Authority();var session=new Session("s","hash",new SessionRules("map","respawn","maphash",0,4,false,false,true,true,1),authority,new Clock(),new Tokens(),new World(),authority);
        string token;Check(session.Join(C("a",1),new JoinRequest(Protocol.Name,1,"hash","a",null,false),out token).Accepted,"actual join");
        var lease=new PoseLease();var owner=lease.Bind(identity,session);Check(lease.AcceptCurrent(owner,frame,session,true),"real session accepts full frame");
        Check(!lease.AcceptCurrent(owner,frame,session,true),"monotonic frame");
        var next=new PoseFrame(identity,2,poses,left,held,right,rightOpen,false);Check(!lease.AcceptCurrent(owner,next,session,false),"physical loss consumer invalidation");
        Check(lease.AcceptCurrent(owner,next,session,true),"physical tracking restored");
        Check(session.Disconnect(C("a",2),false).Accepted,"actual session disconnect");
        Check(!lease.AcceptCurrent(owner,new PoseFrame(identity,3,poses,left,held,right,rightOpen,false),session,true),"real disconnect invalidates consumer");
        string token2;Check(session.Join(C("b",3),new JoinRequest(Protocol.Name,1,"hash","b",token,false),out token2).Accepted,"actual reconnect remap");
        Check(session.Get("b").CanonicalActorId=="a"&&session.Get("a")==null,"canonical identity preserved");
        Check(!SessionPoseGuard.Current(session,identity),"old actor cannot follow remap");
        var remapped=new PoseIdentity("s","b","a",0,2,2,assembly,manifest,profile);var replacement=lease.Bind(remapped,session);
        lease.Revoke(owner);Check(lease.Owns(replacement),"old provider has no replacement disposal authority");
        Check(!lease.Accept(owner,frame),"old provider no presentation authority");
        Check(!lease.Accept(replacement,frame),"foreign actor frame ignored");
        var input=new PlayerInput();var sample=new DeviceSample{Focused=true,IsXr=true,HeadTracked=true,HeadPosition=new Float3(0,1.6f,0),Left=new HandSample{Tracked=true},Right=new HandSample{Tracked=true}};
        var first=input.Read(1,.1f,sample);Check(first.Tracked&&input.LeftReady,"real input armed");sample.Paused=true;var paused=input.Read(2,.1f,sample);Check(paused.Tracked&&paused.Paused&&!input.LeftReady,"actual pause preserves physical head tracked while actions disarm");
        var frames=new PlayerFrameOwner(input);var state=new PlayerLifecycleState{Enabled=true,Focused=true,RigValid=true,Session="s",Actor="b",Epoch=0};frames.Observe(state);frames.Publish(first);long generation=frames.Generation;state.Enabled=false;frames.Observe(state);Check(!frames.Valid&&frames.Generation>generation,"actual frame-owner disable mutation");
        var motor=new Locomotion(new PlayerTuning());motor.SetVelocity(new Float3(2,3,4));Check(motor.Velocity.Y==3,"actual locomotion velocity mutation");motor.CancelMotion();Check(motor.Velocity.Y==0,"actual locomotion cancellation");
        var catalog=Ashfall.Modules.InventoryItems.SourceCatalog.Create();Check(catalog.Get("meridian").ProfileId=="px4"&&catalog.Get("cairn6").ProfileId=="magnum","real catalog identity mapping");
        var inventory=new Ashfall.Modules.InventoryItems.Inventory(catalog,new UnusedBoundary(),"s","b",0,true);
        var weapon=new Ashfall.Modules.WeaponHandling.WeaponHandling("s","b",0,Ashfall.Modules.WeaponHandling.WeaponMechanism.ServiceRifle,new Ashfall.Modules.WeaponHandling.FiniteAmmoReserve(100,100),authority);
        var contact=InventoryGripContact.Read(inventory.Capture(),weapon.Snapshot,inventory.SelectedInstanceId,true,false,"longgun",null);
        Check(contact.SourceId=="cx4"&&contact.Role=="firing","actual Inventory + WeaponHandling snapshot consumed");
        Check(InventoryGripContact.Read(inventory.Capture(),weapon.Snapshot,inventory.SelectedInstanceId,true,true,"longgun",null).Role=="free","gesture reservation releases render contact");
        inventory.RetireForHandoff();Check(InventoryGripContact.Read(inventory.Capture(),weapon.Snapshot,inventory.SelectedInstanceId,true,false,"longgun",null).Role=="free","actual inventory retirement invalidates contact");
        Console.WriteLine("PASS "+count+" assertions; neutral only, native/production gates OPEN");
    }
}
