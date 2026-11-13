#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.World;
using Ashfall.Integration.Playable.World.Loot;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
using UnityEditor;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // One actual Alpha cache lifecycle. No source-site injection, item grant,
    // inventory reset, synthetic collider, effect mutation or weapon-suite replay.
    public sealed class R24MixedCacheSmoke
    {
        [Serializable]sealed class Receipt
        {
            public bool success,nativeExecuted,dispatch,foodFloor,visibility,retirement,cleanup,headsetVerified=false;
            public string error="",map="",policy="unity-mixed-v1";public int activated,guns,melee,grenades,foods,settledFoods;
            public List<string> notes=new List<string>(),shaderErrors=new List<string>();
        }
        readonly Receipt result=new Receipt();R23PlayableLoop loop;float deadline;string originalMap;bool manual,external,rig,pump,persist;
        void Need(bool condition,string why){if(!condition)throw new InvalidOperationException(why);}
        void Note(string text){result.notes.Add(text);Debug.Log("MIXED CACHE "+text);}
        void Pump(){Need(Time.realtimeSinceStartup<deadline-15,"Mixed cache300s bound with cleanup margin");loop.SmokeUiUpdate();Physics.SyncTransforms();}
        public IEnumerator Run()
        {
            deadline=Time.realtimeSinceStartup+300;
            while(Time.realtimeSinceStartup<deadline-200){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual ready XR-off Root required";Finish();yield break;}
            result.nativeExecuted=true;originalMap=loop.Map.MapId;manual=loop.ManualSmoke;external=loop.ExternalProbePump;rig=loop.Rig.enabled;pump=loop.Gameplay.ExternalInputPump;persist=loop.Calibration.SuppressPersistence;
            loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
            try
            {
                var work=Drive(Exercise(),"cache");while(work.MoveNext())yield return work.Current;
                work=Drive(Cleanup(),"cleanup");while(work.MoveNext())yield return work.Current;
            }
            finally{loop.ManualSmoke=manual;loop.ExternalProbePump=external;loop.Rig.enabled=rig;loop.Gameplay.ExternalInputPump=pump;loop.Calibration.SuppressPersistence=persist;Finish();}
        }
        IEnumerator Drive(IEnumerator work,string phase)
        {
            try{while(true){bool more=false;object next=null;try{Pump();more=work.MoveNext();if(more)next=work.Current;}catch(Exception e){result.error+=phase+": "+e+"\n";Note("FAIL "+e);break;}if(!more)break;yield return next;}}
            finally{var disposable=work as IDisposable;if(disposable!=null)disposable.Dispose();}
        }
        IEnumerator Exercise()
        {
            loop.ReturnToLobby();loop.SelectMap(R23MapCatalog.AlphaId,true);
            while(!loop.Ready&&loop.Failure==null){Pump();yield return null;}
            Need(loop.Ready&&loop.Map.MapId==R23MapCatalog.AlphaId,"Actual Alpha load failed: "+loop.Failure);loop.StartPlaying();
            while(loop.WorldLoot.PreparationPending||loop.WorldLoot.PreparedMapId!=loop.Map.MapId){Pump();Need(loop.WorldLoot.Failure==null,"Actual cache preparation: "+loop.WorldLoot.Failure);yield return null;}
            result.map=loop.Map.MapId;var world=loop.WorldLoot;result.activated=world.ActivatedSites;
            Need(result.activated>0&&world.UnsupportedDraws==0,"Actual activated mixed sites must have no unsupported draws");
            foreach(string id in world.CacheWeaponActionIds)
            {
                string profile=loop.Gameplay.ResolveWeaponProfile(id);int catalog=R23WeaponPickupRules.CatalogIndex(profile);
                Need(loop.Gameplay.TryReadWeaponInstance(id,out var item)&&!loop.Gameplay.OwnsWeapon(id),"Actual unowned independent weapon instance "+id);
                if(catalog==14||catalog==15||catalog==16||catalog==29)result.melee++;else result.guns++;
            }
            foreach(string id in world.CacheGrenadeInstanceIds){Need(loop.Explosives.TryReadItem(id,out var item)&&!item.Owned&&!item.Spent,"Actual finite grenade owner "+id);result.grenades++;}
            string[] foods=world.CacheFoodInstanceIds;result.foods=foods.Length;
            foreach(string id in foods){var item=loop.Consumables.InspectItem(id);Need(item!=null&&item.Owner==null&&!item.Removed&&Array.IndexOf(new[]{"soda","shaker","banana"},item.Kind)>=0,"Actual consumable owner "+id);}
            Need(result.guns>0&&result.melee>0&&result.grenades>0&&result.foods>0,"Actual initial Alpha sites exercise all four categories");
            Need(result.guns+result.melee+result.grenades+result.foods==world.SpawnedCount&&world.SpawnedCount==result.activated*6,"Six actual successful owner dispatches per activated site");result.dispatch=true;
            // Wait the production deferred settler, without moving props by hand.
            float settleUntil=Mathf.Min(deadline-60,Time.realtimeSinceStartup+3);while(Time.realtimeSinceStartup<settleUntil){Pump();yield return null;}
            foreach(string id in foods)
            {
                Transform prop=null;foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name==id){prop=t;break;}
                Need(prop!=null,"Actual food visual missing "+id);float lowest;
                if(!R24WorldPickupSettler.TryReadLowestVisibleMesh(prop,out lowest))continue;
                RaycastHit floor;Need(Physics.Raycast(prop.position+Vector3.up*2,Vector3.down,out floor,6,1,QueryTriggerInteraction.Ignore),"Actual cache food floor absent "+id);
                Need(Mathf.Abs(lowest-floor.point.y-R24WorldPickupLayout.HoverHeight)<.008f,"Actual settled food mesh/floor gap "+id+" gap="+(lowest-floor.point.y));result.settledFoods++;
            }
            Need(result.settledFoods>0,"At least one actual visible cache food floor exercised");result.foodFloor=true;
            CheckShaders();
            world.SetWorldEnabled(false);foreach(string id in foods){foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name==id)Need(!t.gameObject.activeInHierarchy,"Disabled cache food stays visible "+id);}result.visibility=true;
            world.WithdrawMap();foreach(string id in foods)Need(loop.Consumables.InspectItem(id)==null,"Unowned cache food survived map withdrawal "+id);result.retirement=true;
            Need(world.CacheFoodInstanceIds.Length==0,"World cache food registry cleared");
            Need(result.shaderErrors.Count==0,"Rendered shader compiler errors");Note("Actual cache dispatch/food floor/hide/retirement exercised; owned retention/effect continuation currently CPU-proven only.");
        }
        void CheckShaders(){var checkedShaders=new HashSet<Shader>();foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>())if(r.enabled&&r.gameObject.activeInHierarchy)foreach(var m in r.sharedMaterials)if(m!=null&&checkedShaders.Add(m.shader))foreach(var message in ShaderUtil.GetShaderMessages(m.shader))if(message.severity.ToString()=="Error")result.shaderErrors.Add(m.shader.name+": "+message.message);}
        IEnumerator Cleanup()
        {
            loop.ReturnToLobby();if(loop.Map.MapId!=originalMap){loop.SelectMap(originalMap,false);while(!loop.Ready&&loop.Failure==null){Pump();yield return null;}}
            Need(loop.Ready&&loop.Map.MapId==originalMap,"Original map restore failed");loop.ReturnToLobby();result.cleanup=loop.Lobby.Visible&&!loop.MatchActive;
        }
        void Finish(){result.success=result.nativeExecuted&&result.dispatch&&result.foodFloor&&result.visibility&&result.retirement&&result.cleanup&&result.error.Length==0&&result.shaderErrors.Count==0;R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));}
    }
}
#endif
