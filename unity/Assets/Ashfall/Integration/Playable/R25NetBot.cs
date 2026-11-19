#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;
using Ashfall.Core;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Diagnostics;
namespace Ashfall.Integration.Playable
{
    // Drives devices, never invents host authority, ammo, damage or inventory.
    [DefaultExecutionOrder(-150)]
    public sealed partial class R25NetBot : MonoBehaviour
    {
        [Serializable] sealed class Entry
        {
            public string utc, kind, detail, state, scene;
            public float elapsed, health, shield, distance;
            public int members, acceptedShots, deniedShots, ammo, rejoinCount;
            public string[] passedChecks, failedChecks;
        }
        public static string Arg(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == key) return args[i + 1];
            return "";
        }
        public static bool Requested { get { return Arg("-ashfallNetBot").Length > 0; } }
        public static bool Finished { get; private set; }
        public static bool Passed { get; private set; }
        R23PlayableLoop root;
        StreamWriter log;
        string mode, target, routine, lastStep = "", lastState = "";
        float began, playBegan = -1, nextObservation, reconnectAt = -1, nextRejoin = 20;
        float duration = 180, previousHealth = -1;
        int joins, shots, denied, localShots, captures, admittedJoins;
        string admittedActor; float nextReload, nextEquip;
        long healthEpoch = -1; int healthSpawn = -1;
        string captureFolder;
        bool connected, presence, played, moved, picked, waved, crouched, thrown, closed, requestedMap;
        bool startAfterWarmup;
        Vector3 origin, circleCenter;
        int lastAmmo = -1;
        // Rig instrumentation and exploration; neither grants authority nor edits world state.
        R26PerfProbe probe; bool roam, alone; int audits; float roamUntil, nextSites; int roamSite = -1;
        Vector3[] roamSites = new Vector3[0]; readonly HashSet<int> roamVisited = new HashSet<int>();
        Vector3 roamProbe; float roamProbeAt; uint roamSeed = 2463534242;
        public static string Target()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 2 < args.Length; i++) if (args[i] == "-ashfallNetBot") return args[i + 2];
            return "";
        }
        IEnumerator Start()
        {
            Finished = Passed = false;
            mode = Arg("-ashfallNetBot"); target = Target(); routine = Arg("-ashfallNetBotRoutine");
            if (routine.Length == 0) routine = "default";
            began = Time.realtimeSinceStartup;
            Application.runInBackground = true; Application.targetFrameRate = 30;
            string rate = Arg("-ashfallNetBotFps"); int requestedRate;
            if (rate.Length > 0 && Int32.TryParse(rate, NumberStyles.Integer, CultureInfo.InvariantCulture, out requestedRate) && requestedRate >= 0 && requestedRate <= 1000)
            { QualitySettings.vSyncCount = 0; Application.targetFrameRate = requestedRate == 0 ? -1 : requestedRate; }
            roam = Arg("-ashfallNetBotRoam") == "true";
            // Optional rig switches, by name so this bot also builds against commits that predate them.
            if (Arg("-ashfallNetBotDurableHealth") == "true") { var durable = typeof(R24LanLobby).GetField("DurableHealthImages"); if (durable != null) durable.SetValue(null, true); }
            float cull; if (Single.TryParse(Arg("-ashfallNetBotCullRange"), NumberStyles.Float, CultureInfo.InvariantCulture, out cull))
            { var policy = typeof(R25NetBot).Assembly.GetType("Ashfall.Integration.Playable.World.Loot.R26PickupCulling"); var range = policy == null ? null : policy.GetProperty("ShowRange"); if (range != null) range.SetValue(null, cull, null); }
            string path = Arg("-ashfallNetBotLog");
            if (path.Length == 0) path = Path.Combine(Application.persistentDataPath, "netbot-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl");
            captureFolder = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(captureFolder);
            log = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)); log.AutoFlush = true;
            string seconds = Arg("-ashfallNetBotSeconds");
            if (seconds.Length == 0 && Arg("-ashfallNetBotMinutes").Length>0) { float minutes; if(Single.TryParse(Arg("-ashfallNetBotMinutes"),NumberStyles.Float,CultureInfo.InvariantCulture,out minutes))seconds=(minutes*60).ToString(CultureInfo.InvariantCulture);else seconds="invalid"; }
            if (seconds.Length > 0 && (!Single.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out duration) || duration < 1 || Single.IsInfinity(duration) || Single.IsNaN(duration)))
            { Finish("invalid-duration"); yield break; }
            if (!ReadCombatAI()) { Finish("invalid-skill"); yield break; }
            if (!ReadWindowRoute() || routine=="route"&&routePoints.Length==0) { Finish("invalid-route-options"); yield break; }
            Write("boot", "mode=" + mode + " routine=" + routine + " target=" + target);
            // "solo" never opens a session: the offline comparison for the same map, bot and probe.
            alone = Arg("-ashfallNetBotAlone") == "true";
            if ((mode != "host" && mode != "join" && mode != "join-code" && mode != "solo") || (routine != "default" && routine != "soak" && routine != "rejoin" && routine != "route") || (routine == "rejoin" && (mode == "host" || mode == "solo")))
            { Finish("invalid-mode-or-routine"); yield break; }
            root = GetComponent<R23PlayableLoop>();
            while (!root.Ready && root.Failure == null && Time.realtimeSinceStartup - began < 180) yield return null;
            if (!root.Ready || root.Failure != null) { Finish(root.Failure ?? "boot-timeout"); yield break; }
            root.Gameplay.AcceptedBallisticVolley += Shot;
            root.LanSession.RemoteGunshot+=HearGunshot;
            if (Arg("-ashfallNetBotPerf") != "off")
            {
                string perf = Arg("-ashfallNetBotPerf"); float window;
                if (perf.Length == 0) perf = Path.ChangeExtension(Path.GetFullPath(path), null) + ".perf.jsonl";
                if (!Single.TryParse(Arg("-ashfallNetBotPerfSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out window)) window = 2;
                probe = new R26PerfProbe(root, perf, mode, window, Arg("-ashfallNetBotPerfFilter"), Arg("-ashfallNetBotPerfDump"));
                gameObject.AddComponent<R26PerfFrameEnd>(); gameObject.AddComponent<R26PerfLanOpen>(); gameObject.AddComponent<R26PerfLanClose>();
            }
            root.Explosives.Thrown += GrenadeThrown;
            string map = Arg("-ashfallNetBotMap");
            if((mode=="host" || mode=="solo") && map.Length>0 && root.Map.MapId!=map)
            {
                root.SelectMap(map,false);
                yield return null;
                while(!root.Ready && Time.realtimeSinceStartup-began<180)yield return null;
                if(!root.Ready || root.Map.MapId!=map){Finish("map-selection-failed");yield break;}
            }
            Write("selected-map",root.Map.MapId);
            if (mode == "solo") { root.UI.ClearMatchSelection(); root.StartPlaying(); Write("solo-start", root.Map.MapId); }
            else Connect();
        }
        void Shot(string id, int side, Float3 at, Float3[] directions) { LogCombatShot(id); localShots++; Write("local-shot", id + " (host acceptance observed separately)"); }
        void GrenadeThrown(Ashfall.Integration.Playable.Explosives.R23GrenadeLaunch launch) { thrown = true; Write("grenade-thrown", launch.Kind); }
        void Connect()
        {
            Write("connect", mode + " " + target);
            if (mode == "host") root.LanSession.Host();
            else if (mode == "join") root.LanSession.Join(target);
            else
            {
                root.LanSession.JoinOnline(target);
            }
        }
        void Update()
        {
            // Earliest enabled script of the frame (the tracked rig is disabled under the bot).
            if (probe != null && !closed) { probe.Frame(); R26PerfMarks.Begin(R26PerfMarks.Frame); R26PerfMarks.Begin(R26PerfMarks.Updates); R26PerfMarks.Begin(R26PerfMarks.BotTick); }
            if (closed || root == null || !root.Ready || log == null) return;
            try { Tick(); } catch (Exception error) { Write("exception", error.ToString()); Finish("runtime-exception"); }
            R26PerfMarks.End(R26PerfMarks.BotTick);
        }
        // Optional evidence partial: no dependency while its workstream is still writing the fixture.
        partial void TickSwordEvidence(ref bool handled);
        void Tick()
        {
            bool evidenceHandled=false;TickSwordEvidence(ref evidenceHandled);if(evidenceHandled)return;
            if(Arg("-ashfallReviveEvidence")=="true")return;
            float now = Time.realtimeSinceStartup, dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            var lobby = root.LanSession.Lobby;
            if (reconnectAt > 0 && now >= reconnectAt) { reconnectAt = -1; Connect(); lobby = root.LanSession.Lobby; }
            bool live = lobby != null && (lobby.State == R24LanLobbyState.Hosting || lobby.State == R24LanLobbyState.Connected);
            if (live && lobby.Members.Length > 1)
            {
                connected = true;
                if(lobby.OwnActor!=admittedActor) { admittedActor=lobby.OwnActor;admittedJoins++;Write("admitted","admissions="+admittedJoins); }
            }
            R24LanPoseSnapshot host = null;
            if (live) foreach (var member in lobby.Members) if (member.Actor != lobby.OwnActor)
            {
                R24LanPoseSnapshot pose;
                if (lobby.TryReadPresence(member.Actor, out pose) && now - pose.ReceivedAt <= 1 && pose.Pose.Tracked)
                { presence = true; host = pose; }
            }
            if (mode == "host" && live && (lobby.Members.Length > 1 || alone) && !requestedMap && now - began > 12)
            { requestedMap = true; startAfterWarmup = true; root.LanSession.RouteMapEntry(root.Map.MapId); }
            if (startAfterWarmup && live && lobby.WarmupPlayersPlaced && !root.LanSession.LoadingSharedMap && !root.LanSession.MapEntryBlocked)
            { startAfterWarmup = false; Write("start-round", "Host starts the match from warm-up"); root.LanSession.RouteRoundRequest(HitregRoundRules()); }
            bool playing = (live || mode == "solo") && root.Lobby != null && !root.Lobby.Visible && !root.LanSession.MapEntryBlocked;
            if (routePoints.Length > 0 && root.SourceArchiveEntryPending) playing = false;
            if(live)host=SelectCombatTarget(lobby,now);
            R24LanHealthState botHealth;bool botAlive=!live||AiHealth(lobby,lobby.OwnActor,out botHealth)&&botHealth.Life==Ashfall.Modules.HealthStates.LifeState.Alive;
            Vector2 move = Vector2.zero; float height = 1.7f, grip = 0, leftGrip = 0, trigger = 0;
            Vector3 head = root.Rig.transform.position + Vector3.up * height;
            Vector3 left = head + root.Rig.transform.rotation * new Vector3(-.32f, -.35f, .35f);
            Vector3 right = head + root.Rig.transform.rotation * new Vector3(.32f, -.35f, .35f);
            Quaternion aim = root.Rig.transform.rotation;
            string step = "wait-in-lobby";
            if (playing && botAlive && routePoints.Length == 0)
            {
                if (playBegan < 0) { playBegan = now; origin = root.Rig.transform.position; circleCenter = origin - root.Rig.transform.right * 2; if (probe != null) probe.PlayBegan = now; }
                played = true;
                float t = now - playBegan, phase = t % 48;
                // Turn the synthetic observer, then approach through the normal swept motor.
                if(host!=null)
                {
                    Vector3 facing=Vector3.ProjectOnPlane(V(host.Pose.Head.Position)-head,Vector3.up);
                    if(facing.sqrMagnitude>.01f)root.Rig.transform.rotation=Quaternion.RotateTowards(root.Rig.transform.rotation,Quaternion.LookRotation(facing),dt*90);
                    head=root.Rig.transform.position+Vector3.up*height;
                    left=head+root.Rig.transform.rotation*new Vector3(-.32f,-.35f,.35f);
                    right=head+root.Rig.transform.rotation*new Vector3(.32f,-.35f,.35f);
                    aim=root.Rig.transform.rotation;
                }
                // A 4 m diameter circle, approached through the swept normal motor.
                if (host != null) circleCenter = V(host.Pose.Root.Position);
                Vector3 goal = circleCenter + new Vector3(Mathf.Cos(t * .22f), 0, Mathf.Sin(t * .22f)) * 2;
                Vector3 delta = Vector3.ProjectOnPlane(goal - root.Rig.transform.position, Vector3.up);
                Vector3 local = root.Rig.transform.InverseTransformDirection(delta);
                if (phase < 14 || routine == "soak") move = Vector2.ClampMagnitude(new Vector2(local.x, local.z) * .3f, .35f);
                if(host!=null && !roam)
                {
                    Vector3 toward=Vector3.ProjectOnPlane(V(host.Pose.Root.Position)-root.Rig.transform.position,Vector3.up);
                    Vector3 approach=root.Rig.transform.InverseTransformDirection(toward);
                    move=toward.magnitude>3?Vector2.ClampMagnitude(new Vector2(approach.x,approach.z)*.3f,.6f):Vector2.zero;
                }
                if(roam)move=Roam(now,move);
                // Release the stick after spawn/focus resets so the normal neutral gate rearms.
                if(t<1 || t%12<.25f)move=Vector2.zero;
                moved |= Vector3.ProjectOnPlane(root.Rig.transform.position - origin, Vector3.up).magnitude > .5f;
                step = "walk-circle";
                if (phase >= 14 && phase < 19)
                { step = "wave-both-hands"; left.y = right.y = head.y + .25f; left.x += Mathf.Sin(t * 5) * .18f; right.x -= Mathf.Sin(t * 5) * .18f; waved = true; }
                if (phase >= 19 && phase < 27 && !root.Gameplay.HasWeapon)
                {
                    step = "approach-nearest-weapon";
                    Vector3 nearest = root.Gameplay.WeaponGripPosition; float best = Single.MaxValue;
                    foreach (string id in root.Gameplay.WorldWeaponActionIds)
                    { Vector3 p; Quaternion q; if (root.Gameplay.TryGetDroppedPose(id, out p, out q) && (p-root.Rig.transform.position).sqrMagnitude < best) { nearest=p; best=(p-root.Rig.transform.position).sqrMagnitude; } }
                    Vector3 toward = Vector3.ProjectOnPlane(nearest-root.Rig.transform.position,Vector3.up);
                    Vector3 walk = root.Rig.transform.InverseTransformDirection(toward); move=Vector2.ClampMagnitude(new Vector2(walk.x,walk.z)*.25f,.3f);
                    if (toward.magnitude < .85f) { right = Vector3.MoveTowards(right, nearest, 1); grip = ((int)(t*2)%2)==0 ? .8f : 0; }
                }
                if(!root.Gameplay.HasWeapon && now>=nextEquip)
                {
                    nextEquip=now+1;
                    foreach(string id in root.Gameplay.OwnedWeaponActionIds)
                    { if(root.NetBotEquipOwned(id)){Write("equip-owned",id);break;} }
                }
                picked |= root.Gameplay.HasWeapon;
                if(root.Gameplay.HasWeapon && (root.Gameplay.Ammo==0 || root.Gameplay.Reloading) && now>=nextReload)
                { nextReload=now+1; Write("reload-attempt",root.Gameplay.ReloadInstruction+" reserve="+root.Gameplay.Reserve);root.Gameplay.Reload(); }
                if (!live && (phase >= 27 && phase < 39 || routine == "soak") && root.Gameplay.HasWeapon)
                {
                    step = "fire-at-wall";
                    Vector3 point = head + root.Rig.transform.forward * 20;
                    if (host != null)
                    { Vector3 to = V(host.Pose.Head.Position)-right; if(Vector3.Dot(to.normalized,root.Rig.Head.transform.forward)>.25f && !Physics.Linecast(right,V(host.Pose.Head.Position),1<<0)) { point=V(host.Pose.Head.Position); step="fire-at-host"; } }
                    aim = Quaternion.LookRotation(point-right) * Quaternion.Inverse(Ashfall.Integration.Playable.HandsWeapons.R23TrackedGripPose.LocalRotation);
                    trigger = t % 2 < .35f ? 1 : 0;
                }
                if (phase >= 39 && phase < 43) { step="crouch"; height=1.1f; crouched=true; }
                if (phase >= 43 && !live)
                {
                    step="throw-grenade-if-owned";
                    if (root.Explosives.HeldId == null && root.Explosives.OwnedItems.Length > 0)
                    { root.Gameplay.HolsterCurrent(); root.Explosives.TryEquip(root.Explosives.OwnedItems[0].Id,2,true); }
                    trigger = phase < 46 ? 1 : 0;
                    right += root.Rig.transform.forward * Mathf.Clamp01((phase-45)*2) * .5f;
                }
            }
            if (playing && routePoints.Length > 0) WindowRoute(now, ref move, ref height, ref left, ref right, ref aim, ref grip, ref trigger, ref step);
            if(playing&&routePoints.Length==0)AimHitreg(host,ref move,ref height,ref left,ref right,ref aim,ref trigger,ref grip);
            if(playing&&live&&routePoints.Length==0){CombatAI(now,dt,host,ref move,ref left,ref right,ref aim,ref trigger,ref grip,ref step);
                if(botAlive&&(roam||routine=="soak"))SteerMatch(lobby,now,host!=null&&aiVisible,ref move,ref left,ref leftGrip,ref step);}
            if(playing&&live&&botAlive&&routePoints.Length==0&&root.PlatformUnarmed)DeployFromPlatform(lobby,now,ref move,ref grip,ref trigger,ref step);
            else deckEntered=-1;
            if (step != lastStep) { lastStep=step; Write("step",step); }
            height = Mathf.MoveTowards(root.Rig.Head.transform.localPosition.y, height, dt * 1.5f);
            if(!botAlive){move=Vector2.zero;trigger=grip=0;height=root.Rig.Head.transform.localPosition.y;left=root.Rig.Left.position;right=root.Rig.Right.position;aim=root.Rig.Right.rotation;}
            root.Rig.NetBotSample(move,height,left,right,aim,grip,trigger,dt,leftGrip);
            if (playing && routePoints.Length > 0) ObserveWindowRoute(now);
            if(playBegan >= 0 && captures < 3 && now-playBegan >= (captures==0?10:captures==1?90:170))
            { Capture(captures++); }
            CaptureRemoteEvidence();
            CaptureRulesEvidence();
            if (routine == "rejoin" && live && botAlive && now-began >= nextRejoin && joins < 3)
            { joins++; Write("leave", "rejoin cycle " + joins); root.LanSession.Leave(); reconnectAt=now+3; nextRejoin=now-began+25; }
            if (now >= nextObservation) { nextObservation=now+2; Observe(); }
            if (probe != null && playBegan >= 0 && audits < 3 && now-playBegan >= (audits==0?20:audits==1?duration*.5f:duration-3)) { probe.Audit(audits==0?"early":audits==1?"middle":"end"); audits++; }
            if (playBegan >= 0 && now-playBegan >= duration || now-began > duration+180) Finish(null);
        }
        static Vector3 V(Float3 p) { return new Vector3(p.X,p.Y,p.Z); }
        // Walks toward the nearest unvisited loot cache site through the normal motor, as an exploring player would.
        Vector2 Roam(float now, Vector2 fallback)
        {
            Vector3 here = root.Rig.transform.position;
            if (now >= nextSites && root.WorldLoot != null) { nextSites = now + 5; roamSites = root.WorldLoot.CacheSitePositions; }
            if (roamSite >= roamSites.Length) { roamSite = -1; roamVisited.Clear(); }
            if (roamSite >= 0 && (now >= roamUntil || Vector3.ProjectOnPlane(roamSites[roamSite] - here, Vector3.up).magnitude < 4)) { roamVisited.Add(roamSite); roamSite = -1; }
            // Blocked by map geometry: under 1.5 m of progress in 3 s gives the site up and picks one of the eight nearest at random.
            bool blocked = false;
            if (now >= roamProbeAt) { blocked = roamSite >= 0 && Vector3.ProjectOnPlane(here - roamProbe, Vector3.up).magnitude < 1.5f; if (blocked) { roamVisited.Add(roamSite); roamSite = -1; } roamProbe = here; roamProbeAt = now + 3; }
            if (roamSite < 0)
            {
                if (roamVisited.Count >= roamSites.Length) roamVisited.Clear();
                float best = Single.MaxValue;
                for (int i = 0; i < roamSites.Length; i++) { float d = (roamSites[i] - here).sqrMagnitude; if (!roamVisited.Contains(i) && d < best) { best = d; roamSite = i; } }
                if (blocked && roamSite >= 0)
                {
                    roamSeed ^= roamSeed << 13; roamSeed ^= roamSeed >> 17; roamSeed ^= roamSeed << 5; int skip = (int)(roamSeed % 8);
                    // The (skip+1)-th nearest unvisited site: walk the candidates in distance order.
                    float floor = best; for (int k = 0; k < skip; k++) { float next = Single.MaxValue; int pick = -1; for (int i = 0; i < roamSites.Length; i++) { float d = (roamSites[i] - here).sqrMagnitude; if (!roamVisited.Contains(i) && d > floor && d < next) { next = d; pick = i; } } if (pick < 0) break; floor = next; roamSite = pick; best = next; }
                }
                if (roamSite < 0) return fallback; // a map without cache sites (Sandbox Range): keep the walking circle
                roamUntil = now + 25; Write("roam", "site=" + roamSite + " of " + roamSites.Length + " distance=" + Mathf.Sqrt(best).ToString("0.0", CultureInfo.InvariantCulture));
            }
            Vector3 toward = Vector3.ProjectOnPlane(roamSites[roamSite] - here, Vector3.up);
            if (toward.sqrMagnitude > .01f) root.Rig.transform.rotation = Quaternion.RotateTowards(root.Rig.transform.rotation, Quaternion.LookRotation(toward), Mathf.Min(Time.unscaledDeltaTime, .1f) * 120);
            Vector3 local = root.Rig.transform.InverseTransformDirection(toward);
            return Vector2.ClampMagnitude(new Vector2(local.x, local.z), .8f);
        }
        void Capture(int index)
        {
            var camera=root.Rig.Head;var previous=camera.targetTexture;var active=RenderTexture.active;
            var targetTexture=RenderTexture.GetTemporary(960,540,24);Texture2D pixels=null;
            try { camera.targetTexture=targetTexture;camera.Render();RenderTexture.active=targetTexture;pixels=new Texture2D(960,540,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();string path=Path.Combine(captureFolder,mode+"-sample-"+index+".png");File.WriteAllBytes(path,pixels.EncodeToPNG());Write("capture",path); }
            finally {camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(targetTexture);if(pixels!=null)Destroy(pixels);}
        }
        void Observe()
        {
            ObserveHitreg();
            var lobby=root.LanSession.Lobby;
            string state=lobby==null?"Offline:"+root.UI.LanStatus:lobby.State+":"+lobby.Status;
            if(state!=lastState) { Write("lobby-state",state); if(lobby==null)Write("disconnect",state); lastState=state; }
            if(lobby!=null)
            {
                foreach(var member in lobby.Members) Write("member",member.Actor+" "+member.Name+" team="+member.Team+" host="+member.Host+" loaded="+member.WorldLoaded);
                if(lobby.AcceptedShots!=shots || lobby.RejectedShots!=denied) { shots=lobby.AcceptedShots;denied=lobby.RejectedShots;Write("host-shot-totals", "accepted="+shots+" denied="+denied+" reason="+lobby.LastShotRejection); }
                var selection=lobby.SelectedMap;
                if(selection!=null) Write("round", "epoch="+selection.Epoch+" active="+selection.Active+" results="+selection.Results+" cancelled="+selection.Cancelled);
            }
            R24LanHealthState own;
            if(lobby!=null && lobby.TryReadHealth(lobby.OwnActor,out own))
            {
                float health=own.Health+own.Shield;
                if(healthEpoch==own.Epoch && healthSpawn==own.SpawnRevision)
                {
                    if(previousHealth>=0 && health<previousHealth)Write("damage-taken",(previousHealth-health).ToString(CultureInfo.InvariantCulture));
                    if(previousHealth>0 && health<=0)Write("death","host-confirmed life="+own.Life+" deaths="+own.Deaths);
                }
                previousHealth=health;healthEpoch=own.Epoch;healthSpawn=own.SpawnRevision;
            }
            else { previousHealth=-1;Write("health-pending","awaiting host health/spawn; zero presentation is not a death"); }
            if(lobby!=null)foreach(var member in lobby.Members)
            {
                R24LanHealthState h;
                if(lobby.TryReadHealth(member.Actor,out h))Write("authority-health",member.Actor+" epoch="+h.Epoch+" spawn="+h.SpawnRevision+" life="+h.Life+" deaths="+h.Deaths+" health="+h.Health+" shield="+h.Shield);
            }
            if(root.Gameplay.Ammo!=lastAmmo) { lastAmmo=root.Gameplay.Ammo;Write("ammo-observed", "authoritative reconciliation may follow local prediction: "+lastAmmo); }
            Write("observation","hostPresenceReceived="+presence+" localShots="+localShots+" messagesIn="+root.LanSession.MessagesIn+" messagesOut="+root.LanSession.MessagesOut+" rawRejected="+(lobby==null?"n/a":lobby.RejectedRawTracking+":"+lobby.LastRawTrackingRejection));
        }
        void Write(string kind,string detail,string[] pass=null,string[] fail=null)
        {
            if(log==null)return;
            var lobby=root==null||root.LanSession==null?null:root.LanSession.Lobby;
            var e=new Entry {utc=DateTime.UtcNow.ToString("o"),kind=kind,detail=detail,elapsed=Time.realtimeSinceStartup-began,
                state=lobby==null?"Offline":lobby.State.ToString(),scene=root==null||root.LanSession==null?"":root.LanSession.PresenceScene,
                members=lobby==null?0:lobby.Members.Length,acceptedShots=shots,deniedShots=denied,rejoinCount=joins,
                health=root==null||root.Gameplay==null?0:root.Gameplay.Health,shield=root==null||root.Gameplay==null?0:root.Gameplay.Shield,
                ammo=root==null||root.Gameplay==null?0:root.Gameplay.Ammo,distance=root==null||root.Rig==null?0:root.Rig.DistanceMoved,
                passedChecks=pass,failedChecks=fail};
            log.WriteLine(JsonUtility.ToJson(e));Debug.Log("ASHFALL NETBOT "+kind+" "+detail);
        }
        void Finish(string failure)
        {
            if(closed)return;
            var pass=new List<string>();var fail=new List<string>();
            bool single = mode == "solo" || alone;
            if (!single) { Check(pass,fail,"second-member",connected); Check(pass,fail,"remote-presence",presence); }
            Check(pass,fail,"map-entered",played);Check(pass,fail,"movement",moved);
            if(routine=="rejoin")Check(pass,fail,"three-rejoins",joins==3 && admittedJoins>=4);
            else if (!single) { Check(pass,fail,"weapon-held",picked);Check(pass,fail,"local-shots",localShots>0); if(mode=="host")Check(pass,fail,"host-accepted-shots",shots>0); if(routine=="default") {Check(pass,fail,"wave",waved);Check(pass,fail,"crouch",crouched);} }
            if(routePoints.Length > 0) { Check(pass,fail,"route-completed",routeIndex >= routePoints.Length); WriteWindowPosition("route-final"); }
            if(failure!=null)fail.Add(failure);
            Write("summary", "grenadeThrown="+thrown+"; absent grenades are skipped",pass.ToArray(),fail.ToArray());
            closed=true;Passed=fail.Count==0;Finished=true;
            if(root!=null && root.LanSession!=null)root.LanSession.Leave();
            if(probe!=null){probe.Dispose();probe=null;}
            if(log!=null){log.Dispose();log=null;}
            if(!Application.isEditor)Application.Quit(Passed?0:2);
        }
        static void Check(List<string> pass,List<string> fail,string name,bool ok) { (ok?pass:fail).Add(name); }
        void OnApplicationQuit(){Finish("process-stopped");}
        void OnDestroy()
        {
            if(root!=null && root.Gameplay!=null)root.Gameplay.AcceptedBallisticVolley-=Shot;
            if(root!=null && root.LanSession!=null)root.LanSession.RemoteGunshot-=HearGunshot;
            if(root!=null && root.Explosives!=null)root.Explosives.Thrown-=GrenadeThrown;
            if(!closed)Finish("component-destroyed");
        }
    }
}
#endif
