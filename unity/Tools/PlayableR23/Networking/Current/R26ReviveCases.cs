using System;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    static R24LanRawTracking RevivePose(string hash,int rev,float x,float y,float eye,float lean,bool paddles)
    {
        return new R24LanRawTracking(hash,(byte)(paddles?15:7),PoseAt(x,y,0),PoseAt(x+lean,y+eye,0),PoseAt(x-.3f,y+1,0),PoseAt(x+.3f,y+1,0),0,0,rev);
    }
    public static void ReviveRules(bool baseline)
    {
        foreach(string scenario in new[]{"standing","calibrated-sway","seated-height","lean","paddles-held","outside","other-floor","stale","wall","leave-range"}){
            using(var pair=new Pair()){
                string hash=new string('b',64);bool clear=true;
                for(int i=0;i<5;i++)pair.Tick();pair.Host.BindSpawnWorld(new OriginSpawnWorld());pair.Host.BindRawTrackingWorldQuery(delegate(Float3 a,Float3 b){return clear;});
                var rules=R24PlayableMatchRules.ForPreset("team_revive","range").With("zone_enabled",false).With("protection",0f);
                Need(pair.Host.HostAssignRoomTeam(pair.Client.OwnActor,pair.Host.ReadAuthorityMember(pair.Host.OwnActor).Team),"Assign patient to host team");
                Need(pair.Host.BeginRoundSelection(rules,hash,321)&&pair.Host.ReportMapLoaded(hash),"Host loaded");
                for(int i=0;i<3;i++)pair.Tick();Need(pair.Client.ReportMapLoaded(hash),"Guest loaded");for(int i=0;i<4;i++)pair.Tick();
                pair.Now=pair.Host.SelectedMap.StartsAt+.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);
                Need(pair.Client.RequestSpawn(),"Guest spawn");for(int i=0;i<4;i++)pair.Tick();Need(pair.Host.RequestSpawn(),"Host spawn");for(int i=0;i<4;i++)pair.Tick();
                string guest=pair.Client.OwnActor;int hrev=Rev(pair.Host),grev=Rev(pair.Client);
                R24LanSpawn spawn;pair.Host.TryReadSpawn(pair.Host.OwnActor,out spawn);
                float pos=spawn.Feet.X;while(Math.Abs(pos-.85f)>.001f){pair.Host.PublishRawTracking(Body(hash,hrev,pos,0,0));pair.Client.PublishRawTracking(Body(hash,grev,0,0,0));pair.Tick();pos+=Math.Sign(.85f-pos)*Math.Min(.5f,Math.Abs(pos-.85f));}
                var health=Health(pair.Host,guest);Need(pair.Host.ApplyHostHealthLoss(guest,health.Revision,health.Health,health.Shield,true).Accepted&&Health(pair.Host,guest).Life==LifeState.Downed,"Authoritative knockdown");
                int loaded,reserve;string dropPrefix=guest+"/death/";
                Need(Array.Exists(pair.Host.LootStates,row=>row.Id.StartsWith(dropPrefix,StringComparison.Ordinal)&&row.Profile=="cx4"&&row.Owner==""),"Knockdown releases the starter rifle to shared loot");
                for(int i=0;i<222;i++){
                    // 11.1 seconds, one 50 ms physics/network tick per iteration.
                    float x=scenario=="outside"||scenario=="leave-range"&&i>=100&&i<120?1.1f:.85f;
                    float eye=scenario=="seated-height"?1.06f:scenario=="calibrated-sway"?1.06f+.647f:1.7f;
                    float lean=scenario=="lean"&&i>=70&&i<145?-.21f:scenario=="calibrated-sway"?.01f*(float)Math.Sin(i*.4):0f;
                    clear=scenario!="wall"||i<100||i>=120;
                    pair.Host.PublishRawTracking(RevivePose(hash,hrev,x,scenario=="other-floor"?1.2f:0,eye,lean,scenario=="paddles-held"));
                    if(scenario!="stale"||i<100||i>=120)pair.Client.PublishRawTracking(Body(hash,grev,0,0,0));
                    pair.Tick();if(i==188)Need(Health(pair.Host,guest).Life==LifeState.Downed,"9.45 seconds cannot complete");
                }
                var end=Health(pair.Host,guest);bool expect=scenario=="standing"||scenario=="calibrated-sway"||!baseline&&(scenario=="seated-height"||scenario=="lean");
                Need(expect?end.Life==LifeState.Alive&&end.Health==50&&end.Shield==0:end.Life==LifeState.Downed,scenario+" unexpectedly "+end.Life);
                if(expect)Need(pair.Host.TryReadAmmo(guest,"cx4",out loaded,out reserve)&&loaded==25&&reserve==2400&&Array.Exists(pair.Host.LootStates,row=>row.Id.StartsWith(dropPrefix,StringComparison.Ordinal)&&row.Profile=="cx4"&&row.Owner==""),"Revive grants a fresh rifle and leaves the dropped copy on the ground");
                Console.WriteLine(scenario+" "+end.Life+" hp="+end.Health+" shield="+end.Shield);
            }
        }
        ThreePlayers(false,baseline);ThreePlayers(true,baseline);
    }

    // Keep a living ally across the map so the fixture cannot finish by team elimination.
    static void ThreePlayers(bool bothDown,bool baseline)
    {
        using(var pair=new Pair()){
            var pipe=new Pipe();var ally=new R24LanLobby(pipe,pipe.Close,delegate{return "unused";},new string('a',64));pipe.Lost=ally.TransportDisconnected;
            pair.HostPipe.Links.Add("ally",new Link{Remote=pipe,Back="server"});pipe.Links.Add("server",new Link{Remote=pair.HostPipe,Back="ally"});ally.StartClient("server","Ally",pair.Now);pair.Host.TransportConnected("ally");
            Action tick=delegate{pair.Tick();ally.Tick(pair.Now);};
            try{
                for(int i=0;i<8;i++)tick();Need(ally.State==R24LanLobbyState.Connected&&pair.Host.Members.Length==3,"Three members");
                pair.Host.BindSpawnWorld(new ReviveWorld());pair.Host.BindRawTrackingWorldQuery(Open);pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
                var rules=R24PlayableMatchRules.ForPreset("team_revive","range").With("zone_enabled",false).With("protection",0f);string hash=new string('b',64);
                int hostTeam=pair.Host.ReadAuthorityMember(pair.Host.OwnActor).Team;
                Need(pair.Host.HostAssignRoomTeam(ally.OwnActor,hostTeam)&&pair.Host.HostAssignRoomTeam(pair.Client.OwnActor,bothDown?hostTeam:1-hostTeam),"Assign the revive fixture sides before match entry");
                Need(pair.Host.BeginRoundSelection(rules,hash,77)&&pair.Host.ReportMapLoaded(hash),"Round");for(int i=0;i<3;i++)tick();pair.Client.ReportMapLoaded(hash);ally.ReportMapLoaded(hash);for(int i=0;i<5;i++)tick();
                pair.Now=pair.Host.SelectedMap.StartsAt+.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);ally.Tick(pair.Now);
                string host=pair.Host.OwnActor,enemy=pair.Client.OwnActor,friend=ally.OwnActor;
                Need(pair.Host.ReadAuthorityMember(friend).Team==pair.Host.ReadAuthorityMember(host).Team&&(pair.Host.ReadAuthorityMember(enemy).Team==pair.Host.ReadAuthorityMember(host).Team)==bothDown,"Host and ally share a team");
                Need(ally.RequestSpawn(),"Ally spawn");for(int i=0;i<4;i++)tick();Need(pair.Host.RequestSpawn(),"Host spawn");for(int i=0;i<4;i++)tick();Need(pair.Client.RequestSpawn(),"Enemy spawn");for(int i=0;i<5;i++)tick();
                if(bothDown){
                    foreach(var member in new[]{pair.Host,pair.Client,ally}){
                        R24LanSpawn born;Need(member.TryReadSpawn(member.OwnActor,out born),"Starting position");
                        float goal=member==pair.Host?0:member==ally?.8f:2.4f;
                        for(float x=born.Feet.X;;x+=Math.Sign(goal-x)*Math.Min(.5f,Math.Abs(goal-x))){
                            member.PublishRawTracking(Body(hash,Rev(member),x,0,0));tick();if(Math.Abs(x-goal)<.001f)break;
                        }
                    }
                    for(int i=0;i<226;i++){
                        // The second patient becomes a closer alternative four seconds into the hold.
                        var victim=i==0?friend:i==80?enemy:null;
                        if(victim!=null){var h=Health(pair.Host,victim);Need(pair.Host.ApplyHostHealthLoss(victim,h.Revision,h.Health,h.Shield,true).Accepted,"Down teammate");}
                        pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),0,0,0));ally.PublishRawTracking(Body(hash,Rev(ally),.8f,0,0));
                        pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),i<80?2.4f:.75f,0,0));tick();
                    }
                    Need(Health(pair.Host,friend).Life==(baseline?LifeState.Downed:LifeState.Alive),"First target keeps its ten seconds");
                    Console.WriteLine("second downed ally arrives, first target "+Health(pair.Host,friend).Life);return;
                }
                var f=Health(pair.Host,friend);
                Need(pair.Host.ApplyHostHealthLoss(friend,f.Revision,f.Health,f.Shield,true).Accepted,"Downed ally in three-peer game");
                for(int i=0;i<245;i++){
                    float enemyX=Math.Max(.65f,10-i*.3f);
                    ally.PublishRawTracking(Body(hash,Rev(ally),0,0,0));
                    pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),enemyX,0,0));
                    pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),2,0,0));tick();
                }
                R24LanRawTrackingSample raw;
                Need(pair.Host.TryReadRawTracking(enemy,pair.Now,out raw)&&Math.Abs(raw.Pose.Root.Position.X-.65f)<.01f,"Enemy reached downed player");
                Need(Health(pair.Host,friend).Life==LifeState.Downed&&pair.Host.Revives==0,"Enemy within radius for ten seconds cannot revive");
                Console.WriteLine("enemy Downed");
            }finally{ally.Dispose();pipe.Dispose();}
        }
    }
}
