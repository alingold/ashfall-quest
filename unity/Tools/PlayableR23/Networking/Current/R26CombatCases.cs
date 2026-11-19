// Campaign r12 COMBAT CPU cases (WPN-CX4-SEMI, MP-HITMARKER, MP-RETICLE).
// Proposed for Tools/PlayableR23/Networking/Current/ (LAN harness, C# 5). Compile with the
// LAN case sources plus R26LanAmmoProjection.cs and R26RemoteAimVolumes.cs; entry R26CombatMain.
using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Modules.WeaponHandling;using Ashfall.Modules.HealthStates;
using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    internal static void R26Combat()
    {
        int before=checks;R26HeldTrigger();R26JitterAdmission();R26ForgedStamps();R26HitConfirms();R26ShotgunReach();R26ReticleVolumes();
        Console.WriteLine("PASS R26 combat cases: "+(checks-before)+" checks");
    }
    // ---------- WPN-CX4-SEMI ----------
    // Emulates R24LanSession.SyncAmmo for one owned instance: the pre-fix skip rule (legacy) or R26LanAmmoProjection.
    sealed class AmmoMirror{internal R24LanAmmoState Prior;internal int Applied;}
    static void MirrorAmmo(R24LanLobby lobby,R23RoundState native,string id,AmmoMirror m,bool legacy,double sinceVolley)
    {
        R24LanAmmoState state;if(!lobby.TryReadOwnAmmo(id,out state)||!lobby.OwnsAmmo(state))return;var prior=m.Prior;
        bool apply=legacy?!(prior!=null&&prior.Epoch==state.Epoch&&prior.Acknowledged>=lobby.AmmoPredictionSequence&&prior.Loaded==state.Loaded&&prior.Left==state.Left&&prior.Right==state.Right&&prior.Reserve==state.Reserve)
            :R26LanAmmoProjection.ShouldApply(prior,state,lobby.AmmoPredictionSequence,sinceVolley);
        if(apply&&native.ReconcileNetworkAmmo(id,state.Loaded,state.Left,state.Right,state.Reserve)){m.Prior=state;m.Applied++;}
    }
    static bool DropFirstShot(Pipe pipe){var kept=new List<Frame>();bool dropped=false;while(pipe.Inbox.Count>0){var f=pipe.Inbox.Dequeue();Packet p;if(!dropped&&Wire.TryDecode(f.Bytes,out p)&&p.FeatureId=="playable.combat.shot/1"){dropped=true;continue;}kept.Add(f);}foreach(var f in kept)pipe.Inbox.Enqueue(f);return dropped;}
    // A real native CX4 owner held for 1 s at 72 Hz, then released for 1 s, while the actual
    // host/guest lobbies tick every frame and their ammo rows are projected back each frame.
    static int HoldCx4(string profile,bool legacy,bool hostShooter,bool dropOne,out int local,out int ledger)
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);
            var shooter=hostShooter?pair.Host:pair.Client;var native=new R23RoundState(new string[0],new ReceiptPorts(),true);R23WeaponInstanceInfo info;string id=profile;
            if(profile!="cx4"){Need(hostShooter,"World custody fixture is host-side");id="shared/"+profile;Need(pair.Host.RequestLoot("px4",1,true,new Float3(8,1,0)),"Free a slot");Need(pair.Host.RegisterWorldWeapon(id,profile,1,new Float3(8,1,0))&&pair.Host.RequestLoot(id,1,false,new Float3()),"Host takes custody of "+profile);}
            if(!native.TryReadWeaponInstance(id,out info))Need(native.RegisterWeaponInstance(id,profile,1),"Native "+profile+" owner");Need(native.SelectWeapon(id),"Select native "+profile);
            var muzzle=hostShooter?new Float3(7.6f,1.6f,0):new Float3(.4f,1.6f,0);var away=Dir(muzzle,new Float3(muzzle.X,50,0));
            var mirror=new AmmoMirror();double now=pair.Now,lastVolley=Double.NegativeInfinity;const double dt=1.0/72;int shots=0;bool dropped=!dropOne;
            for(int frame=0;frame<150;frame++){
                bool held=frame>=6&&frame<78;now+=dt;pair.Now=now; // released first: selection arms requireTriggerRelease
                if(frame%4==0){pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0));}
                pair.Host.Tick(now);pair.Client.Tick(now);if(!dropped&&shots>=3)dropped=DropFirstShot(pair.HostPipe);
                MirrorAmmo(shooter,native,id,mirror,legacy,now-lastVolley);
                if(native.Step((float)dt,new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=held},false,false)){if(held)shots++;Need(shooter.SubmitShot(profile,0,muzzle,new[]{away},id),"Local volley submitted");lastVolley=now;}
            }
            int reserve;local=native.ReadWeapon(id).Loaded;Need(pair.Host.TryReadAmmo(shooter.OwnActor,id,out ledger,out reserve),"Host ledger readable");return shots;
        }
    }
    static void R26HeldTrigger()
    {
        int local,ledger;
        int legacy=HoldCx4("cx4",true,true,false,out local,out ledger);
        Need(legacy<=2,"Reproduction: pre-fix host projection turns the held CX4 semi-automatic (shots="+legacy+")");Console.WriteLine("host cx4 held 1 s: pre-fix shots="+legacy);
        int shots=HoldCx4("cx4",false,true,false,out local,out ledger);
        Need(shots>=6,"Host held CX4 fires at its 6/s automatic cadence (shots="+shots+")");
        Need(local==ledger&&ledger==25-shots,"Host native magazine settles to the host ledger (local="+local+" ledger="+ledger+" shots="+shots+")");Console.WriteLine("host cx4 held 1 s: fixed shots="+shots+" local="+local+" ledger="+ledger);
        shots=HoldCx4("cx4",false,false,false,out local,out ledger);
        Need(shots>=6&&local==ledger&&ledger==25-shots,"Guest held CX4 automatic and consistent (shots="+shots+" local="+local+" ledger="+ledger+")");
        legacy=HoldCx4("cx4",true,false,true,out local,out ledger);
        Need(legacy<6,"Reproduction: a host-side correction mid-burst stops the guest's held trigger (shots="+legacy+")");Console.WriteLine("guest cx4 held 1 s, one shot lost before the host: pre-fix shots="+legacy);
        shots=HoldCx4("cx4",false,false,true,out local,out ledger);
        Need(shots>=6,"Guest correction waits for the trigger to settle; burst continues (shots="+shots+")");
        Need(local==ledger&&ledger==25-shots+1,"Settled guest magazine adopts the host correction (local="+local+" ledger="+ledger+" shots="+shots+")");Console.WriteLine("guest cx4 held 1 s, one shot lost before the host: fixed shots="+shots+" local="+local+" ledger="+ledger);
        foreach(string profile in new[]{"mk18","ump","p90","mp5","mp9","akm","m60","tec9","rfb"}){
            int before=HoldCx4(profile,true,true,false,out local,out ledger),after=HoldCx4(profile,false,true,false,out local,out ledger);
            Need(after>before&&local==ledger,"Host automatic "+profile+" no longer stalls (legacy="+before+" fixed="+after+" local="+local+" ledger="+ledger+")");Console.WriteLine("host "+profile+" held 1 s: pre-fix shots="+before+" fixed shots="+after+" local="+local+" ledger="+ledger);
        }
        Need(!R26LanAmmoProjection.ShouldApply(null,null,0,10),"No row, no correction");
    }
    // Host admission under Relay/Wi-Fi bunching: a guest's held trigger, client->host frames held
    // and released together every stall period. Every locally fired round must be admitted.
    static void BunchedGuestBurst(string profile,int stallMs,out int shots,out int accepted,out int rejected)
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);string id=profile;
            if(profile!="cx4"){id="shared/"+profile;Need(pair.Host.RegisterWorldWeapon(id,profile,5,new Float3(0,1,0)),"World "+profile);Need(pair.Client.RequestLoot("px4",1,true,new Float3(0,1,0)),"Guest frees a slot");for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);Need(pair.Client.RequestLoot(id,1,false,new Float3()),"Guest takes "+profile);for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);}
            var native=new R23RoundState(new string[0],new ReceiptPorts(),true);R23WeaponInstanceInfo info;if(!native.TryReadWeaponInstance(id,out info))Need(native.RegisterWeaponInstance(id,profile,profile=="cx4"?1:5),"Native owner");Need(native.SelectWeapon(id),"Select");
            var muzzle=new Float3(.4f,1.6f,0);var away=Dir(muzzle,new Float3(.4f,50,0));double now=pair.Now,dt=1.0/72;int stall=stallMs*72/1000,acc=pair.Host.AcceptedShots,rej=pair.Host.RejectedShots;var held=new List<Frame>();shots=0;
            for(int frame=0;frame<216;frame++){bool trigger=frame>=6&&frame<150;now+=dt;pair.Now=now;
                if(frame%4==0){pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0));}
                pair.Host.Tick(now);pair.Client.Tick(now);if(stall>0){while(pair.HostPipe.Inbox.Count>0)held.Add(pair.HostPipe.Inbox.Dequeue());if(frame%stall==0){foreach(var f in held)pair.HostPipe.Inbox.Enqueue(f);held.Clear();}}
                if(native.Step((float)dt,new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=trigger},false,false)){shots++;Need(pair.Client.SubmitShot(profile,0,muzzle,new[]{away},id),"Guest volley queued");}
            }
            for(int i=0;i<6;i++){foreach(var f in held)pair.HostPipe.Inbox.Enqueue(f);held.Clear();KeepRaw(pair,hash,0,8);}
            accepted=pair.Host.AcceptedShots-acc;rejected=pair.Host.RejectedShots-rej;
        }
    }
    static void R26JitterAdmission()
    {
        foreach(string profile in new[]{"cx4","p90"})foreach(int stall in new[]{0,300,500}){int shots,accepted,rejected;BunchedGuestBurst(profile,stall,out shots,out accepted,out rejected);
            Console.WriteLine("guest "+profile+" 2 s burst, "+stall+" ms bunching: fired="+shots+" host accepted="+accepted+" rejected="+rejected);
            Need(shots>0&&accepted==shots&&rejected==0,"Bunched legitimate "+profile+" volleys all admitted at "+stall+" ms (fired="+shots+" accepted="+accepted+" rejected="+rejected+")");}
    }
    // Forged stamps: a guest that back-dates stamps and sends as fast as the cadence gap allows is still held
    // to ceil(rate)+2 per stamp second; denser stamps are cadence rejections.
    static void R26ForgedStamps()
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("infinite_ammo",true);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);string guest=pair.Client.OwnActor;
            var muzzle=new Float3(.4f,1.6f,0);var away=Dir(muzzle,new Float3(.4f,50,0));double gap=.65/6+.0005,stamp=pair.Host.EstimatedHostTime-.74;long sequence=500000;int acc=pair.Host.AcceptedShots,cadence=0;double start=pair.Now;
            for(int tick=0;tick<60;tick++){
                while(stamp<=pair.Now+.04){var shot=new R24LanShot("cx4",0,stamp,muzzle,new[]{away});pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(new Packet{Kind=PacketKind.Command,FeatureId="playable.combat.shot/1",ActorId=guest,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=++sequence,BaseSequence=-1,Payload=shot.Encode()})});stamp+=gap;}
                pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0));pair.Tick();if(pair.Host.LastShotRejection=="cadence")cadence++;
                if(pair.Host.AcceptedShots-acc>=25)break;
            }
            double seconds=pair.Now-start+.75;int accepted=pair.Host.AcceptedShots-acc;
            Console.WriteLine("forged gap-spaced stamps: accepted="+accepted+" over "+seconds.ToString("0.00")+" stamp-seconds");
            Need(accepted>0&&accepted<=Math.Ceiling(seconds)*8,"Forged stamps sustain at most ceil(rate)+2 per second (accepted="+accepted+" seconds="+seconds+")");
            var dense=new R24LanShot("cx4",0,pair.Host.EstimatedHostTime,muzzle,new[]{away});int rejected=pair.Host.RejectedShots;
            for(int i=0;i<3;i++)pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(new Packet{Kind=PacketKind.Command,FeatureId="playable.combat.shot/1",ActorId=guest,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=++sequence,BaseSequence=-1,Payload=dense.Encode()})});
            pair.Tick();Need(pair.Host.RejectedShots>rejected,"Same-stamp forged burst is still refused");
        }
    }
    // ---------- MP-HITMARKER ----------
    static List<byte[]> HitFrames(Pipe pipe){var found=new List<byte[]>();var all=new List<Frame>();while(pipe.Inbox.Count>0)all.Add(pipe.Inbox.Dequeue());foreach(var f in all){Packet p;if(Wire.TryDecode(f.Bytes,out p)&&p.FeatureId=="playable.score/1"&&p.Payload.Length>0&&p.Payload[0]==2)found.Add((byte[])f.Bytes.Clone());pipe.Inbox.Enqueue(f);}return found;}
    static void R26HitConfirms()
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("starting_shield",0).With("respawn_delay",1f);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);string guest=pair.Client.OwnActor,host=pair.Host.OwnActor;for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);R26HitConfirm hit;
            // Guest hits the host body: one reliable confirmation reaches the guest only.
            var gm=new Float3(.4f,1.6f,0);Need(pair.Client.SubmitShot("cx4",0,gm,new[]{Dir(gm,new Float3(8,1.25f,0))}),"Guest volley queued");
            pair.Tick();pair.Now+=.05;pair.Host.Tick(pair.Now);var frames=HitFrames(pair.ClientPipe);Need(frames.Count==1,"One host hit frame for the remote shooter (frames="+frames.Count+")");pair.Client.Tick(pair.Now);
            Need(pair.Client.TryDequeueHitConfirm(out hit)&&hit.Victim==host&&Math.Abs(hit.Total-19)<.01f&&hit.Shield==0&&!hit.Headshot&&!hit.Kill&&hit.Located,"Guest marker carries actual host loss 19 (total="+(hit==null?-1:hit.Total)+")");
            Need(Math.Abs(hit.Point.X-8)<.01f&&Math.Abs(hit.Point.Y-1.7f)<.01f,"Marker anchor is the victim head on the host pose");
            Need(!pair.Client.TryDequeueHitConfirm(out hit)&&!pair.Host.TryDequeueHitConfirm(out hit),"Exactly one marker, none for the victim");
            pair.ClientPipe.Inbox.Enqueue(new Frame{Peer="server",Bytes=frames[0]});pair.Tick();Need(!pair.Client.TryDequeueHitConfirm(out hit)&&pair.Client.HitConfirmsReceived==1,"Replayed hit frame is ignored");
            pair.ClientPipe.Inbox.Enqueue(new Frame{Peer="server",Bytes=Wire.Encode(new Packet{Kind=PacketKind.Snapshot,FeatureId="playable.score/1",ActorId=host,SessionId=pair.Client.SessionId,ContentHash=new string('a',64),Epoch=pair.Client.Epoch,Sequence=900000,BaseSequence=-1,Payload=new byte[]{2,9,9}})});pair.Tick();
            Need(!pair.Client.TryDequeueHitConfirm(out hit)&&pair.Client.State==R24LanLobbyState.Connected,"Malformed hit frame ignored without dropping the connection");
            for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);R24LanScoreRow row;Need(pair.Client.TryReadScore(host,out row),"Score rows still replicate beside hit frames");
            // Host headshot on the guest: confirmed locally, never sent to the victim.
            var muzzle=new Float3(7.6f,1.6f,0);Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,new Float3(0,1.7f,0))}),"Host head volley");pair.Tick();
            Need(pair.Host.TryDequeueHitConfirm(out hit)&&hit.Victim==guest&&hit.Headshot&&Math.Abs(hit.Total-23)<.01f&&!hit.Kill,"Host headshot marker, actual head damage 23");
            for(int i=0;i<2;i++)KeepRaw(pair,hash,0,8);Need(!pair.Client.TryDequeueHitConfirm(out hit),"Victim guest receives no shooter marker");
            // Miss: no marker.
            for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,new Float3(0,1.7f,3))})&&!pair.Host.TryDequeueHitConfirm(out hit),"Miss confirms nothing");
            // Shotgun: eight pellets, one grouped marker; lethal volley is the kill variant.
            for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);var chest=Dir(muzzle,new Float3(0,1.25f,0));int events=pair.Host.CombatDamageEvents;
            Need(pair.Host.SubmitShot("m1014",0,muzzle,new[]{chest,chest,chest,chest,chest,chest,chest,chest}),"Host shotgun volley");pair.Tick();
            Need(pair.Host.CombatDamageEvents==events+1&&pair.Host.TryDequeueHitConfirm(out hit)&&hit.Kill&&Math.Abs(hit.Total-77)<.01f&&!pair.Host.TryDequeueHitConfirm(out hit),"One grouped shotgun marker; lethal volley marks the kill (total="+(hit==null?-1:hit.Total)+")");
            Need(Health(pair.Host,guest).Life!=LifeState.Alive,"Guest actually dead");
            for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,new Float3(0,1.25f,0))})&&!pair.Host.TryDequeueHitConfirm(out hit),"Dead target yields no marker");
            Need(pair.Host.HitConfirmsSent==1&&pair.Host.HitConfirmsQueued==3,"Counters: 1 remote frame sent, 3 confirmations queued (sent="+pair.Host.HitConfirmsSent+" queued="+pair.Host.HitConfirmsQueued+")");
        }
    }
    // A distant host volley groups eight scaled pellets in a single confirmation.
    static void R26ShotgunReach()
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("starting_shield",0);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);string guest=pair.Client.OwnActor;float hx=8;for(int i=0;i<3;i++)KeepRaw(pair,hash,0,hx);
            while(hx<45){hx=Math.Min(45,hx+.2f);KeepRaw(pair,hash,0,hx);}for(int i=0;i<4;i++)KeepRaw(pair,hash,0,hx);
            var muzzle=new Float3(hx-.4f,1.6f,0);var chest=Dir(muzzle,new Float3(0,1.25f,0));var rays=new[]{chest,chest,chest,chest,chest,chest,chest,chest};R26HitConfirm hit;float before=Health(pair.Host,guest).Health;
            Need(pair.Host.SubmitShot("m1014",0,muzzle,rays),"Far shotgun volley accepted (consumes a shell)");for(int i=0;i<3;i++)KeepRaw(pair,hash,0,hx);
            Need(pair.Host.TryDequeueHitConfirm(out hit)&&hit.Victim==guest&&Math.Abs(hit.Total-19.25f)<.01f&&Math.Abs(Health(pair.Host,guest).Health-(before-19.25f))<.01f&&!pair.Host.TryDequeueHitConfirm(out hit),"A distant volley applies and confirms one grouped 25%-damage hit");
            while(hx>30){hx=Math.Max(30,hx-.2f);KeepRaw(pair,hash,0,hx);}for(int i=0;i<4;i++)KeepRaw(pair,hash,0,hx);muzzle=new Float3(hx-.4f,1.6f,0);chest=Dir(muzzle,new Float3(0,1.25f,0));rays=new[]{chest,chest,chest,chest,chest,chest,chest,chest};
            before=Health(pair.Host,guest).Health;Need(pair.Host.SubmitShot("m1014",0,muzzle,rays),"Volley accepted");for(int i=0;i<2;i++)KeepRaw(pair,hash,0,hx);Need(pair.Host.TryDequeueHitConfirm(out hit)&&hit.Victim==guest&&Math.Abs(Health(pair.Host,guest).Health-(before-19.25f))<.01f,"25% plateau also applies at 29.6 m");
        }
    }
    // ---------- MP-RETICLE ----------
    static void R26ReticleVolumes()
    {
        Need(Math.Abs(R26RemoteAimVolumes.Nearest(0,1.7f,-5,0,0,1,0,1.7f,0)-(5-.17f))<1e-4,"Head sphere distance");
        Need(Math.Abs(R26RemoteAimVolumes.Nearest(0,1.25f,-5,0,0,1,0,1.7f,0)-(5-.29f))<1e-4,"Chest sphere distance");
        Need(Math.Abs(R26RemoteAimVolumes.Nearest(0,.78f,-5,0,0,1,0,1.7f,0)-(5-.27f))<1e-4,"Hip sphere distance");
        Need(Single.IsPositiveInfinity(R26RemoteAimVolumes.Nearest(.5f,1.7f,-5,0,0,1,0,1.7f,0)),"Ray beside the avatar misses");
        Need(Single.IsPositiveInfinity(R26RemoteAimVolumes.Nearest(0,1.7f,-5,0,0,-1,0,1.7f,0)),"Avatar behind the ray misses");
        Need(Single.IsPositiveInfinity(R26RemoteAimVolumes.Nearest(0,1.7f,0,0,0,1,0,1.7f,0)),"Ray from inside a sphere ignores it (host SphereRay)");
        // The reticle volumes agree with the host's actual hit decisions along the same rays.
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("starting_shield",0).With("damage",.1f);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);var muzzle=new Float3(7.6f,1.6f,0);int agree=0;
            float[][] aims={new[]{0,1.7f,0},new[]{0,1.95f,0},new[]{0,1.25f,.2f},new[]{0,1.25f,.45f},new[]{0,.6f,0},new[]{0,.3f,0},new[]{0,1.48f,.25f},new[]{0,1.0f,0},new[]{0,2.2f,0},new[]{0,1.7f,.3f}};
            int shots=0;foreach(var aim in aims){
                if(++shots>20){Need(pair.Host.SubmitReload("cx4",-1),"Reload");shots=0;}
                for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);var d=Dir(muzzle,new Float3(aim[0],aim[1],aim[2]));
                bool predicted=!Single.IsPositiveInfinity(R26RemoteAimVolumes.Nearest(muzzle.X,muzzle.Y,muzzle.Z,d.X,d.Y,d.Z,0,1.7f,0));
                Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{d}),"Probe volley accepted");pair.Tick();R26HitConfirm hit;bool actual=pair.Host.TryDequeueHitConfirm(out hit);
                Need(predicted==actual,"Reticle volume and host hit agree at ("+aim[0]+","+aim[1]+","+aim[2]+") predicted="+predicted+" actual="+actual);agree++;
            }
            Need(agree==aims.Length,"All probe rays agree");
        }
    }
}
