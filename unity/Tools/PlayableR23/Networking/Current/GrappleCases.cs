using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Traversal;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.World.Loot;
partial class LobbyCases
{
    static void GrappleCases()
    {
        var input=new R25GrappleInput();
        Need(!input.Step(false,1)&&!input.Aiming&&!input.Held,"Empty hand cannot aim/fire/claim grapple at any grip");
        Need(!input.Step(true,1)&&!input.Held,"Equipping while squeezed requires release");
        Need(!input.Step(true,0)&&input.Aiming,"Gun aims without grip or arming delay");
        Need(input.Step(true,.6f)&&input.Held,"Fresh trigger fires");
        Need(!input.Step(true,1)&&input.Held,"Holding reels without repeated fire");
        Need(!input.Step(true,0)&&!input.Held,"Trigger release detaches");
        Need(input.Step(true,1),"Next press is a new shot");input.Cancel();
        Need(!input.Step(true,1)&&!input.Held,"Menu/recovery cancellation needs neutral");
        foreach(float value in new[]{float.NaN,float.PositiveInfinity,-1,2})Need(!input.Step(true,value)&&!input.Held&&!input.Aiming,"Malformed trigger fails closed");
        var rope=new R25WebRope();input.Step(true,0);Need(input.Step(true,1)&&rope.Fire(new Float3(),new Float3(0,0,12)),"Gun launches 120m/s hook");
        Need(Near(rope.FlightRemaining,.1f,.0001f),"Muzzle flight timing");rope.Tick(.1f,input.Held,true,false);
        input.Step(true,0);rope.Tick(.01f,input.Held,true,false);Need(!rope.Attached,"Release trigger terminates solver");
        Need(!rope.Fire(new Float3(),new Float3(0,0,12)),"Refire delay survives release");for(int i=0;i<4;i++)rope.Tick(.1f,false,true,false);Need(rope.Fire(new Float3(),new Float3(0,0,12)),"Unlimited next shot after 350ms");
        var rules=R24PlayableMatchRules.ForPreset("practice");
        Need(rules.GrappleGun&&rules.GrapplePull&&!rules.With("grapple_gun",false).GrappleGun,"New rule default and disable");
        Need(!rules.With("web_slingers",false).GrappleGun,"Old source key aliases new key");
        Need(!R24PlayableMatchRules.FromSourceValues(new Dictionary<string,object>{{"web_slingers",false}}).GrappleGun,"Legacy source decoder alias");
        Need(!rules.With("grapple_pull",false).GrapplePull&&rules.With("grapple_pull",true).GrapplePull&&!rules.GrapplePullFriends,"Pull is a real replicated rule, friends opt in");
        Need(Array.IndexOf(R24PlayableMatchRules.SourceKeys,"grapple_gun")>=0&&Array.IndexOf(R24PlayableMatchRules.SourceKeys,"web_slingers")<0,"Only new gun key encoded");
        Need(R24LanLoadout.Profile(32)=="grapple"&&R23WeaponPickupRules.CatalogIndex("grapple")==32,"Catalogue maps to carried tool");
        var actual=new R23RoundState(new string[0],new ReceiptPorts(),true);string copy;
        Need(actual.CreateWeaponInstance("grapple",2,out copy)&&actual.SelectWeapon(copy),"Second genuine tool lifetime can be carried");
        var feedback=new R23WeaponFeedbackState("grapple");feedback.Advance(.01f);
        int rifle=actual.ReadAmmoReserveFamily("rifle");
        for(int i=0;i<100;i++)Need(!actual.Step(.02f,true,false,false),"Tool cannot produce firearm damage");
        Need(actual.ActiveLoaded==1&&actual.ActiveReserve==0&&actual.ReconcileNetworkAmmo(copy,1,-1,-1,0)&&actual.ReadAmmoReserveFamily("rifle")==rifle,"Unlimited charge does not debit/reconcile rifle reserve");
        int tools=0;for(int i=0;i<4096;i++)foreach(var draw in R24MixedLootRecipe.Cache("range",i))if(draw.Catalog==32){tools++;Need(draw.Stars==2,"Uncommon loot tier");}
        Need(tools>300&&tools<650,"Uncommon independent cache stream");
        using(var pair=new Pair()){
            string hash=new string('b',64);StartRound(pair,new HazardWorld(),rules.With("zone_enabled",false),hash);for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
            Need(pair.Client.SelectedMap.Rules.GrappleGun&&pair.Client.SelectedMap.Rules.GrapplePull,"Gun and pull rules replicate");
            string id;Need(pair.Client.RequestCatalog("weapon","grapple",2,1,new Float3(0,1.2f,0),out id),"Guest requests tool from creative backpack");for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);
            R24LanLootState state;Need(pair.Host.TryReadLoot(id,out state)&&state.Profile=="grapple"&&state.Owner=="","Host creates canonical tool loot");
            pair.Client.RequestLoot("cx4",1,true,new Float3(0,1.2f,0));for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
            pair.Client.RequestLoot(id,1,false,new Float3());for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);
            Need(pair.Host.TryReadLoot(id,out state)&&state.Owner==pair.Client.OwnActor,"Tool uses ordinary LAN custody");
            string second;pair.Client.RequestCatalog("weapon","grapple",2,0,new Float3(0,1.2f,0),out second);for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);
            pair.Client.RequestLoot("px4",0,true,new Float3(0,1.2f,0));for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
            pair.Client.RequestLoot(second,0,false,new Float3());for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);
            Need(pair.Host.TryReadLoot(second,out state)&&state.Owner==pair.Client.OwnActor,"Two separate grapple guns fit ordinary inventory");
            Need(pair.Host.TryReadLoot(id,out state)&&state.Owner==pair.Client.OwnActor,"Collecting second tool preserves first custody");
            Need(!pair.Host.SubmitShot("grapple",1,new Float3(),new[]{new Float3(0,0,1)},id),"Tool never admits ballistic shot claims");
            pair.Client.RequestLoot(id,1,true,new Float3(0,1.2f,0));for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadLoot(id,out state)&&state.Owner=="","Tool drops through custody");
        }
        // Existing relay tests exercise malformed, late join, release, death, reset and rule-off.
        TraversalWire();
        Console.WriteLine("P3 grapple gun CPU checks PASS; player pull separately tested, native unrun");
    }
}
