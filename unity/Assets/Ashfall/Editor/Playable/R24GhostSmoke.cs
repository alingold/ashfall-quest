#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.TeamLaunch;
using Ashfall.Integration.Playable.UI;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Editor.Playable
{
    // Explicit XR-off fixture. Uses genuine Range floor/walls and registered
    // practice actors; never creates geometry, peers, Health or a second motor.
    public sealed class R24GhostSmoke
    {
        [Serializable]sealed class Group{public string name="",error="";public bool passed;public List<string> notes=new List<string>();}
        [Serializable]sealed class Receipt{public bool nativeExecuted,success,headsetVerified=false;public string error="",startedUtc="",finishedUtc="";public List<Group> groups=new List<Group>();}
        R23PlayableLoop loop;Receipt receipt;Saved saved;float deadline;
        public bool AutoFinish=true;public bool Passed{get;private set;}public string Json{get;private set;}
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        void Bound(){Need(Time.realtimeSinceStartup<deadline,"Ghost native fixture exceeded240s active bound");}
        void Note(Group g,string text){g.notes.Add(text);Debug.Log("R24 GHOST "+g.name+": "+text);}
        public IEnumerator Run()
        {
            receipt=new Receipt{startedUtc=DateTime.UtcNow.ToString("o")};float end=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){receipt.error="Actual ready XR-disabled Root required: "+(loop==null?"no Root":loop.Failure);Finish();yield break;}
            receipt.nativeExecuted=true;saved=new Saved(loop);deadline=Time.realtimeSinceStartup+240;
            try
            {
                loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
                loop.Rig.TrackingOrigin.localPosition=Vector3.zero;loop.Rig.Head.transform.SetLocalPositionAndRotation(new Vector3(0,1.7088f,0),Quaternion.identity);
                loop.Rig.Left.SetLocalPositionAndRotation(new Vector3(-.24f,1.16f,.24f),Quaternion.identity);loop.Rig.Right.SetLocalPositionAndRotation(new Vector3(.24f,1.16f,.24f),Quaternion.identity);
                foreach(string name in new[]{"ActualUiDownedNeutralAndEliminated","ActualRangeFloorWallAndActor4","ActualFallenGhostRecoveryPreservesState","ActualItemFireAndConstructionSuppression","ActualRespawnRestoresArtAndCollision","ActualEndRestoresNewGloveMaterial"})
                {
                    var g=new Group{name=name};receipt.groups.Add(g);
                    IEnumerator work=name=="ActualUiDownedNeutralAndEliminated"?Life(g):name=="ActualRangeFloorWallAndActor4"?Traversal(g):name=="ActualFallenGhostRecoveryPreservesState"?Recovery(g):name=="ActualItemFireAndConstructionSuppression"?Items(g):name=="ActualRespawnRestoresArtAndCollision"?Respawn(g):End(g);
                    var drive=Drive(g,work);while(drive.MoveNext())yield return drive.Current;
                }
                var cleanup=new Group{name="ActualSceneCleanup"};receipt.groups.Add(cleanup);var restore=Drive(cleanup,Cleanup(cleanup));while(restore.MoveNext())yield return restore.Current;
            }
            finally
            {
                try{if(loop!=null&&loop.Ready)loop.ReturnToLobby();}catch(Exception error){receipt.error+=" Lobby cleanup: "+error;}
                saved.Restore(loop);Finish();
            }
        }
        IEnumerator Drive(Group g,IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            try
            {
                while(stack.Count>0)
                {
                    bool moved=false;object value=null;try{Bound();moved=stack.Peek().MoveNext();if(moved)value=stack.Peek().Current;}catch(Exception error){g.error=error.ToString();Debug.LogError("R24 GHOST FAIL "+g.name+": "+error);break;}
                    if(!moved){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}
                    var child=value as IEnumerator;if(child!=null)stack.Push(child);else yield return value;
                }
                g.passed=g.error.Length==0;
            }
            finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}}
        }
        void Finish(){receipt.success=receipt.nativeExecuted&&receipt.error.Length==0&&receipt.groups.Count==7;foreach(var g in receipt.groups)receipt.success&=g.passed;receipt.finishedUtc=DateTime.UtcNow.ToString("o");Passed=receipt.success;Json=JsonUtility.ToJson(receipt,true);Debug.Log("R24 GHOST TERMINAL "+Json);if(AutoFinish)R23SmokeControl.Finish(Passed,Json);}
        IEnumerator Begin(Group g,bool respawn=false)
        {
            loop.ReturnToLobby();Need(loop.UI.SetSelectedMap("range"),"Actual installed Range unavailable");loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset(respawn?"free_for_all":"team_revive");
            foreach(var rule in new[]{new KeyValuePair<string,object>("zone_enabled",false),new KeyValuePair<string,object>("protection",0f),new KeyValuePair<string,object>("respawn_delay",1f),new KeyValuePair<string,object>("time_limit",0)})Need(loop.UI.SetMatchRule(rule.Key,rule.Value),"Actual rule unavailable: "+rule.Key);
            Need(loop.UI.RequestMatchStart(),"Real UI START MATCH refused");float end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);
            while(!loop.MatchActive&&loop.Failure==null&&Time.realtimeSinceStartup<end){Bound();yield return null;}
            Need(loop.Ready&&loop.MatchActive&&loop.Map.MapId=="range"&&loop.Gameplay.LocalMatchHealthConfigured,"UI did not start genuine Range match: "+loop.MatchStatus+" / "+loop.Failure);
            while((!loop.Appearance.Ready||!loop.Gameplay.Ready)&&Time.realtimeSinceStartup<end)yield return null;
            Need(loop.Appearance.Ready&&loop.Gameplay.Ready,"Actual source body/gloves not ready");Pump();yield return null;
            Need(loop.Gameplay.LocalMatchHealth.Life==LifeState.Alive&&!loop.MatchLocalGhost&&loop.MatchLocalMotionAllowed&&loop.MatchLocalItemsAllowed,"Actual fresh match did not start alive");
            Note(g,"Real UI started "+loop.ActualMatchRules.Mode+" Range; Health protection="+loop.Gameplay.LocalMatchHealth.ProtectionUntil+" clock="+loop.MatchClock+". No synthetic actor/floor.");
        }
        void Down(Group g,bool respawn=false)
        {
            Need(loop.Gameplay.ApplyDirectPlayerDamage(1000),"Canonical lethal Health callback refused");loop.TickMatch(.001f,true);
            Need(loop.Gameplay.LocalMatchHealth.Life==(respawn?LifeState.Dead:LifeState.Downed),"Actual source life differs");
            Need(loop.MatchLocalGhost&&loop.Rig.SourceGhostMotion&&loop.MatchLocalMotionAllowed&&!loop.MatchLocalItemsAllowed,"Source ghost motion/item split missing");
            Need(!loop.Rig.LeftInputReady&&!loop.Rig.RightInputReady,"Life transition failed to require fresh neutral input");Pump();
            Need(loop.UI.Page==R23UiPage.Playing&&loop.UI.IsLocalMatchGhost&&loop.UI.GhostHudVisible&&!loop.UI.BlocksGameplay&&!loop.Rig.GameplayBlocked,"Ghost notice became a death modal or blocked actual motion");
            Need(!loop.Gameplay.HasWeapon&&!loop.Consumables.LeftOccupied&&!loop.Consumables.RightOccupied&&!loop.Explosives.LeftOccupied&&!loop.Explosives.RightOccupied,"Actual held items were not stowed on ghost transition");
            Note(g,"Actual "+loop.Gameplay.LocalMatchHealth.Life+"; notice='"+loop.UI.LocalMatchLifeCaption+"'; motion allowed/items blocked, neutral required.");
        }
        IEnumerator Life(Group g)
        {
            yield return Begin(g);Down(g);
            loop.Rig.SmokeXrHands(.8f,.9f,.02f);Need(!loop.Rig.LeftInputReady&&!loop.Rig.RightInputReady,"Held grip/trigger rearmed ghost transition");
            Neutral();Need(loop.Rig.LeftInputReady&&loop.Rig.RightInputReady,"Fresh neutral did not rearm normal ghost traversal");
            loop.UI.ToggleMenu();Pump();Need(loop.UI.BlocksGameplay&&loop.Rig.GameplayBlocked,"Real pause menu did not block ghost motion");loop.UI.ToggleMenu();Pump();Need(!loop.Rig.GameplayBlocked&&loop.UI.GhostHudVisible,"Closing real menu failed to restore nonmodal ghost motion");
            double until=loop.Gameplay.LocalMatchHealth.DownedUntil;while(loop.MatchClock<until+.01){Bound();loop.TickMatch((float)Math.Min(.1,until+.01-loop.MatchClock),true);}Pump();
            Need(loop.Gameplay.LocalMatchHealth.Life==LifeState.Dead&&loop.MatchLocalMotionAllowed&&!loop.MatchLocalItemsAllowed&&loop.UI.LocalMatchLifeState=="eliminated"&&loop.UI.GhostHudVisible,"Actual bleedout incorrectly disabled eliminated movement");
            Need(!loop.Gameplay.TryRespawnLocalMatch(),"Fixture manufactured revive-mode respawn");Note(g,"PASS actual downed→eliminated, no fabricated allied revive/peer; menu gating and fresh neutral restored.");
        }
        IEnumerator Traversal(Group g)
        {
            yield return Begin(g);Vector3 actorFoot,actorDirection,actorHead;Collider actor;
            Need(FindActorLane(out actor,out actorFoot,out actorDirection,out actorHead),"No actual registered Range actor with native supported clear lane");
            bool ceiling;Vector3 request=actorDirection*1.6f,alive=loop.Rig.HeadCollision.Sweep(actorHead-actorDirection*.8f,request,false,out ceiling);
            Need(alive.magnitude<1.2f,"Alive source head hull did not collide with genuine actor-role4 control");
            var mask=new CollisionSnapshot(loop);Down(g);mask.AssertGhost(loop);Neutral();
            Vector3 center=RangeOrigin();Vector3 floor=Floor(center+Vector3.up*2);
            DropFromSupport(floor,3,Vector3.forward);Note(g,"Gravity start3m above actual native Range support="+floor);Vector3 rawHead=loop.Rig.Head.transform.localPosition,rawLeft=loop.Rig.Left.localPosition,rawRight=loop.Rig.Right.localPosition;
            float startY=loop.Rig.transform.position.y;for(int i=0;i<150;i++)Move(Vector2.zero);
            Need(startY-loop.Rig.transform.position.y>2.8f&&Mathf.Abs(loop.Rig.transform.position.y-floor.y)<.08f&&loop.Rig.Supported,"Ghost disabled real gravity/floor collision");
            Vector3 walkStart=loop.Rig.transform.position;for(int i=0;i<12;i++)Move(Vector2.up);Need(Vector3.Distance(walkStart,loop.Rig.transform.position)>.2f,"Actual source ghost walk failed");
            // Exact authored source Range right wall: x45,16m end wall remains
            // physical. Both test positions are confirmed against actual native map.
            Vector3 wallFoot=Floor(center+new Vector3(41,2,0));Place(wallFoot+Vector3.up*.02f,Vector3.right);Neutral();
            RaycastHit wall;Need(MapRay(loop.Rig.Head.transform.position,Vector3.right,6,out wall)&&wall.normal.x<-.8f,"Actual source Range side wall not present");
            for(int i=0;i<WalkSteps(6);i++)Move(Vector2.up);
            Need(loop.Rig.Head.transform.position.x<wall.point.x-.05f&&loop.Rig.Head.transform.position.x>wall.point.x-.5f,"Ghost passed world wall or walk never reached it");
            Need(!loop.Gameplay.IsSourceActorLayer4(wall.collider),"World collider incorrectly classified as actor4");
            Place(actorFoot-actorDirection*.8f+Vector3.up*.02f,actorDirection);Neutral();float before=Vector3.Dot(loop.Rig.transform.position-actorFoot,actorDirection);
            for(int i=0;i<WalkSteps(2);i++)Move(Vector2.up);
            float after=Vector3.Dot(loop.Rig.transform.position-actorFoot,actorDirection);Need(before<-.5f&&after>.5f,"Actual ghost did not pass through registered source actor4 with ordinary Rig movement");
            Need(loop.Gameplay.IsSourceActorLayer4(actor),"Fixture used unregistered proxy actor");
            Need(loop.Rig.Head.transform.localPosition==rawHead&&loop.Rig.Left.localPosition==rawLeft&&loop.Rig.Right.localPosition==rawRight,"Ghost traversal overwrote raw tracked local poses");
            Note(g,"PASS source gravity/floor/normal walk, actual wall='"+wall.collider.name+"', actual actor='"+actor.name+"' aliveSweep="+alive.magnitude+" ghost progress="+before+"→"+after+". No noclip/freeflight/fake geometry.");
        }
        IEnumerator Recovery(Group g)
        {
            yield return Begin(g);Down(g);Neutral();var health=loop.Gameplay.LocalMatchHealth;string owned=string.Join(",",loop.Gameplay.OwnedWeaponActionIds);string weapons=WeaponState();
            var head=loop.Rig.Head.transform.localPosition;var left=loop.Rig.Left.localPosition;var right=loop.Rig.Right.localPosition;var origin=loop.Rig.TrackingOrigin.localPosition;var rotation=loop.Rig.transform.rotation;
            int count=loop.GhostRecoveryCount;Need(loop.GhostRecoverySupported&&loop.Map.MapId=="range","Real Range source recovery unavailable");
            Vector3 fallen=RangeOrigin()+new Vector3(0,-26,0);Need(loop.Rig.RelocateSourceGhost(fallen),"Pose-only fallen ghost staging refused");Physics.SyncTransforms();
            for(int i=0;i<3&&loop.GhostRecoveryCount==count;i++)loop.TickMatch(.1f,true);
            Need(loop.GhostRecoveryCount==count+1&&!loop.GhostRecoveryUsedSourceFallback,"Real source candidate floor/head-clear recovery did not accept once");
            Need(FloorExists(loop.LastGhostRecoveryTo,1)&&loop.Rig.transform.position==loop.LastGhostRecoveryTo&&loop.LastGhostRecoveryFrom==fallen,"Recovery did not use actual native supported Range candidate");
            var after=loop.Gameplay.LocalMatchHealth;
            Need(after.Life==health.Life&&after.Revision==health.Revision&&after.LifeGeneration==health.LifeGeneration&&after.Health==health.Health&&after.Shield==health.Shield&&after.Deaths==health.Deaths&&after.DownedUntil==health.DownedUntil&&after.RespawnAt==health.RespawnAt&&after.ProtectionUntil==health.ProtectionUntil,"Ghost recovery changed canonical Health/counters/timers");
            Need(owned==string.Join(",",loop.Gameplay.OwnedWeaponActionIds)&&weapons==WeaponState(),"Ghost recovery reset actual owned weapons/ammo");
            Need(loop.Rig.Head.transform.localPosition==head&&loop.Rig.Left.localPosition==left&&loop.Rig.Right.localPosition==right&&loop.Rig.TrackingOrigin.localPosition==origin&&loop.Rig.transform.rotation==rotation,"Ghost recovery rewrote tracking/heading");
            Need(!loop.Rig.LeftInputReady&&!loop.Rig.RightInputReady,"Authoritative source relocation did not require neutral");Pump();
            Note(g,"PASS actual source<-25 Range recovery "+fallen+"→"+loop.LastGhostRecoveryTo+" preserving sameHealth revision/life/vitals/deathcount/timers/inventory/ammo/rawtracking/heading. Archives explicitly remain unbound.");
        }
        IEnumerator Items(Group g)
        {
            yield return Begin(g);string sourceWeapon=loop.Gameplay.ActiveWeaponActionId;Down(g);Place(Floor(RangeOrigin()+Vector3.up*2)+Vector3.up*.02f,Vector3.forward);Neutral();
            string before=WeaponState();int panels=loop.Activities.PanelCount,balance=loop.Activities.Balance;
            Need(!loop.Gameplay.EquipOwned(sourceWeapon,1)&&!loop.Gameplay.TryPickup(0)&&!loop.Gameplay.TryPickup(1),"Ghost admitted actual carried equip/pickup");
            for(int i=0;i<8;i++){loop.Rig.SmokeXrHands(0,.9f,.02f);Pump(.9f);Need(!loop.Gameplay.TickInput(.02f,new R23HandsInputFrame{LeftTracked=true,RightTracked=true,RightTrigger=.9f}),"Ghost accepted actual firearm shot");}
            for(int i=0;i<3;i++){loop.Rig.SmokeXrHands(0,0,.02f);Pump();}
            Need(WeaponState()==before&&loop.Activities.PanelCount==panels&&loop.Activities.Balance==balance,"Ghost firing/build gesture changed real ammo/placed panels/charge ledger");
            Need(loop.MatchLocalMotionAllowed&&!loop.MatchLocalItemsAllowed&&!loop.Rig.GameplayBlocked,"Suppressing items accidentally disabled movement");
            Note(g,"PASS real owner fire/equip/pickup refused; actual Rig trigger-preview/release produced no panel or charge mutation; existing movement remains allowed.");
        }
        IEnumerator Respawn(Group g)
        {
            yield return Begin(g,true);var masks=new CollisionSnapshot(loop);var art=new ArtSnapshot(loop);Down(g,true);masks.AssertGhost(loop);art.AssertGhost(loop);Neutral();
            double at=loop.Gameplay.LocalMatchHealth.RespawnAt;while(loop.MatchClock<at+.01){Bound();loop.TickMatch((float)Math.Min(.1,at+.01-loop.MatchClock),true);}Pump();
            Need(loop.Gameplay.LocalMatchHealth.Life==LifeState.Alive&&!loop.MatchLocalGhost&&!loop.Rig.SourceGhostMotion&&!loop.UI.IsLocalMatchGhost,"Canonical timed respawn did not restore actual life presentation");masks.AssertRestored(loop);art.AssertRestored(null);
            Need(loop.MatchLocalMotionAllowed&&loop.MatchLocalItemsAllowed,"Respawn failed to restore item/movement permissions");Note(g,"PASS genuine canonical timed respawn restores exact renderer material/shadow/mesh references and original CC layer/include/exclude/head-hull policy.");
        }
        IEnumerator End(Group g)
        {
            yield return Begin(g);var masks=new CollisionSnapshot(loop);var art=new ArtSnapshot(loop);Material actualGlove=loop.Appearance.CurrentGloveMaterial,replacement=null;
            try
            {
                Down(g);art.AssertGhost(loop);Neutral();Place(Floor(RangeOrigin()+Vector3.up*2)+Vector3.up*.02f,Vector3.forward);
                replacement=new Material(actualGlove);replacement.name="Native same-source glove material replacement control";loop.Gameplay.SetGloveMaterial(replacement);loop.TickMatch(.01f,true);Pump();art.AssertGhost(loop);
                loop.Rig.Head.transform.localRotation=Quaternion.Euler(40,0,0);Pump();yield return null;art.AssertGhost(loop);R23SmokeControl.Capture(loop.Rig.Head,"ghost-actual-source-body-hands");art.AssertGhost(loop);
                loop.ReturnToLobby();Need(!loop.MatchLocalGhost&&!loop.Rig.SourceGhostMotion&&loop.UI.LocalMatchLifeState==null,"EndMatch left ghost policy or notice active");masks.AssertRestored(loop);art.AssertRestored(replacement);
                Note(g,"PASS End restores actual body originals and genuine same-root newglove source material; no shared-source shader/material/mesh mutation. Actual ghost POV captured for visual inspection, no headset claim.");
            }
            finally{loop.Gameplay.SetGloveMaterial(actualGlove);if(replacement!=null)UnityEngine.Object.Destroy(replacement);loop.Rig.Head.transform.localRotation=Quaternion.identity;}
        }
        IEnumerator Cleanup(Group g)
        {
            loop.ReturnToLobby();if(loop.Map.MapId!=saved.MapId){loop.SelectMap(saved.MapId,false);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+60);while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId==saved.MapId,"Original actual map cleanup failed");loop.ReturnToLobby();}
            Need(loop.ActualMatchRules==null&&!loop.Rig.SourceGhostMotion&&loop.UI.LocalMatchLifeState==null,"Scene cleanup left match/ghost state");Note(g,"Original actual map restored through loader; ghost ended, no geometry created, raw tracking/flags restore in finally.");
        }
        void Pump(float trigger=0)
        {
            Bound();loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true,Position=F(loop.Rig.transform.InverseTransformPoint(loop.Rig.Left.position))},new HandSample{Tracked=true,Position=F(loop.Rig.transform.InverseTransformPoint(loop.Rig.Right.position)),Trigger=trigger});loop.SmokeUiUpdate();Physics.SyncTransforms();
        }
        // Actual PlayerInput.Rearm requires .08s: five20ms samples provide .10s,
        // preserving the owner's threshold and held-control cancellation rules.
        void Neutral(){for(int i=0;i<5;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);Pump();}
        void Move(Vector2 move){Bound();loop.Rig.SmokeXrMove(move,.02f);Physics.SyncTransforms();}
        int WalkSteps(float distance){float speed=PlayerTuning.WalkSpeed*Mathf.Min(1,loop.UI.Settings.WalkMultiplier)*loop.ActualMatchRules.Walk;Need(speed>0,"Actual source walk setting is not positive");return Mathf.Min(300,Mathf.CeilToInt(distance/(speed*.02f))+3);}
        void Place(Vector3 feet,Vector3 forward){Need(FloorExists(feet,1),"Fixture site lacks actual native map floor");loop.Rig.SetSpawn(feet,feet+forward);loop.Calibration.NotifyRootPoseReset();Physics.SyncTransforms();Pump();}
        void DropFromSupport(Vector3 floor,float height,Vector3 forward)
        {
            RaycastHit native;Need(height>0&&height<=5&&MapRay(floor+Vector3.up*height,Vector3.down,height+.05f,out native)&&native.normal.y>.65f&&Vector3.Distance(native.point,floor)<.005f,"Airborne gravity test lacks the same actual native support below");
            Vector3 feet=floor+Vector3.up*height;loop.Rig.SetSpawn(feet,feet+forward);loop.Calibration.NotifyRootPoseReset();Physics.SyncTransforms();Pump();
        }
        Vector3 RangeOrigin(){R24LaunchMapInfo info;Need(loop.LaunchMapCatalog.TryRead("range",out info),"Real source Range frame unavailable");return loop.Map.transform.TransformPoint(V(info.RepresentedOriginUnity));}
        bool MapRay(Vector3 origin,Vector3 direction,float distance,out RaycastHit hit){hit=new RaycastHit();float nearest=float.PositiveInfinity;foreach(var h in Physics.RaycastAll(origin,direction,distance,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(h.collider.gameObject.layer!=19&&h.collider.gameObject.layer!=22&&h.collider.transform.IsChildOf(loop.Map.transform)&&h.distance<nearest){hit=h;nearest=h.distance;}return !float.IsPositiveInfinity(nearest);}
        Vector3 Floor(Vector3 from){RaycastHit hit;Need(MapRay(from,Vector3.down,10,out hit)&&hit.normal.y>.65f,"Actual source Range support ray missing");return hit.point;}
        bool FloorExists(Vector3 feet,float distance){RaycastHit hit;return MapRay(feet+Vector3.up*.3f,Vector3.down,distance,out hit)&&hit.normal.y>.65f;}
        bool FindActorLane(out Collider actor,out Vector3 foot,out Vector3 direction,out Vector3 head)
        {
            actor=null;foot=head=direction=Vector3.zero;
            for(int i=0;i<loop.Gameplay.PracticeTargetCount;i++)
            {
                Collider body;if(!loop.Gameplay.TryReadMeleeTargetCollider(i,out body)||!loop.Gameplay.IsSourceActorLayer4(body)||!body.gameObject.activeInHierarchy)continue;
                var dir=Vector3.ProjectOnPlane(body.transform.forward,Vector3.up).normalized;if(dir.sqrMagnitude<.5f)continue;var at=body.transform.position;
                if(!FloorExists(at-dir*.8f,1)||!FloorExists(at+dir*1.8f,1))continue;
                Vector3 eye=loop.Gameplay.PracticeAimPoint(i,true);RaycastHit world;if(MapRay(eye-dir*.8f,dir,2.6f,out world))continue;
                actor=body;foot=at;direction=dir;head=eye;return true;
            }
            return false;
        }
        string WeaponState(){var rows=new List<string>();foreach(string id in loop.Gameplay.OwnedWeaponActionIds){var s=loop.Gameplay.ReadWeaponState(id);rows.Add(id+":"+s.Loaded+":"+s.Seated+":"+s.Shots+":"+s.Phase);}return string.Join("|",rows.ToArray());}
        sealed class CollisionSnapshot
        {
            readonly int layer,include,exclude;readonly bool hull,enabled,detect;
            public CollisionSnapshot(R23PlayableLoop l){var b=l.Rig.Body;layer=b.gameObject.layer;include=b.includeLayers;exclude=b.excludeLayers;hull=l.Rig.UseSourceHeadHull;enabled=b.enabled;detect=b.detectCollisions;}
            public void AssertGhost(R23PlayableLoop l){var b=l.Rig.Body;Need(b.enabled&&b.gameObject.layer==2&&(int)b.includeLayers==0&&(int)b.excludeLayers==~0&&b.detectCollisions==detect&&l.Rig.UseSourceHeadHull&&l.Rig.HeadCollision.Ready,"Ghost did not retain enabled world head-hull +scoped exact controller policy");}
            public void AssertRestored(R23PlayableLoop l){var b=l.Rig.Body;Need(b.gameObject.layer==layer&&(int)b.includeLayers==include&&(int)b.excludeLayers==exclude&&b.enabled==enabled&&b.detectCollisions==detect&&l.Rig.UseSourceHeadHull==hull,"Original native collision policy not restored exactly");}
        }
        sealed class ArtSnapshot
        {
            sealed class Row{public Renderer View;public Material[] Materials;public Shader[] Shaders;public ShadowCastingMode Shadow;public Mesh Mesh;public bool Glove;}
            readonly List<Row> rows=new List<Row>();
            public ArtSnapshot(R23PlayableLoop l){Add(l.Appearance.CurrentVisualRoot,false);Add(l.Gameplay.SourceLeftGloveRoot,true);Add(l.Gameplay.SourceRightGloveRoot,true);Need(rows.Count>3,"Actual body/glove art absent");}
            void Add(Transform root,bool glove){Need(root!=null,"Actual source visual root absent");foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true)){var m=r.sharedMaterials;var shaders=new Shader[m.Length];for(int i=0;i<m.Length;i++)shaders[i]=m[i]==null?null:m[i].shader;rows.Add(new Row{View=r,Materials=m,Shaders=shaders,Shadow=r.shadowCastingMode,Mesh=r.GetComponent<MeshFilter>().sharedMesh,Glove=glove});}}
            public void AssertGhost(R23PlayableLoop l)
            {
                Need(l.LocalGhostAppearance!=null&&l.LocalGhostAppearance.Active&&l.LocalGhostAppearance.Failure==null,"Actual source ghost presenter failed");
                var sourceShader=Resources.Load<Shader>("R24SourceGhost");Need(sourceShader!=null&&!UnityEditor.ShaderUtil.ShaderHasError(sourceShader),"Source ghost shader compiler errors; appearance rendering is not accepted");
                foreach(var row in rows){Need(row.View!=null&&row.View.GetComponent<MeshFilter>().sharedMesh==row.Mesh&&row.View.shadowCastingMode==ShadowCastingMode.Off,"Ghost changed source geometry or kept shadows");var actual=row.View.sharedMaterials;Need(actual.Length==row.Materials.Length,"Source surface count changed");for(int i=0;i<actual.Length;i++){Need(actual[i]!=row.Materials[i]&&actual[i].shader.name=="Ashfall/R24SourceGhost"&&actual[i].shader.isSupported&&actual[i].renderQueue==3012&&Mathf.Abs(actual[i].GetFloat("_GhostOpacity")-.42f)<.00001f,"Actual source ghost shader/alpha/priority missing");if(row.Materials[i]!=null){Need(row.Materials[i].shader==row.Shaders[i],"Shared original material shader mutated");if(row.Materials[i].HasProperty("_BaseMap"))Need(actual[i].GetTexture("_SourceAlbedo")==row.Materials[i].GetTexture("_BaseMap"),"Ghost discarded actual original source albedo/alpha image");}}}
            }
            public void AssertRestored(Material gloveReplacement)
            {
                foreach(var row in rows){Need(row.View!=null&&row.View.shadowCastingMode==row.Shadow&&row.View.GetComponent<MeshFilter>().sharedMesh==row.Mesh,"Source renderer mesh/shadow restore failed");var actual=row.View.sharedMaterials;Need(actual.Length==row.Materials.Length,"Source surface count not restored");for(int i=0;i<actual.Length;i++)Need(actual[i]==(row.Glove&&gloveReplacement!=null?gloveReplacement:row.Materials[i]),"Exact source material reference restore failed");}
            }
        }
        sealed class Saved
        {
            public readonly string MapId;
            readonly bool manual,external,rigEnabled,pump,persist,blocked;readonly Vector3 root,origin,head,left,right;readonly Quaternion rootQ,headQ,leftQ,rightQ;readonly string selectedMap;
            public Saved(R23PlayableLoop l){var r=l.Rig;MapId=l.Map.MapId;manual=l.ManualSmoke;external=l.ExternalProbePump;rigEnabled=r.enabled;pump=l.Gameplay.ExternalInputPump;persist=l.Calibration.SuppressPersistence;blocked=r.GameplayBlocked;root=r.transform.position;rootQ=r.transform.rotation;origin=r.TrackingOrigin.localPosition;head=r.Head.transform.localPosition;headQ=r.Head.transform.localRotation;left=r.Left.localPosition;leftQ=r.Left.localRotation;right=r.Right.localPosition;rightQ=r.Right.localRotation;selectedMap=l.UI.SelectedMapId;}
            public void Restore(R23PlayableLoop l){var r=l.Rig;l.UI.ClearMatchSelection();if(selectedMap!=null)l.UI.SetSelectedMap(selectedMap);r.transform.SetPositionAndRotation(root,rootQ);r.TrackingOrigin.localPosition=origin;r.Head.transform.SetLocalPositionAndRotation(head,headQ);r.Left.SetLocalPositionAndRotation(left,leftQ);r.Right.SetLocalPositionAndRotation(right,rightQ);r.ClearCarrierMotion();l.Calibration.SuppressPersistence=persist;l.Gameplay.ExternalInputPump=pump;r.GameplayBlocked=blocked;r.enabled=rigEnabled;l.ExternalProbePump=external;l.ManualSmoke=manual;Physics.SyncTransforms();}
        }
    }
}
#endif
