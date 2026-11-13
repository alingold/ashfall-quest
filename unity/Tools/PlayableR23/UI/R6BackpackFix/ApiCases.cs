using System;
using Ashfall.Integration.Playable.UI;
using Ashfall.Integration.Playable.Input;
static class ApiCases
{
    static int count;
    static void Check(bool value,string name){count++;if(!value)throw new Exception(name);}
    static void Main()
    {
        string[] ids={"knife","katana","excalibur","cyberblade"};string[] names={"FIELD KNIFE","ASH KATANA","RIDGE EXCALIBUR","ARC CYBERBLADE"};
        for(int i=0;i<ids.Length;i++)
        {
            Check(!R23InventoryPresentation.Tiered(ids[i]),"source non-tiered melee");
            Check(R23InventoryPresentation.Caption(ids[i],"STALE NAME",5)==names[i],"exact source name without fake stars");
        }
        Check(R23InventoryPresentation.Tiered("smaw")&&R23InventoryPresentation.Caption("smaw","",2)=="SMAW \u2605\u2605","source27 retains actual tier");
        var a=new R23UiSettings();var b=a.With(damping:.65f,offhand:.4f,gripToggle:true,dominantHand:0);
        Check(a.MotionDamping==1&&a.OffhandInfluence==0&&!a.GripToggle&&a.DominantHand==1,"original immutable settings intact");
        Check(b.MotionDamping==.65f&&b.OffhandInfluence==.4f&&b.GripToggle&&b.DominantHand==0,"all four actual weapon settings publish");
        var bounded=b.With(damping:3,offhand:-1);Check(bounded.MotionDamping==2&&bounded.OffhandInfluence==0,"source tuning ranges");
        var first=new R23LoadoutOption("copy/17","SMAW","",1,false,1,true,true,"smaw",2);
        var second=new R23LoadoutOption("copy/18","FIELD KNIFE","",1,false,0,true,true,"knife",1);
        var kit=new R23OwnedLoadout(new[]{first,second},6);var exposed=kit.Options;exposed[0]=second;
        Check(kit.Capacity==6&&kit.Slot(0).Id=="copy/17"&&kit.Slot(1).Id=="copy/18","six-slot/action IDs and clone ownership preserved");
        var gesture=new R23LoadoutGesture();
        Check(gesture.Sample(1,"copy/17",true,false,true,true)==null,"held opener neutral admission");
        gesture.Sample(1,"copy/17",false,false,true,true);
        var equip=gesture.Sample(1,"copy/18",false,true,true,true);
        Check(equip!=null&&equip.Id=="copy/18"&&equip.Action==R23LoadoutAction.Equip&&equip.Hand==1,"fresh grip equips exact copy");
        Check(gesture.Sample(1,"copy/17",false,true,true,true)==null,"held grip never crosses into another copy");
        gesture.Sample(1,"copy/17",false,false,true,true);
        var drop=gesture.Sample(1,"copy/17",true,false,true,true);
        Check(drop!=null&&drop.Id=="copy/17"&&drop.Action==R23LoadoutAction.Drop,"fresh trigger drops exact copy");
        gesture.Sample(1,"copy/17",true,false,false,true);
        Check(gesture.Sample(1,"copy/17",true,false,true,true)==null,"tracking-loss neutral rearm");
        Console.WriteLine(count+" source names/rarity/live-setting/action-copy/neutral-input cases PASS (no engine).");
    }
}
