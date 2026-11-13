using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.World.Loot;using Ashfall.Integration.Playable.HandsWeapons;
class GroundCases {
 static int n;static void N(bool value,string why){n++;if(!value)throw new Exception(why);}sealed class Query:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range){return false;}}
 static void Main(){
  var owned=new List<int>{0,2,9};var grid=R23BaselineLootRecipe.Slots(owned,false,false);N(grid.Length==28&&owned.Count==3,"one full source kit, carried originals unchanged");var active=R23LootRecipe.ReviewCatalogIds;var unique=new HashSet<int>();
  for(int i=0;i<28;i++){N(grid[i].Catalog==active[i]&&grid[i].GridIndex==i&&grid[i].LayoutIndex==i&&unique.Add(grid[i].Catalog),"source active28 grid slot "+i);var p=R23BaselineLootRecipe.BuiltinOffset(i);N(Math.Abs(p.X-(-4.3f+i%8*1.4f))<.0001f&&p.Y==.3f&&Math.Abs(p.Z-(-.6f+i/8*1.4f))<.0001f,"exact once-reflected source grid offset "+i);}
  N(grid[0].SourceInstanceIndex==30&&grid[2].SourceInstanceIndex==31&&grid[8].SourceInstanceIndex==32,"owned original0/2/9 receive independent source30/31/32 copies");
  var scatter=R23BaselineLootRecipe.Slots(owned,true,false);int[] order={1,3,5,6,7,8,10,11,12,13,14,15,16,17,18,19,20,21,23,24,25,26,27,28,29,0,2,9};
  for(int i=0;i<28;i++){N(scatter[i].Catalog==order[i]&&scatter[i].LayoutIndex==i&&(i==0||scatter[i-1].SourceInstanceIndex<scatter[i].SourceInstanceIndex),"archive sorts source instance IDs, not profile strings: "+i);N(R23BaselineLootRecipe.ScatterIndex(i,37)==i*17%37,"source17 stride actual loot_points order "+i);}
  var magnum=R23BaselineLootRecipe.Slots(owned,true,true);N(magnum.Length==1&&magnum[0].Catalog==7&&magnum[0].LayoutIndex==0&&R23BaselineLootRecipe.ScatterIndex(0,1)==0&&owned.Count==3,"Magnum ground filter does not silently rewrite carried roster");
  var changed=R23BaselineLootRecipe.Slots(new[]{1,7},true,false);N(changed[26].Catalog==1&&changed[26].SourceInstanceIndex==30&&changed[27].Catalog==7&&changed[27].SourceInstanceIndex==31,"actual original custody determines source clone sort");
  var r=new R23RoundState(new[]{"target"},new Query(),true);N(r.SelectWeapon("cx4")&&r.ActiveLoaded==25,"real initial CX4 native owner");r.Step(0,false,false,false);N(r.Step(0,true,false,false)&&r.ActiveLoaded==24,"real carried gun discharges one native round");int reserve=r.ReadAmmoReserveFamily("rifle");string copy;N(r.CreateWeaponInstance("cx4",1,out copy)&&r.ReadWeapon(copy).Loaded==25&&r.ReadWeapon("cx4").Loaded==24,"ground clone is independent native lifetime, carried ammo untouched");r.SelectWeapon(copy);r.SuspendActiveWeapon();r.SelectWeapon("cx4");N(r.ActiveLoaded==24&&r.ReadAmmoReserveFamily("rifle")==reserve,"custody/selection cannot refill carried owner or reserve");
  Console.WriteLine(n+" source baseline grid/instance-sort/archive17/Magnum filter +actual carried/copy native ammo cases PASS");
 }
}
