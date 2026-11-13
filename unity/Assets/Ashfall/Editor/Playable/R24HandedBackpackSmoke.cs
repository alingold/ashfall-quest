#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;
using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.UI;using Ashfall.Modules.Player;using UnityEngine;
namespace Ashfall.Editor.Playable
{
    public static partial class R24Feedback6237Smoke
    {
        static IEnumerator HandedBackpackClose(R23PlayableLoop loop,Action<string> note)
        {
            var original=loop.UI.Settings;try
            {
                loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range card");float end=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range loaded");loop.StartPlaying();
                foreach(int primary in new[]{1,0})
                {
                    loop.Gameplay.HolsterCurrent();Call(loop,"ApplySettings",original.With(dominantHand:primary));Need(loop.Gameplay.SetPreferredHandWhenHolstered(primary),"Actual selected dominant hand");for(int i=0;i<8;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
                    string inventory=string.Join("|",loop.Gameplay.OwnedWeaponActionIds);int capacity=loop.InventoryCapacity;
                    loop.UI.OpenLoadout();BackpackPrimaryFrame(loop,primary,false,false,true);Need(loop.UI.Page==R23UiPage.Loadout,"Actual backpack opened");
                    BackpackPrimaryFrame(loop,primary,false,true,true);Need(loop.UI.Page==R23UiPage.Loadout,"Support primary never substitutes for selected A/X");BackpackPrimaryFrame(loop,primary,false,false,true);
                    BackpackPrimaryFrame(loop,primary,true,true,false);Need(loop.UI.Page==R23UiPage.Loadout,"Untracked dominant hand cannot close through support fallback");BackpackPrimaryFrame(loop,primary,false,false,true);
                    BackpackPrimaryFrame(loop,primary,true,false,true);Need(loop.UI.Page!=R23UiPage.Loadout,"Actual dominant A/X closes backpack");Need(!loop.WeaponWheel.Active,"Close press never opens weapon wheel");
                    for(int i=0;i<12;i++)BackpackPrimaryFrame(loop,primary,true,false,true);Need(!loop.WeaponWheel.Active,"Held close press stays consumed");BackpackPrimaryFrame(loop,primary,false,false,true);Need(!loop.WeaponWheel.Active,"Release stays consumed without wheel action");
                    Need(string.Join("|",loop.Gameplay.OwnedWeaponActionIds)==inventory&&loop.InventoryCapacity==capacity,"Closure preserves actual carried identities and capacity");note("Dominant="+primary+" button="+(primary==0?"X":"A")+" supportRefused=true untrackedRefused=true actualRootClose=true held12FramesConsumed=true inventoryRetained=true");
                }
            }
            finally{loop.UI.SetPlaying();Call(loop,"ApplySettings",original);loop.ReturnToLobby();}yield return null;
        }
        static void BackpackPrimaryFrame(R23PlayableLoop loop,int primary,bool selected,bool support,bool selectedTracked)
        {
            var left=new HandSample{Tracked=true,Primary=primary==0?selected:support};var right=new HandSample{Tracked=true,Primary=primary==1?selected:support};if(primary==0)left.Tracked=selectedTracked;else right.Tracked=selectedTracked;loop.Rig.SmokeTrackedHands(left,right);loop.SmokeUiUpdate();
        }
    }
}
#endif
