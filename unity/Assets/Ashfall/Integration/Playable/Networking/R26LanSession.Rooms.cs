#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using System.IO;using Ashfall.Modules.HealthStates;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    // Room continuity (campaign r12). A hosted room is ONE durable Session: shared maps, rounds
    // and HOME are epochs of it. It ends only through Leave() (host: END ROOM) or a lost transport.
    public sealed partial class R24LanSession
    {
        R26RoomStatus room=R26RoomStatus.Offline;
        string mapGuestNotice;
        public R26RoomStatus Room{get{return room;}}
        public bool Hosting{get{return peer!=null&&peer.Lobby.State==R24LanLobbyState.Hosting;}}
        void Update(){TickSession();SyncRoomStatus();SyncWarmupFeedback();PumpPublicRooms();}
        // User HOME as host: all members return to the room lobby (menu city) and stay connected.
        // True means the shared transition shows the menu (SyncMaps applies it next frame), so the
        // caller must not also show a local, unshared HOME.
        public bool RouteRoomLobby()
        {
            if(!Hosting)return false;
            // Retire an unsent host selection and any local apply; the current shared state re-applies.
            StopMapWork();
            if(!peer.Lobby.BeginRoomLobby()){Error("The room could not return to its lobby");return true;}
            Publish();return peer.Lobby.SelectedMap!=null;
        }
        // A room is made or joined from menus that can float inside a loaded world (Match Rules).
        // Its lobby is the menu city, so place the local player there before friends arrive.
        void EnterRoomLobby(){if(root!=null&&root.Ready&&root.Lobby!=null)root.ReturnToLobbyForLanTransition();}
        // Per-frame and allocation-free until something a menu shows actually changes.
        void SyncRoomStatus()
        {
            if(root==null||root.UI==null)return;
            var lobby=peer==null?null:peer.Lobby;
            if(lobby==null||lobby.State==R24LanLobbyState.Offline){if(room.InRoom){room=R26RoomStatus.Offline;root.UI.SetRoomStatus(room);}mapGuestNotice=null;return;}
            var map=lobby.SelectedMap;double left=lobby.RoundCountdown;bool host=lobby.State==R24LanLobbyState.Hosting;
            var stage=R26RoomStatus.StageOf(lobby.State,map,left,host&&preparingMap);
            var rules=map==null||map.Cancelled?null:map.Rules;string preset=stage==R26RoomStage.Loading&&preparingRules!=null?preparingRules.PresetId:rules==null?"":rules.PresetId;
            int members=lobby.MemberCount,ready=stage==R26RoomStage.Loading&&!(host&&preparingMap)?lobby.LoadedCount:stage==R26RoomStage.Loading?0:members;
            int countdown=stage==R26RoomStage.Starting?(int)Math.Ceiling(left):0,round=rules==null?0:map.Round,rounds=rules==null?0:rules.Rounds;
            bool busy=mapWorker!=null||MapEntryBlocked;
            bool statusChanged=!room.Same(stage,host,lobby.RoomMapId,preset,members,ready,R24LanLobby.Capacity,countdown,round,rounds,busy);
            if(statusChanged){room=new R26RoomStatus(stage,host,lobby.RoomMapId,preset,members,ready,R24LanLobby.Capacity,countdown,round,rounds,busy);root.UI.SetRoomStatus(room);}
            string notice=lobby.MapGuestNotice;
            if(notice!=mapGuestNotice)
            {
                if(notice!=null)Debug.LogWarning("LAN map "+lobby.MapId+": "+notice);
                else if(String.Equals(root.UI.RoomNotice,mapGuestNotice,StringComparison.OrdinalIgnoreCase))root.UI.SetRoomNotice("");
                mapGuestNotice=notice;
                if(notice!=null)root.UI.SetRoomNotice(notice);
            }
            else if(statusChanged&&notice!=null&&String.IsNullOrEmpty(root.UI.RoomNotice))root.UI.SetRoomNotice(notice);
        }
        // ---- Host Health epoch storage retention (LEAD decision 2026-11-15) ----
        // Every epoch has its own directory. A native restart always creates a fresh room and
        // identity, so an ended epoch has no recovery value: keep the live room's current and
        // previous epoch, and remove what dead rooms left behind before hosting again.
        readonly List<string> healthEpochDirectories=new List<string>();
        static string HealthStorageRoot{get{return Path.Combine(Application.persistentDataPath,"LanHealthReceipts-v1");}}
        IHealthImageStore OpenHealthEpochStore(string session,long epoch)
        {
            string root=HealthStorageRoot;
            // Same identity text as FileHealthImageStore, so the directory name is known without a hook.
            string name=FileHealthImageStore.Digest(System.Text.Encoding.UTF8.GetBytes("ashfall-health-image/1\n"+session+"\n"+epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+R24LanPeer.LobbyContent));
            // The lobby released every earlier epoch before asking for this one (BeginHealthEpoch).
            while(healthEpochDirectories.Count>1){DeleteHealthEpoch(Path.Combine(root,healthEpochDirectories[0]));healthEpochDirectories.RemoveAt(0);}
            var store=new FileHealthImageStore(root,session,epoch,R24LanPeer.LobbyContent,HealthStorageDiskBytes,HealthStorageImageBytes,HealthStorageEpochs);
            // Track only a directory the store really made under that name; an unknown layout is never deleted.
            if(Directory.Exists(Path.Combine(root,name))&&!healthEpochDirectories.Contains(name))healthEpochDirectories.Add(name);return store;
        }
        // owner.lock is opened FileShare.None, which Mono/IL2CPP enforce inside one process only
        // (measured on Linux, 2026-11-15). A second host process sharing this data folder (desktop
        // rigs) is seen through a byte-range lock instead: those are OS locks, visible across
        // processes and dropped by the OS when a host exits or crashes.
        FileStream healthHostLease;int healthHostByte=-1;
        bool ClaimHealthHostLease()
        {
            bool alone=true;
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                if(healthHostLease==null)healthHostLease=new FileStream(HealthStorageRoot+".hosts",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
                if(healthHostByte>=0){healthHostLease.Unlock(healthHostByte,1);healthHostByte=-1;}
                try{healthHostLease.Lock(0,4096);healthHostLease.Unlock(0,4096);}catch(IOException){alone=false;}
                int first=System.Diagnostics.Process.GetCurrentProcess().Id&4095;
                for(int i=0;i<16&&healthHostByte<0;i++){int at=(first+i)&4095;try{healthHostLease.Lock(at,1);healthHostByte=at;}catch(IOException){alone=false;}}
            }
            catch(IOException){alone=false;}catch(UnauthorizedAccessException){alone=false;}
            // No range locks on this platform: it runs one instance of the game, so nobody else can host here.
            catch(NotSupportedException){}catch(NotImplementedException){}
            return alone;
        }
        void ReleaseHealthHostLease()
        {var lease=healthHostLease;healthHostLease=null;healthHostByte=-1;if(lease==null)return;try{lease.Dispose();}catch(IOException){}}
        void PruneDeadHealthRooms()
        {
            healthEpochDirectories.Clear();string root=HealthStorageRoot;
            try
            {
                if(!ClaimHealthHostLease())return; // another live host shares this folder: leave every directory alone
                if(!Directory.Exists(root)||(new DirectoryInfo(root).Attributes&FileAttributes.ReparsePoint)!=0)return;
                // The store constructor serializes on this lease; hold it so no epoch is created mid-prune.
                using(new FileStream(Path.Combine(root,"quota.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                    foreach(string directory in Directory.GetDirectories(root))DeleteHealthEpoch(directory);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
        // Removes one epoch directory only when it is a plain store directory whose owner lease is free.
        // A live owner (this or another process) holds owner.lock exclusively and is left untouched.
        static void DeleteHealthEpoch(string directory)
        {
            try
            {
                var info=new DirectoryInfo(directory);string name=info.Name;
                if(!info.Exists||(info.Attributes&FileAttributes.ReparsePoint)!=0||name.Length!=64||Directory.GetDirectories(directory).Length!=0)return;
                foreach(char c in name)if(!(c>='0'&&c<='9')&&!(c>='a'&&c<='f'))return;
                string owner=Path.Combine(directory,"owner.lock");
                if(File.Exists(owner))using(new FileStream(owner,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
                foreach(string file in Directory.GetFiles(directory)){if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)return;}
                foreach(string file in Directory.GetFiles(directory))File.Delete(file);
                Directory.Delete(directory,false);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
#endif
