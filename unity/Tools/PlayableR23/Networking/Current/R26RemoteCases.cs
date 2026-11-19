using System;using System.Linq;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Modules.HealthStates;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    public static void RemoteCases()
    {
        int before=checks;RemoteWireCases();RemoteNetworkCases();Console.WriteLine("PASS REMOTE "+(checks-before)+" checks");
    }
    static void RemoteWireCases()
    {
        R26RemoteEquipment[] read;var rows=new[]{new R26RemoteEquipment("left","cx4",0,false,false),new R26RemoteEquipment("right","matadors",1,false,true),new R26RemoteEquipment("none",null,1,true,true)};
        var bytes=R26RemoteWire.Equipment(rows);Need(R26RemoteWire.TryEquipment(bytes,out read)&&read.Length==3&&read[0].Profile=="cx4"&&read[0].Hand==0&&read[1].Blocked&&read[2].Profile==null&&read[2].Suppressed,"Equipment profile/left/right/paired/holster flags roundtrip");
        for(int n=0;n<bytes.Length;n++)Need(!R26RemoteWire.TryEquipment(bytes.Take(n).ToArray(),out read),"Equipment truncation "+n);
        Need(!R26RemoteWire.TryEquipment(bytes.Concat(new byte[]{0}).ToArray(),out read),"Equipment trailing bytes");
        var bad=(byte[])bytes.Clone();bad[2+1+4]=254;Need(!R26RemoteWire.TryEquipment(bad,out read),"Unknown catalog rejected");bad=(byte[])bytes.Clone();bad[2+1+4+1]=16;Need(!R26RemoteWire.TryEquipment(bad,out read),"Reserved equip flags rejected");
        bool threw=false;try{R26RemoteWire.Equipment(new[]{rows[0],rows[0]});}catch(ArgumentException){threw=true;}Need(threw,"Duplicate actors rejected");
        foreach(string profile in new[]{"cx4","dt11","matadors","awp","uzis"}){R24LanBallisticProfile p;Need(R24LanBallisticProfile.TryRead(profile,out p),"Known gun");var ends=new Float3[p.Pellets];for(int i=0;i<ends.Length;i++)ends[i]=new Float3(0,0,1);var shot=new R26RemoteVolley("remote",profile,0,123,new Float3(0,1,0),ends);var encoded=R26RemoteWire.Volley(shot);R26RemoteVolley value;Need(R26RemoteWire.TryVolley(encoded,out value)&&value.Profile==profile&&value.Directions.Length==p.Pellets&&value.Hand==0&&value.Time==123,"Single/paired/pellet volley roundtrip "+profile);for(int n=0;n<encoded.Length;n++)Need(!R26RemoteWire.TryVolley(encoded.Take(n).ToArray(),out value),"Volley truncation "+profile+" "+n);Need(!R26RemoteWire.TryVolley(encoded.Concat(new byte[]{0}).ToArray(),out value),"Volley trailing bytes");}
        var limit=new R26RemoteVolley("remote","cx4",1,0,new Float3(),new[]{new Float3(1,0,0)});R26RemoteVolley atLimit;
        Need(R26RemoteWire.TryVolley(R26RemoteWire.Volley(limit),out atLimit)&&atLimit.Directions[0].X==1,"Launch wire roundtrips a unit direction");
        foreach(float value in new[]{float.NaN,float.PositiveInfinity,3700.125f}){threw=false;try{R26RemoteWire.Volley(new R26RemoteVolley("remote","cx4",1,0,new Float3(),new[]{new Float3(value,0,0)}));}catch(ArgumentException){threw=true;}Need(threw,"Invalid/nonfinite/non-unit direction refused");}
    }
    static void RemoteNetworkCases()
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float range){return Math.Min(range,7);});string hash=new string('b',64);
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("starting_shield",0).With("respawn_delay",1f);StartRound(pair,new HazardWorld(),rules,hash);for(int i=0;i<5;i++)KeepRaw(pair,hash,0,8);
            string guest=pair.Client.OwnActor,host=pair.Host.OwnActor;Need(pair.Client.RequestEquipment("cx4",0,false),"Guest left-hand equip declared");Need(pair.Host.RequestEquipment("px4",1,false),"Host right-hand equip accepted");for(int i=0;i<8;i++)KeepRaw(pair,hash,0,8);
            R26RemoteEquipment equipment;Need(pair.Host.TryReadRemoteEquipment(guest,out equipment)&&equipment.Profile=="cx4"&&equipment.Hand==0,"Host sees accepted guest equip");Need(pair.Client.TryReadRemoteEquipment(host,out equipment)&&equipment.Profile=="px4"&&equipment.Hand==1,"Guest sees host equip");
            Need(pair.Client.RequestEquipment(null,0,false),"Guest holsters");for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadRemoteEquipment(guest,out equipment)&&equipment.Profile==null,"Holster clears held art");Need(pair.Client.RequestEquipment("cx4",1,false),"Guest changes hand");for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);
            var origin=new Float3(.4f,1.6f,0);var up=new[]{new Float3(0,1,0)};Need(pair.Client.SubmitShot("cx4",0,origin,up),"Guest submits genuine volley");for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);R26RemoteVolley shot;Need(pair.Host.TryDequeueRemoteVolley(out shot)&&shot.Actor==guest&&shot.Hand==1&&Math.Abs(shot.Directions[0].Y-1f)<.0001f,"Host observes guest shot, correct physical hand and upward launch direction");Need(!pair.Client.TryDequeueRemoteVolley(out shot),"Shooter receives no duplicate feedback");
            Need(pair.Host.SubmitShot("px4",0,new Float3(8.4f,1.6f,0),up),"Host submits genuine volley");for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);Need(pair.Client.TryDequeueRemoteVolley(out shot)&&shot.Actor==host&&shot.Profile=="px4","Guest receives accepted host volley");
            int count=pair.Host.RemoteVolleysReceived;Need(pair.Client.SubmitShot("cx4",0,new Float3(500,1,0),up),"Bad origin command queued");for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.RemoteVolleysReceived==count,"Rejected volley emits no visual receipt");
            var h=Health(pair.Host,guest);Need(pair.Host.ApplyHostHealthLoss(guest,h.Revision,100,0,true).Accepted,"Host commits actual lethal loss");for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);Need(Health(pair.Client,guest).Life==LifeState.Dead&&Health(pair.Host,guest).Health==0,"Dead/zero health replicate");for(int i=0;i<30;i++)KeepRaw(pair,hash,0,8);Need(Health(pair.Client,guest).Life==LifeState.Alive&&Health(pair.Client,guest).Health==100,"Respawn restores replicated life");
        }
    }
}
