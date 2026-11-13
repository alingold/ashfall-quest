from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall');p=R/'unity/Assets/Ashfall/Integration/ConsumableTransactions/ConsumableBridge.cs';s=p.read_text()
s=s.replace('if(active||reserve==null||!inventories.ContainsKey(actor)', 'if(active||reserve==null||String.IsNullOrEmpty(actor)||!inventories.ContainsKey(actor)')
s=s.replace('IDropBatch batch=null;\n            try {\n                var removed=', 'IDropBatch batch=null;EvictedInstance[] placements=new EvictedInstance[0];\n            try {\n                var removed=')
s=s.replace('var placements=batch.Placements;if(placements==null||placements.Length!=removed.Length)', 'placements=batch.Placements;if(placements==null||placements.Length!=removed.Length)')
s=s.replace('admission,members.ToArray(),batch)', 'admission,members.ToArray(),batch,placements)')
s=s.replace('string[] members,IDropBatch drops){admission=prepared;batch=drops;Receipt=new StoreReceipt(t,drops==null?new EvictedInstance[0]:drops.Placements,', 'string[] members,IDropBatch drops,EvictedInstance[] placements){admission=prepared;batch=drops;Receipt=new StoreReceipt(t,placements,')
p.write_text(s)
p=R/'unity/Tools/Integration/ConsumableTransactions/TestsR3.cs';s=p.read_text();marker='  // Collect\'s actual expiry and competing source ownership.'
s=s.replace(marker,'''  // Full shaker50, banana health100/stamina6 and soda50 over ten active seconds.
  Init();Collect("shake","shaker");Action(PickupAction.Equip,"shake");for(int i=0;i<3;i++){Action(PickupAction.DesktopRelease,"shake");Action(PickupAction.DesktopPress,"shake");}Advance(180);Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,0,80,false).Accepted,"shaker full setup");Action(PickupAction.Consume,"shake");Check(health.Read("a").Shield==70&&inventory.Items.Length==0,"full shaker exact50");
  Init();Collect("soda","soda");Action(PickupAction.Equip,"soda");Action(PickupAction.DesktopRelease,"soda");Action(PickupAction.DesktopPress,"soda");Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,0,100,false).Accepted,"soda setup");Action(PickupAction.Consume,"soda");Check(pickups.PendingSoda("a")==50,"soda schedules50");Advance(60);Check(health.Read("a").Shield==5&&pickups.PendingSoda("a")==45,"soda exact5 per second");Advance(600);Check(health.Read("a").Shield==50&&pickups.PendingSoda("a")==0,"soda exact total50 ten seconds");Advance(660);Check(health.Read("a").Shield==50,"soda no excess duration");
  Init();Collect("ban","banana");PrepareBanana("ban");Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,90,0,false).Accepted,"banana setup");Action(PickupAction.Consume,"ban");Check(health.Read("a").Health==100&&move.SprintStamina==6&&inventory.Items.Length==0,"banana exact capped health/stamina and removal");
''' +marker)
p.write_text(s)
p=R/'unity/Tools/Integration/ConsumableTransactions/Run-R3.py';s=p.read_text();s=s.replace("'/warnaserror+','/target:'", "'/warnaserror+',r'/reference:C:\\Program Files\\Unity\\Hub\\Editor\\6000.6.8f1\\Editor\\Data\\Managed\\UnityEngine\\UnityEngine.CoreModule.dll','/target:'")
s=s.replace('print(name,result)', "print(name,'compile',result['compile'],'exit',result.get('exit'),'tail',result['output'][-240:])")
p.write_text(s)
