using System;
using Ashfall.Core;
using Ashfall.Modules.Avatar;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.AvatarPose;
public static class LifecycleR2Tests
{
    sealed class Ports:IAuthorityGate,ISessionManagement,ISessionClock,IReconnectTokens,ISpawnWorld
    {
        int serial;public double Now{get{return 0;}}
        public string Issue(){return "token"+(++serial);}
        public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
        public bool CanManage(CommandContext c){return true;}
        public SpawnSite[] Sites(string map,bool warm){return new SpawnSite[0];}
        public bool IsSafe(string map,string actor,Float3 p){return false;}
    }
    static int n,tick;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);n++;}
    static CommandContext C(string actor){return new CommandContext("s",actor,"cmd"+(++tick),tick,0,true);}
    static Session NewSession(){var p=new Ports();return new Session("s","hash",new SessionRules("map","respawn","maphash",0,4,false,false,true,true,1),p,p,p,p,p);}
    public static void Main()
    {
        AvatarR4Fixtures.Source bytes;var manifest=AvatarR4Fixtures.Manifest(out bytes);
        var assembly=AvatarAssembler.Prepare(manifest,bytes,null,AvatarUse.UnreviewedPilot);
        var profile=SourceBodyProfile.QuestFixed("camo",new string('a',64));
        var id=new PoseIdentity("s","a","a",0,999,999,assembly,manifest,profile);
        VerifiedSkin left=null,right=null;foreach(var p in assembly.CopyParts()){if(p.Label=="LeftHand")left=VerifiedSkinReader.Read(p);if(p.Label=="RightHand")right=VerifiedSkinReader.Read(p);}
        var lg=AvatarR4Fixtures.Grip(left,"left");var rg=AvatarR4Fixtures.Grip(right,"right");
        var lp=lg.Update(0,0,"free","free","",1);var rp=rg.Update(0,0,"free","free","",1);
        var poses=new float[16][];for(int i=0;i<16;i++)poses[i]=SkinMatrix.Identity();
        Func<long,PoseIdentity,PoseFrame> frame=(seq,identity)=>new PoseFrame(identity,seq,poses,left,lp,right,rp,true);
        var session=NewSession();string reconnect,next;
        Check(session.Join(C("a"),new JoinRequest(Protocol.Name,1,"hash","a",null,false),out reconnect).Accepted,"join");
        var lease=new PoseLease();var legacy=lease.Bind(id);
        Check(!lease.AcceptCurrent(legacy,frame(1,id),session,true),"legacy unbound native consumer fails closed");
        Check(!lease.Owns(legacy),"unattested lifetime retired");
        var old=lease.Bind(id,session);Check(lease.AcceptCurrent(old,frame(1,id),session,true),"opaque actual connection captured");
        Check(!lease.AcceptCurrent(old,frame(1,id),session,true),"duplicate sequence");
        Check(!lease.AcceptCurrent(old,frame(2,id),session,false),"physical loss hides admission");
        Check(lease.AcceptCurrent(old,frame(2,id),session,true),"physical sampled restore distinct from incarnation");
        // No lease call at all between disconnect and successful same-actor reconnect.
        Check(session.Disconnect(C("a"),false).Accepted,"disconnect");
        Check(session.Join(C("a"),new JoinRequest(Protocol.Name,1,"hash","a",reconnect,false),out next).Accepted,"same actor reconnect");
        Check(!lease.AcceptCurrent(old,frame(3,id),session,true),"between-sample old lease cannot resurrect with matching numeric labels");
        Check(!lease.Owns(old),"mismatched incarnation terminal");
        var fresh=lease.Bind(id,session);Check(lease.AcceptCurrent(fresh,frame(1,id),session,true),"fresh actual token accepts same numeric labels");
        lease.Revoke(old);Check(lease.Owns(fresh),"stale disposal cannot revoke replacement");
        var clone=NewSession();Check(clone.Join(C("a"),new JoinRequest(Protocol.Name,1,"hash","a",null,false),out next).Accepted,"other exact-string session");
        Check(!lease.AcceptCurrent(fresh,frame(2,id),clone,true),"different actual Session object rejected");
        fresh=lease.Bind(id,session);
        Check(session.Disconnect(C("a"),false).Accepted,"disconnect before remap");
        Check(session.Join(C("b"),new JoinRequest(Protocol.Name,1,"hash","b",reconnect,false),out next).Accepted,"remap");
        Check(!lease.ValidateCurrent(fresh,session),"between-sample remap retires old lease");
        var remap=new PoseIdentity("s","b","a",0,999,999,assembly,manifest,profile);
        var replacement=lease.Bind(remap,session);Check(lease.AcceptCurrent(replacement,frame(1,remap),session,true),"fresh canonical remap");
        lease.Revoke(fresh);Check(lease.Owns(replacement),"stale remap callback lacks authority");
        Check(session.Disconnect(C("b"),false).Accepted,"observed disconnect");Check(!lease.ValidateCurrent(replacement,session),"observed disconnect retires");
        Check(session.Join(C("b"),new JoinRequest(Protocol.Name,1,"hash","b",reconnect,false),out next).Accepted,"second reconnect");
        Check(!lease.AcceptCurrent(replacement,frame(2,remap),session,true),"observed disconnected lease terminal after reconnect");
        Console.WriteLine("PASS "+n+" actual neutral lifecycle assertions; no native/runtime approval");
    }
}
