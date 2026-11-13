#if ASHFALL_FEEDBACK_CHECKS
using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    static class R23WeaponFeedbackChecks
    {
        sealed class Query:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float e){return true;}}
        static int cases;
        static void Check(bool okay,string label){if(!okay)throw new Exception(label);cases++;System.Console.WriteLine("PASS "+label);}
        static bool Near(float a,float b){return Math.Abs(a-b)<.00001f;}
        static int Main(){try{
            Check(R23WeaponFeedbackState.FlashSeconds==.045f&&R23WeaponFeedbackState.TracerSeconds==.045f&&R23WeaponFeedbackState.ImpactSeconds==.11f,"source flash/tracer45ms and impact110ms lifetimes");
            Check(R23WeaponFeedbackState.ReserveText(2400)=="2400"&&R23WeaponFeedbackState.ReserveText(99999)=="99999"&&R23WeaponFeedbackState.ReserveText(100000)=="\u221e"&&R23WeaponFeedbackState.ReserveText(-1)=="\u221e","gun panel source finite/infinite reserve formatting");
            var mk=new R23WeaponFeedbackState("mk18");Check(!mk.Flash&&mk.Distance==0&&mk.AcceptedShots==0,"new presentation has no fabricated shot/effects");
            Check(mk.AcceptedShot(1,false)&&Near(mk.Distance,.052f*1.25f)&&Near(mk.PitchRadians,5.5f*(float)Math.PI/180*1.25f),"source MK18 first accepted shot has exact render kick");
            float prior=mk.Distance;Check(!mk.AcceptedShot(1,false)&&!mk.AcceptedShot(0,false)&&mk.Distance==prior,"duplicate/stale native shot serial cannot replay recoil or flash");
            mk.Advance(.044f);Check(mk.Flash&&mk.Bloom>0,"source accepted-shot flash and reference crosshair bloom are live before45ms");mk.Advance(.002f);Check(!mk.Flash,"flash expires independently of ammo or trigger state");
            var pulse=new R23WeaponFeedbackState("mk18");pulse.AcceptedShot(1,false);pulse.Advance(.055f);Check(Near(pulse.Bloom,32)&&Near(pulse.ReticleWidth(false),102)&&Near(pulse.ReticleWidth(true),90),"source reference32px bloom at55ms,70/58px unsupported/supported rest width");
            var two=new R23WeaponFeedbackState("mk18");two.AcceptedShot(1,true);Check(Near(two.Distance,.052f*1.25f*.38f)&&Near(two.PitchRadians,5.5f*(float)Math.PI/180*1.25f*.38f),"actual supported shot uses source38percent render recoil gain");
            Check(Near(R23WeaponFeedbackState.SprPulse(.09f),1)&&Near(R23WeaponFeedbackState.SprPulse(.22f),0)&&Near(R23WeaponFeedbackState.SprPulse(.33f),-.3f)&&Near(R23WeaponFeedbackState.SprPulse(.48f),0),"source SPR signed Hermite peak/zero/undershoot/end timing");
            var spr=new R23WeaponFeedbackState("spr");spr.AcceptedShot(1,false);spr.Advance(.09f);Check(Near(spr.PitchRadians,6.776f*(float)Math.PI/180*1.25f),"source SPR visual pulse peak includes retained gain1.25");spr.Advance(.24f);Check(spr.PitchRadians<0,"SPR settles through source signed undershoot");
            var ak=new R23WeaponFeedbackState("akm");ak.AcceptedShot(1,false);Check(Near(ak.PitchRadians,2.74705024f*(float)Math.PI/180*1.25f),"AKM uses shipped source endpoint experiment, not generic rifle curve");
            var akTwo=new R23WeaponFeedbackState("akm");akTwo.AcceptedShot(1,true);Check(Near(akTwo.PitchRadians,1.53706032f*(float)Math.PI/180*1.25f),"AKM supported presentation uses actual separate source endpoint profile");
            mk.ClearPresentation();Check(mk.Distance==0&&mk.PitchRadians==0&&!mk.Flash&&mk.Bloom==0&&mk.AcceptedShots==1,"holster/focus suppression clears visuals without changing native shot identity");mk.Reset();Check(mk.AcceptedShots==0&&mk.AcceptedShot(1,false),"explicit round restart rearms presentation serial without ammo access");
            var round=new R23RoundState(new string[0],new Query(),true);var actual=new R23WeaponFeedbackState("mk18");int shots=0;
            foreach(float dt in new[]{0f,.08f,.08f}){bool fired=round.Step(dt,new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=true,ForegripDistance=1},false,false);actual.Advance(dt);if(fired){shots++;actual.AcceptedShot(round.Rifle.Snapshot.Shots,false);}}
            Check(shots==3&&actual.AcceptedShots==3&&round.Rifle.LoadedRounds==15&&round.Rifle.Reserve==2400,"real native committed burst emits exactly3 feedback events, single authoritative ammo debit");
            int ammo=round.Rifle.LoadedRounds;bool paused=round.Step(.1f,new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=true},false,true);Check(!paused&&round.Rifle.LoadedRounds==ammo&&actual.AcceptedShots==3,"paused native owner cannot mint feedback or consume ammo");
            for(int i=0;i<5;i++)two.Advance(.2f);Check(two.PitchRadians==0&&two.Distance==0,"source800ms recovery resets render kick");
            Console.WriteLine(cases+" focused feedback cases passed");return 0;
        }catch(Exception error){Console.WriteLine(error);return 1;}}
    }
}
#endif
