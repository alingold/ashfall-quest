using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Gallery;
class M1014GalleryCases
{
    static int count;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
    static HandlingInput Input(bool fire=false,bool grip=false,Float3 point=new Float3())
    {return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=fire,Grip=grip,SupportLocal=point,ForegripDistance=.8f};}
    static R23M1014Pose Pose(){return new R23M1014Pose{Origin=new Float3(1,2,3),Forward=new Float3(0,0,1),Right=new Float3(1,0,0),Up=new Float3(0,1,0)};}
    static void Main()
    {
        var session=new R23M1014GallerySession();R23M1014Volley volley;session.Show();Check(session.Grab()&&session.Owner.Snapshot.Loaded==6&&session.Owner.Reserve==480,"actual private M1014 owner starts6/480");
        Check(!session.Step(.1f,Input(true),false,0,0,Pose(),true,out volley),"new M1014 grip held trigger requires release");session.Step(0,Input(),false,0,0,Pose(),true,out volley);
        Check(session.Step(.01f,Input(true),false,0,0,Pose(),true,out volley)&&volley!=null&&volley.Directions.Length==8&&session.Owner.Snapshot.Loaded==5,"real M1014 discharge creates8pellets and debits one private shell");
        Check(volley.Origin.X==1&&volley.Origin.Y==2&&volley.Origin.Z==3&&volley.PelletDamage==9.625f,"actual supplied muzzle frame and source pellet damage retained");
        Check(session.Step(.3f/2,Input(true),false,0,0,Pose(),true,out volley)==false,"M1014 native interval prevents early repeat");
        Check(session.Step(.3f/2,Input(true),false,0,0,Pose(),true,out volley)&&session.Owner.Snapshot.Loaded==4,"M1014 automatic held trigger repeats through real owner");
        session.Step(0,Input(),false,0,0,Pose(),true,out volley);session.Step(0,Input(),true,0,0,Pose(),true,out volley);
        Check(session.Owner.Snapshot.Phase==ReloadPhase.NeedsMagazine&&session.Owner.Reserve==484,"native eject refunds only private remaining shells");
        session.Step(0,Input(),false,0,0,Pose(),true,out volley);session.Step(.01f,Input(false,true,R23M1014Definition.Offer),false,0,0,Pose(),true,out volley);
        session.Step(.01f,Input(false,true,R23M1014Definition.Socket),false,0,0,Pose(),true,out volley);
        Check(session.Owner.Snapshot.Phase==ReloadPhase.NeedsRack,"source pack UP travel inserts through actual owner");
        session.Step(.01f,Input(),false,0,0,Pose(),true,out volley);session.Step(.01f,Input(false,true,R23M1014Definition.Handle),false,0,0,Pose(),true,out volley);
        var handle=R23M1014Definition.Handle;session.Step(.01f,Input(false,true,new Float3(handle.X,handle.Y,handle.Z-.09f)),false,0,0,Pose(),true,out volley);
        Check(session.Owner.Snapshot.Phase==ReloadPhase.Ready&&session.Owner.Snapshot.Loaded==6&&session.Owner.Reserve==478,"source BACK charge chambers6 and conserves private finite pool");
        var retained=session.Owner;string id=retained.InstanceId;int ammo=retained.Snapshot.Loaded;session.Hide();
        Check(!session.Active&&!session.Live,"hide withdraws M1014 live owner");
        var refused=retained.Apply(new CommandContext("r23-lobby-m1014","preview","retired-attempt",999,0,true),HandlingAction.Fire,Input(true),Pose(),out volley);
        Check(!refused.Accepted&&retained.Snapshot.Loaded==ammo,"ended preview authority cannot fire retained owner");
        session.Show();Check(session.Grab()&&session.Owner.InstanceId!=id&&session.Owner.Snapshot.Loaded==6&&session.Owner.Reserve==480,"new grip creates fresh source preview, never restores carried ammo");
        Check(!session.Step(.25f,Input(true),false,0,0,Pose(),false,out volley),"unfocused gallery cannot fire");session.Hide();
        Console.WriteLine("PASS "+count+" real M1014 preview-owner isolation/reload/volley cases");
    }
}
