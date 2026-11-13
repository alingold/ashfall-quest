#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.UI;
namespace Ashfall.Editor.Playable
{
    public sealed class R23SourceUiCapture
    {
        [Serializable]sealed class Result{public bool lobby,menu,settings,appearance,hud,gallery,mapFlow,deathHome;public bool headsetVerified=false;public string error="";public float verticalFieldOfView=75;}
        public IEnumerator Start()
        {
            var result=new Result();R23PlayableLoop loop=null;float end=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&loop.Ready)break;yield return null;}
            try
            {
                if(loop==null||!loop.Ready)throw new Exception(loop==null?"Loop absent":loop.Failure??"Ready timeout");
                loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Rig.Head.fieldOfView=75;loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.TrackingOrigin.localPosition=Vector3.zero;loop.ReturnToLobby();
                loop.Preview.Show("camo");result.lobby=loop.Lobby.Ready&&loop.Lobby.Visible;
            }catch(Exception e){result.error=e.ToString();}
            if(result.error.Length==0)
            {
                end=Time.realtimeSinceStartup+40;while(loop.Preview.Loading&&Time.realtimeSinceStartup<end)yield return null;yield return null;
                try{R23SmokeControl.Capture(loop.Rig.Head,"menu-source");result.menu=true;loop.UI.OpenSettings();}catch(Exception e){result.error=e.ToString();}
                yield return null;
                try{Debug.Log("R23 ICON IMPORT "+loop.UI.SourceIconBindingDiagnostics());R23SmokeControl.Capture(loop.Rig.Head,"settings-source");result.settings=true;loop.UI.OpenAppearance();}catch(Exception e){result.error=e.ToString();}
                yield return null;
                try{R23SmokeControl.Capture(loop.Rig.Head,"characters-source");result.appearance=true;loop.UI.OpenWeapons();}catch(Exception e){result.error=e.ToString();}
                end=Time.realtimeSinceStartup+60;while(loop.Gallery.Loading&&Time.realtimeSinceStartup<end)yield return null;
                // Let the production root apply presentation exclusivity for one frame.
                loop.ExternalProbePump=false;yield return null;loop.ExternalProbePump=true;
                try
                {
                    if(!loop.Gallery.Ready||!loop.Gallery.Active)throw new Exception(loop.Gallery.Failure??"UI did not open the source gallery");
                    int ammo=loop.Gameplay.Ammo,reserve=loop.Gameplay.Reserve,hits=loop.Gameplay.Hits;float health=loop.Gameplay.Health,shield=loop.Gameplay.Shield;
                    Transform model=null;foreach(var part in loop.Gallery.GetComponentsInChildren<Transform>(true))if(part.name=="R23 gallery "+loop.Gallery.ViewState.Label)model=part;
                    if(model==null)throw new Exception("Active source gallery model missing");
                    var hand=loop.Rig.Right;Vector3 saved=hand.position;Quaternion rotation=hand.rotation;hand.position=model.position;hand.rotation=model.rotation;
                    var input=new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true};
                    loop.Gallery.Sample(.02f,input,true,1);input.RightGrip=.8f;loop.Gallery.Sample(.02f,input,true,1);
                    if(!loop.Gallery.ViewState.Live||loop.Gallery.HoldingHand!=1)throw new Exception("Actual gallery grip did not attach preview");
                    if(Vector3.Dot(model.forward,-hand.up)<.9999f)throw new Exception("Held source gallery skipped reflected -PI/2 grip correction");
                    var definition=Ashfall.Modules.WeaponHandling.SourceRifleDefinition.ForId(loop.Gallery.ViewedWeaponId);
                    if(Vector3.Distance(model.TransformPoint(new Vector3(0,-.057f,-.025f)*definition.ModelScale),hand.position)>.0001f)throw new Exception("Grip correction moved the physical pickup contact");
                    input.RightTrigger=.8f;for(int i=0;i<12;i++)loop.Gallery.Sample(.02f,input,true,1);
                    if(loop.Gallery.ViewState.PreviewShots<1)throw new Exception("Actual source preview did not fire");
                    input.RightGrip=0;loop.Gallery.Sample(.02f,input,true,1);hand.position=saved;hand.rotation=rotation;
                    if(loop.Gallery.ViewState.Live||loop.Gallery.HoldingHand!=-1)throw new Exception("Gallery release did not return weapon to dock");
                    if(ammo!=loop.Gameplay.Ammo||reserve!=loop.Gameplay.Reserve||hits!=loop.Gameplay.Hits||health!=loop.Gameplay.Health||shield!=loop.Gameplay.Shield)throw new Exception("Preview mutated carried ammunition or world health");
                    R23SmokeControl.Capture(loop.Rig.Head,"gallery-source");result.gallery=true;
                    loop.UI.ToggleMenu();if(loop.Gallery.Active||loop.UI.Page!=R23UiPage.Ready)throw new Exception("Leaving loadout did not retire gallery");
                    loop.UI.OpenMaps();if(!loop.UI.ChooseMap(loop.Map.MapId)||!loop.Lobby.Visible)throw new Exception("Map choice left source lobby early");
                    loop.UI.OpenSpawnChoice();if(loop.UI.Page!=R23UiPage.SpawnChoice||!loop.Lobby.Visible)throw new Exception("Ready skipped separate spawn choice");
                    R23SmokeControl.Capture(loop.Rig.Head,"spawn-choice-source");
                    if(!loop.UI.ConfirmMapSpawn(true)||loop.UI.Page!=R23UiPage.Playing||loop.Lobby.Visible)throw new Exception("Game spawn did not enter selected source map");
                    result.mapFlow=true;loop.UI.UpdateHud(new R23HudSnapshot(loop.Gameplay.Health,loop.Gameplay.MaxHealth,loop.Gameplay.Ammo,loop.Gameplay.Reserve,loop.Gameplay.HasWeapon,loop.Gameplay.Reloading,loop.Gameplay.ActiveWeaponName,loop.Gameplay.Hits,loop.Gameplay.Shield,loop.Gameplay.MaxShield,loop.Rig.Stamina,loop.Rig.StaminaMaximum,loop.Activities.Balance));
                }catch(Exception e){result.error=e.ToString();}
                yield return null;
                try{R23SmokeControl.Capture(loop.Rig.Head,"hud-source");result.hud=true;}catch(Exception e){result.error=e.ToString();}
                if(result.error.Length==0)
                {
                    loop.Gameplay.ApplyPlayerDamage(1000);loop.ExternalProbePump=false;yield return null;
                    try{if(loop.UI.Page!=R23UiPage.Dead)throw new Exception("Death HUD did not open during actual round");loop.ReturnToLobby();}catch(Exception e){result.error=e.ToString();}
                    yield return null;loop.ExternalProbePump=true;
                    try{if(loop.UI.Page!=R23UiPage.Ready||!loop.Lobby.Visible)throw new Exception("Old round death stole lobby menu");loop.UI.OpenSpawnChoice();loop.UI.ConfirmMapSpawn(true);if(loop.Gameplay.Health<=0||loop.UI.Page!=R23UiPage.Playing)throw new Exception("New local training round retained dead health");result.deathHome=true;}catch(Exception e){result.error=e.ToString();}
                }
            }
            R23SmokeControl.Finish(result.error.Length==0,JsonUtility.ToJson(result,true));
        }
    }
}
#endif
