#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.UI;
using UnityEngine;

namespace Ashfall.Integration.Playable.Input
{
    public sealed partial class R24TacticalMap
    {
        R24LanLobby rosterLobby;
        R24LanMember[] roster=System.Array.Empty<R24LanMember>();
        readonly string[] rosterNames=new string[R24LanLobby.Capacity];
        bool rosterDirty=true;
        string rosterOwnActor,ownName="YOU",presenceScene;
        int ownTeam=-1;
        static readonly string[] TeamLabels={"TEAM 1","TEAM 2","TEAM 3","TEAM 4","TEAM 5","TEAM 6","TEAM 7","TEAM 8"};
        // Match compact HUD: zero-based team 0 is blue, team 1 is orange.
        static readonly Color[] TeamColors={Hex(R25Theme.Team2),Hex(R25Theme.Team1),Hex("54E39A"),Hex("D99CFF"),Hex("FFE16A"),Hex("5CE5EB"),Hex("FF89BB"),Hex("D4DCE6")};
        static readonly Color RosterRowBase=Hex("101C25");
        static string TeamLabel(int team){return team>=0&&team<TeamLabels.Length?TeamLabels[team]:"NO TEAM";}
        static Color TeamColor(int team){return team>=0&&team<TeamColors.Length?TeamColors[team]:Color.white;}
        void RosterChanged(){rosterDirty=true;nextDraw=0;}
        void UnbindRoster(){if(rosterLobby!=null)rosterLobby.Changed-=RosterChanged;rosterLobby=null;rosterDirty=true;}
        void ReadRoster()
        {
            var lobby=root.LanSession==null?null:root.LanSession.Lobby;
            if(lobby!=null&&lobby.State!=R24LanLobbyState.Hosting&&lobby.State!=R24LanLobbyState.Connected)lobby=null;
            if(!ReferenceEquals(lobby,rosterLobby)){UnbindRoster();rosterLobby=lobby;if(lobby!=null)lobby.Changed+=RosterChanged;}
            // Members returns a clone. Read it only after a lobby change, never per draw.
            if(rosterDirty)
            {
                var incoming=lobby==null?System.Array.Empty<R24LanMember>():lobby.Members;
                string actor=lobby==null?null:lobby.OwnActor;
                for(int i=0;i<incoming.Length;i++)
                    if(i>=roster.Length||incoming[i].Actor!=roster[i].Actor||incoming[i].Name!=roster[i].Name||incoming[i].Team!=roster[i].Team||actor!=rosterOwnActor)
                        rosterNames[i]=(incoming[i].Team>=0?"T"+(incoming[i].Team+1)+"  ":"")+incoming[i].Name+(incoming[i].Actor==actor?" (YOU)":"");
                roster=incoming;rosterOwnActor=actor;rosterDirty=false;
            }
            ownName="YOU";ownTeam=lobby==null?root.LocalMatchTeam:-1;
            for(int i=0;i<roster.Length;i++)if(roster[i].Actor==rosterOwnActor){ownName=rosterNames[i];ownTeam=roster[i].Team;break;}
        }
        // Updated by the actual draw path, also used by the opt-in runtime probe.
        readonly System.Collections.Generic.Dictionary<string,bool> teamVisMarkers=new System.Collections.Generic.Dictionary<string,bool>(System.StringComparer.Ordinal);
        public bool TryReadTeamMarker(string actor,out bool inactive){return teamVisMarkers.TryGetValue(actor,out inactive);}
        bool RosterInactive(string actor)
        {R24LanHealthState health;return rosterLobby!=null&&rosterLobby.TryReadHealth(actor,out health)&&R26TeamVisibility.Inactive(health.Life);}
        void DrawRoster()
        {
            ReadRoster();teamVisMarkers.Clear();bool match=rosterLobby!=null?R26TeamVisibility.Match(rosterLobby):root.MatchActive;
            int teams=rosterLobby!=null&&match?rosterLobby.SelectedMap.Rules.Teams:root.ActualMatchRules==null?0:root.ActualMatchRules.Teams;
            Text("PLAYERS",38,119,23,Color.white,245);
            Text("YOUR TEAM",1000,115,23,Color.white,355);Text(TeamLabel(ownTeam),1000,158,29,TeamColor(ownTeam),355);
            Text("Solid: alive   X: down / dead",310,792,20,Color.white,650);
            // The roster lists everyone. Only the map markers read the team visibility policy.
            float rowHeight=Mathf.Min(34,610f/Mathf.Max(1,roster.Length));
            if(rosterLobby==null)DrawRosterRow(ownName,ownTeam,166,rowHeight);
            for(int i=0;i<roster.Length;i++)DrawRosterRow(rosterNames[i],roster[i].Team,166+i*rowHeight,rowHeight);
            // Keep hidden marker label slots stable while poses become stale.
            for(int i=0;i<roster.Length;i++)
            {
                var member=roster[i];if(member.Actor==rosterOwnActor)continue;
                R24LanPoseSnapshot snapshot=null;bool visible=R26TeamVisibility.Show(match,teams,ownTeam,member.Team,false)&&rosterLobby.TryReadPresence(member.Actor,out snapshot)&&snapshot.Pose.Tracked&&snapshot.Pose.Scene==presenceScene&&Time.realtimeSinceStartupAsDouble-snapshot.ReceivedAt<=1;
                bool inactive=RosterInactive(member.Actor);Marker(member.Name,visible?V(snapshot.Pose.Root.Position):Vector3.zero,TeamColor(member.Team),false,visible,inactive);
                if(visible)teamVisMarkers[member.Actor]=inactive;
            }
            bool ownInactive=rosterLobby!=null?RosterInactive(rosterOwnActor):root.Gameplay.LocalMatchHealth!=null&&R26TeamVisibility.Inactive(root.Gameplay.LocalMatchHealth.Life);
            Marker(ownName,root.Rig.transform.position,TeamColor(ownTeam),true,true,ownInactive);if(rosterOwnActor!=null)teamVisMarkers[rosterOwnActor]=ownInactive;
        }
        void DrawRosterRow(string name,int team,float y,float height)
        {
            var tint=TeamColor(team);float top=y-(height-3)*.5f;
            Rect(30,top,261,height-3,Color.Lerp(RosterRowBase,tint,.12f));
            Rect(30,top,5,height-3,tint);
            Text(name,44,y,24,Color.white,238);
        }
    }
}
#endif
