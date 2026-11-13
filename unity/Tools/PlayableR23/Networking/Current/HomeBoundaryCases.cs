using System;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.Networking;
partial class LobbyCases
{
    static void HomeBoundaryCases()
    {
        using(var pair=new Pair()){
            StartHealthRound(pair);string actor=pair.Host.OwnActor;var prior=Health(pair.Host,actor);var round=new R23RoundState(new[]{"practice"},new RoundHealthQueries(),true);round.ConfigureLocalMatchHealth("respawn",0,-1,80,25,2,10,1,0,1);var view=new R24LanPlayerHealthView(pair.Host);round.BindPlayerHealthView(view);
            var boundary=new R24LanHomeBoundary();int leaves=0,shows=0,releases=0,nestedReleases=0;
            Action show=delegate{shows++;if(round.PlayerHealthViewBound){Need(round.UnbindPlayerHealthView(view),"Actual local Health view detaches before menu owner release");view.Dispose();round.EndLocalMatchHealth();}};
            Func<bool> leave=null;leave=delegate{leaves++;return boundary.Close(delegate{Need(!boundary.Close(delegate{},delegate{nestedReleases++;}),"Reentrant close does not release a second owner");boundary.Home(leave,show);},delegate{releases++;pair.HostPipe.Close("client");pair.Host.Dispose();});};
            boundary.WorldHome(delegate{boundary.Home(leave,show);});var afterWorld=Health(pair.Host,actor);
            Need(shows==1&&leaves==0&&releases==0&&pair.Host.State==R24LanLobbyState.Hosting&&pair.Client.State==R24LanLobbyState.Connected,"Host-driven world HOME preserves real Session-backed membership and transport");
            Need(afterWorld.Revision==prior.Revision&&afterWorld.LifeGeneration==prior.LifeGeneration&&afterWorld.Health==prior.Health&&afterWorld.Shield==prior.Shield&&afterWorld.SpawnRevision==prior.SpawnRevision,"Applying guard does not grant source Health, root revision, protection or a new life");
            Need(!round.PlayerHealthViewBound&&view.Read()==null,"Local menu cleanup retires its old presentation binding without replacing the host Health owner");
            view=new R24LanPlayerHealthView(pair.Host);round.BindPlayerHealthView(view);Need(round.PlayerHealthState.Revision==prior.Revision&&round.Health==80,"A fresh local view reads the same real host life after a declared world menu transition");
            bool threw=false;try{boundary.WorldHome(delegate{throw new InvalidOperationException("declared local menu failure");});}catch(InvalidOperationException){threw=true;}
            Need(threw&&pair.Host.State==R24LanLobbyState.Hosting&&round.Health==80,"Failed world menu callback preserves session and propagates the actual failure");
            boundary.Home(leave,show);
            Need(leaves==1&&releases==1&&shows==2&&nestedReleases==0&&pair.Host.State==R24LanLobbyState.Offline&&pair.Client.State==R24LanLobbyState.Offline,"User HOME leaves once, closes actual lobby pair, and shows menu once without recursive teardown");
            Need(!round.PlayerHealthViewBound&&view.Read()==null&&round.Health==100&&!view.ApplyDamage(1,false,"damage"),"Offline restoration follows exact unbind; disposed network view cannot regain Health authority");
            boundary.Home(delegate{return false;},show);Need(shows==3&&releases==1,"Ordinary offline HOME retains its existing single menu action");
        }
        var control=new R24LanHomeBoundary();int cleanup=0;bool failed=false;
        try{control.Close(delegate{throw new InvalidOperationException("declared scene cleanup failure");},delegate{cleanup++;});}catch(InvalidOperationException){failed=true;}
        Need(failed&&cleanup==1,"Owned release still runs once when local HOME throws; failure is not swallowed");
        Need(control.Close(delegate{},delegate{cleanup++;})&&cleanup==2,"Exception unwinds the close scope instead of permanently locking future ownership");
        failed=false;try{control.WorldHome(delegate{control.WorldHome(delegate{throw new InvalidOperationException("declared nested failure");});});}catch(InvalidOperationException){failed=true;}
        int leavesAfterFailure=0,shownAfterFailure=0;control.Home(delegate{leavesAfterFailure++;return false;},delegate{shownAfterFailure++;});Need(failed&&leavesAfterFailure==1&&shownAfterFailure==1,"Nested world scopes both unwind; later genuine HOME cannot retain an old applying exemption");
    }
}
