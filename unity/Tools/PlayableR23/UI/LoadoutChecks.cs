using System;
using System.Collections.Generic;
using Ashfall.Integration.Playable.UI;
class LoadoutChecks
{
    static int checks;
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;}
    static R23LoadoutIntent Frame(R23LoadoutGesture g,int hand,string id,bool trigger=false,bool grip=false,bool tracked=true,bool open=true)
    {return g.Sample(hand,id,trigger,grip,tracked,open);}
    static void Main()
    {
        var g=new R23LoadoutGesture();
        Check(Frame(g,0,"copy/one",grip:true)==null,"Held grip on entry does not equip");Frame(g,0,"copy/one");
        Check(Frame(g,0,"copy/one",grip:true)==null,"Fresh press captures without equipping");
        var equip=Frame(g,0,"copy/one");Check(equip!=null&&equip.Id=="copy/one"&&equip.Hand==0&&equip.Action==R23LoadoutAction.Equip,"Grip RELEASE equips captured copy");
        Check(Frame(g,0,"copy/one")==null,"Neutral cannot repeat equip");
        Frame(g,0,"copy/one",grip:true);Check(Frame(g,0,"copy/two")==null,"Release over another identity cancels");
        Frame(g,1,"shaker");Check(Frame(g,1,"shaker",trigger:true)==null,"Trigger press does not drop");var drop=Frame(g,1,"shaker");Check(drop!=null&&drop.Hand==1&&drop.Action==R23LoadoutAction.Drop,"Trigger RELEASE drops exact owned slot");
        Frame(g,0,"A",grip:true);Frame(g,0,"A",tracked:false);Frame(g,0,"A",grip:true);Check(Frame(g,0,"A")==null,"Lost tracking requires neutral before fresh gesture");
        Frame(g,0,"A",grip:true);Check(Frame(g,0,"A")!=null,"Fresh release recovers");
        g.Reset();Check(Frame(g,0,"A",trigger:true)==null,"Identity/page reset cannot repeat held drop");Frame(g,0,"A");Frame(g,0,"A",trigger:true);Check(Frame(g,0,"A",open:false)==null,"Closed backpack never drops");
        g.Reset();Frame(g,0,"A");Frame(g,0,"A",trigger:true,grip:true);var chord=Frame(g,0,"A");Check(chord!=null&&chord.Action==R23LoadoutAction.Equip,"Simultaneous grip wins trigger on release");
        g.Reset();Frame(g,0,null);Frame(g,0,null,grip:true);Check(Frame(g,0,"A")==null,"Held entry from blank never adopts an item");
        g.Reset();Frame(g,0,"L");Frame(g,1,"R");Frame(g,0,"L",grip:true);Frame(g,1,"R",grip:true);var left=Frame(g,0,"L");var right=Frame(g,1,"R");Check(left!=null&&right!=null&&left.Hand==0&&right.Hand==1,"Independent hand identities");
        float delta;g.Reset();g.Sample(0,"A",false,false,true,true,0,0,true,true,out delta);g.Sample(0,"A",false,true,true,true,0,0,true,true,out delta);g.Sample(0,"A",false,true,true,true,0,.05f,true,true,out delta);Check(delta>.049f&&g.Sample(0,"A",false,false,true,true,0,.05f,true,true,out delta)==null,"Drag scroll suppresses equip on release");
        var list=new List<R23LoadoutOption>{new R23LoadoutOption("copy/a","1911",profileId:"1911",stars:5),new R23LoadoutOption("copy/b","1911",profileId:"1911",stars:1)};
        var owned=new R23OwnedLoadout(list);list.Clear();var exported=owned.Options;exported[0]=null;
        Check(owned.Capacity==3&&owned.Count==2&&owned.Slot(2)==null&&owned.Slot(0).Id=="copy/a"&&owned.Slot(1).Id=="copy/b","Three-slot immutable snapshot retains same-profile copies");
        Check(owned.Slot(0).ProfileId=="1911"&&owned.Slot(0).Stars==5&&owned.Slot(1).Stars==1,"Profile/rarity separate from action ID");
        bool rejected=false;try{new R23OwnedLoadout(new[]{new R23LoadoutOption("same","A"),new R23LoadoutOption("same","B")});}catch(ArgumentException){rejected=true;}Check(rejected,"Duplicate action ID rejected");
        rejected=false;try{new R23OwnedLoadout(new[]{new R23LoadoutOption("1","A"),new R23LoadoutOption("2","B"),new R23LoadoutOption("3","C"),new R23LoadoutOption("4","D")});}catch(ArgumentException){rejected=true;}Check(rejected,"No fourth default slot grant");
        Check(new R23OwnedLoadout(null,6).Capacity==6,"Source capacity upgrade host-owned");foreach(var field in typeof(R23LoadoutOption).GetFields())Check(field.IsInitOnly,"Immutable option");
        var resource=new R23BackpackResource("rifle",2400);Check(!resource.CanDrop&&resource.Count==2400,"Resource drops disabled until actual provider supplied");
        Console.WriteLine("PASS "+checks+" backpack release/drag/neutral/identity/resource cases");
    }
}
