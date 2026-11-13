using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Gallery
{
    // Source gallery live097 with the real M1014 owner and a private finite shell
    // reserve. No carried inventory, target health or combat resolver is referenced.
    public sealed class R23M1014GallerySession
    {
        sealed class Lease:IAuthorityGate
        {
            internal bool Active=true;
            public RuleResult Validate(CommandContext c){return Active&&c.IsAuthority&&c.SessionId==Session&&c.ActorId==Actor&&c.AuthorityEpoch==0?RuleResult.Accept():RuleResult.Reject("private M1014 preview lifetime ended");}
        }
        const string Session="r23-lobby-m1014",Actor="preview";
        readonly R23M1014Controls controls=new R23M1014Controls();
        R23M1014Owner owner;Lease lease;long generation,tick,sequence;
        public bool Active {get;private set;}public bool Live {get{return owner!=null;}}
        public R23M1014Owner Owner {get{return owner;}}
        public int PreviewShots {get;private set;}
        public void Show(){if(!Active){Release();Active=true;}}
        public bool Grab()
        {
            if(!Active||Live)return false;lease=new Lease();tick=sequence=0;
            owner=new R23M1014Owner(Command(),"lobby-live097/m1014/"+(++generation),new R23RifleReserve(480),lease);
            owner.SuspendControls();controls.Reset();return true;
        }
        CommandContext Command(){return new CommandContext(Session,Actor,"preview/"+(++sequence),tick,0,true);}
        public void Release(){if(owner!=null)owner.SuspendControls();if(lease!=null)lease.Active=false;owner=null;lease=null;controls.Reset();}
        public void Hide(){Release();Active=false;}
        public bool Step(float seconds,HandlingInput input,bool primaryClick,float stickX,float stickY,R23M1014Pose actualMuzzle,bool focused,out R23M1014Volley volley,bool desktopReload=false)
        {
            volley=null;if(!Active||owner==null)return false;
            if(Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<0||seconds>.25f)throw new ArgumentOutOfRangeException("seconds");
            if(!focused||!input.Enabled||input.Paused||!input.PrimaryTracked||input.PrimaryClimbing||input.HeadObstructed){owner.SuspendControls();controls.Reset();return false;}
            var projection=controls.Project(true,primaryClick,true,stickX,stickY,desktopReload);
            tick++;input.Sequence=sequence+1;input.DeltaSeconds=seconds;
            owner.Apply(Command(),projection.Action,input,actualMuzzle,out volley,projection.EjectAfterShot);
            if(volley!=null){PreviewShots++;return true;}return false;
        }
    }
}
