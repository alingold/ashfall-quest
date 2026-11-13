using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Gallery
{
    // Source live097 creates a fresh private owner on grab. The real Magnum
    // requires a finite pool: this preview owns its own source1200 pistol rounds.
    public sealed class R23MagnumGallerySession
    {
        long generation;R23MagnumRuntime live;
        public bool Active {get;private set;}
        public bool Live {get{return live!=null;}}
        public R23MagnumRuntime Owner {get{return live;}}
        public int PreviewShots {get;private set;}
        public void Show(){if(!Active){Release();Active=true;}}
        public bool Grab()
        {
            if(!Active||Live)return false;
            live=new R23MagnumRuntime(R23MagnumRuntime.CreatePistolReserve(),"lobby-live097/magnum/"+(++generation));return true;
        }
        public void Release(){if(live!=null)live.Retire();live=null;}
        public void Hide(){Release();Active=false;}
        public bool Step(float seconds,R23MagnumInput input,bool focused)
        {
            if(!Active||live==null)return false;
            if(!focused){live.SuspendControls();return false;}
            bool shot=live.Step(seconds,input);if(shot)PreviewShots++;return shot;
        }
    }
}
