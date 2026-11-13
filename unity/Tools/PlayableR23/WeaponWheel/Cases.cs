using System;
using Ashfall.Integration.Playable.Input;
static class Cases
{
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static R23WeaponWheelState New(){var state=new R23WeaponWheelState();state.SetSlots(new[]{new R23WeaponWheelSlot("a","MK18","mk18"),new R23WeaponWheelSlot("b","AKM","akm"),new R23WeaponWheelSlot("c","SPR","spr")});return state;}
    static void Arm(R23WeaponWheelState s,int hand=1){s.Step(.02f,false,true,-1,hand);}
    static void Open(R23WeaponWheelState s,int sector=2,int hand=1){Arm(s,hand);s.Step(.11f,true,true,sector,hand);}
    static int Main(){try{
        for(int i=0;i<8;i++){double angle=Math.PI*.5-i*Math.PI*.25;Check(R23WeaponWheelRules.Sector((float)Math.Cos(angle)*.215f,(float)Math.Sin(angle)*.215f)==i,"source clockwise sector "+i);}
        Check(R23WeaponWheelRules.Sector(0,0)==-1&&R23WeaponWheelRules.Sector(.101f,0)==-1,"source inner deadzone cancels");
        Check(R23WeaponWheelRules.Sector(10,0)==2,"source sector pointing remains valid beyond outer pointer clamp");
        Check(R23WeaponWheelRules.Sector(float.NaN,0)==-1&&R23WeaponWheelRules.Sector(0,float.PositiveInfinity)==-1,"invalid ray coordinates reject");
        var s=New();Check(s.Step(.2f,true,true,2,1)==null&&!s.Active,"new component requires physical A release before arming");
        Arm(s);s.Step(.1f,true,true,2,1);Check(!s.Active,"source quick tap .1 boundary does not open");var request=s.Step(.01f,false,true,2,1);Check(request!=null&&request.Action==R23WeaponWheelAction.QuickToggle,"source rapid A tap toggles held item, never emits jump/equip");
        Open(s);Check(s.Active&&s.HighlightedSector==2,"source hold threshold opens owned wheel");request=s.Step(.02f,false,true,2,1);Check(request!=null&&request.Id=="a"&&request.Hand==1&&request.Slot==0&&!s.Active,"right sector releases to first real owned slot");
        Check(s.Step(.02f,false,true,2,1)==null,"repeated release cannot duplicate equip request");
        Open(s,1,0);request=s.Step(.02f,false,true,1,0);Check(request!=null&&request.Id=="b"&&request.Hand==0,"left dominant hand selects same source upper-right slot mapping");
        Open(s,2);request=s.Step(.02f,false,true,0,1);Check(request!=null&&request.Id=="c","release recomputes current pointed sector rather than stale hover");
        Open(s,2);Check(s.Step(.02f,false,true,-1,1)==null&&!s.Active,"long hold release at center cancels");
        Open(s,4);request=s.Step(.02f,false,true,4,1);Check(request!=null&&request.Action==R23WeaponWheelAction.Backpack,"source bottom utility requests backpack without pause mutation");
        Open(s,5);Check(s.Step(.02f,false,true,5,1)==null,"capacity3 locked arc never grants a fourth item");
        Open(s,3);Check(s.Step(.02f,false,true,3,1)==null,"unimplemented revive utility cannot fake an equip");
        Open(s);s.Step(.02f,true,false,2,1);s.Step(.2f,true,true,2,1);Check(!s.Active&&s.Step(.02f,false,true,2,1)==null,"focus/tracking loss cancels and held A cannot reopen on recovery");
        Open(s);s.Step(.2f,true,true,2,0);Check(!s.Active&&s.Step(.02f,false,true,2,0)==null,"dominance changes cancel and require new neutral edge");
        Open(s);s.SetSlots(new[]{new R23WeaponWheelSlot("replacement","AKM","akm")});Check(!s.Active&&s.Step(.02f,false,true,2,1)==null,"live inventory identity replacement cancels stale held selection");
        Open(s);s.SetSlots(new[]{new R23WeaponWheelSlot("replacement","AKM","akm","20 / 1100")});Check(s.Active,"live ammo/label refresh preserves a stable owned selection");
        s.SetSlots(new[]{new R23WeaponWheelSlot("replacement","AKM","akm","",false)});Check(!s.Active,"equip eligibility loss cancels stale request");
        Open(s);Check(s.Step(.02f,false,true,2,1)==null,"unavailable supplied slot cannot request equip");
        var holes=New();holes.SetSlots(new R23WeaponWheelSlot[]{null,new R23WeaponWheelSlot("middle","AKM","akm")});Open(holes,2);Check(holes.Step(.02f,false,true,2,1)==null,"null current slot remains empty without compaction or grant");
        bool reject=false;try{s.SetSlots(new R23WeaponWheelSlot[4]);}catch(ArgumentException){reject=true;}Check(reject,"more than three supplied slots rejected");
        reject=false;try{s.SetSlots(new[]{new R23WeaponWheelSlot("same","A","mk18"),new R23WeaponWheelSlot("same","B","akm")});}catch(ArgumentException){reject=true;}Check(reject,"duplicate owned instance identities rejected");
        var copies=New();copies.SetSlots(new[]{new R23WeaponWheelSlot("copy/one","1911 ONE","1911"),new R23WeaponWheelSlot("copy/two","1911 TWO","1911")});Open(copies,1);request=copies.Step(.02f,false,true,1,1);
        Check(request!=null&&request.Id=="copy/two"&&copies.Slot(1).WeaponId=="1911","same-profile copies retain distinct equip action ID and shared genuine profile icon");
        Check(new R23WeaponWheelSlot("copy/rarity","WRONG NAME","mk18",stars:5).Label=="MK18 \u2605\u2605\u2605\u2605\u2605","source name plus actual five stars without stats/aliases");
        Check(new R23WeaponWheelSlot("one","MK18 / M4A1","mk18").Label=="MK18 \u2605","one-star source rarity is visible too");
        Check(new R23WeaponWheelSlot("zone/one","wrong","zone",stars:5).Label=="ZONE GRENADE","source untiered zone has no stars");
        Check(R23InventoryPresentation.Caption("spr","SPR (2x) \u2605\u2605")=="SPR \u2605\u2605","source SPR label preserves inferred instance stars and removes implementation alias");
        Console.WriteLine(count+" source wheel behavior cases passed");return 0;
    }catch(Exception error){Console.WriteLine(error);return 1;}}
}
