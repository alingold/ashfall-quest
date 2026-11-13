using System;
namespace Ashfall.Integration.Playable.Networking
{
    // main._return_home0992 leaves a live session unless the bridge is applying
    // a host world transition. The scope covers synchronous local menu work only.
    public sealed class R24LanHomeBoundary
    {
        int applyingWorld;bool closing;
        public void Home(Func<bool> leaveSession,Action showMenu)
        {
            if(leaveSession==null||showMenu==null)throw new ArgumentNullException();
            if(applyingWorld>0||closing){showMenu();return;}
            // Close may already have shown HOME while clearing the old binding.
            if(!leaveSession())showMenu();
        }
        public void WorldHome(Action home)
        {
            if(home==null)throw new ArgumentNullException("home");applyingWorld=checked(applyingWorld+1);
            try{home();}finally{applyingWorld--;}
        }
        public bool Close(Action home,Action release)
        {
            if(home==null||release==null)throw new ArgumentNullException();if(closing)return false;
            closing=true;
            try{try{home();}finally{release();}return true;}
            finally{closing=false;}
        }
    }
}
