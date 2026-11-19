#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Match;
using UnityEngine;
namespace Ashfall.Integration.Playable.UI
{
    // Room continuity menus (campaign r12): one room picture for the room panel, pause menu and
    // Match Rules, the shared match actions, HOME confirmation and the bottom BACK button.
    public sealed partial class R23VrUI
    {
        // The one set of match actions. The pause menu and Match Rules both draw these labels
        // and call these handlers; guests never get them.
        const string StartMatchLabel="START MATCH",EndMatchLabel="END MATCH",RestartMatchLabel="RESTART MATCH";
        string StartActionLabel{get{return RoomHost&&room.Stage==R26RoomStage.Lobby?"ENTER WARM-UP":StartMatchLabel;}}
        R26RoomStatus room=R26RoomStatus.Offline;string roomNotice="";bool homeConfirm,roomConfirm;R24PlayableMatchRules runningMatchRules;
        public R26RoomStatus Room{get{return room;}}
        public string RoomNotice{get{return roomNotice;}}
        public bool HomeConfirmOpen{get{return homeConfirm;}}
        bool InRoomUi{get{return lanState==R24LanLobbyState.Hosting||lanState==R24LanLobbyState.Connected;}}
        bool RoomGuest{get{return LanMapHostOnly;}}
        bool RoomHost{get{return lanState==R24LanLobbyState.Hosting;}}
        // A match action is the host's (or the offline player's) and only on a settled room.
        bool MayControlMatch{get{return lanState==R24LanLobbyState.Offline||RoomHost&&!room.Busy&&!room.InTransition;}}
        public void SetRoomStatus(R26RoomStatus value)
        {
            Require();if(value==null)throw new ArgumentNullException("value");
            bool settled=room.Stage!=value.Stage||room.Members!=value.Members;room=value;if(value.InRoom&&value.MapId.Length>0)mapSelection.Select(value.MapId);if(settled&&!value.Busy)roomNotice="";
            if(!value.InRoom){roomConfirm=false;roomNotice="";}
            if(Page==R23UiPage.Ready||Page==R23UiPage.Pause||Page==R23UiPage.MatchRules)Build();
        }
        // A refusal or failure from the session owner, shown where the player is looking.
        public void SetRoomNotice(string value)
        {
            Require();string text=Clean(value,110);if(text==roomNotice)return;roomNotice=text;
            if(Page==R23UiPage.Ready||Page==R23UiPage.Pause||Page==R23UiPage.MatchRules)Build();
        }
        // Network status, roster and countdown updates rebuild a page while a trigger may be held on
        // one of its buttons. Carry that press over to the rebuilt button so the release still clicks.
        void Build()
        {
            string left=PressKey(0),right=PressKey(1);var page=Page;
            BuildPage();
            if(Page!=page||left==null&&right==null)return;
            foreach(var button in buttons)
            {
                if(!button.Enabled)continue;string key=ButtonKey(button);
                if(left!=null&&key==left){pressed[0]=button;armed[0]=true;left=null;}
                if(right!=null&&key==right){pressed[1]=button;armed[1]=true;right=null;}
            }
        }
        string PressKey(int hand){return held[hand]&&armed[hand]&&pressed[hand]!=null&&pressed[hand].Surface!=null&&pressed[hand].SliderChanged==null?ButtonKey(pressed[hand]):null;}
        static string ButtonKey(Button button)
        {var at=button.Surface.localPosition;return (button.Caption==null?"":button.Caption.text)+"@"+Mathf.RoundToInt(at.x*1000)+","+Mathf.RoundToInt(at.y*1000)+"@"+(button.OwnedId??"");}
        static string PresetLabel(string id){int index=Array.IndexOf(MatchPresetIds,id);return index<0?(id??"").ToUpperInvariant():MatchPresetLabels[index];}
        string MapLabel(string id){for(int i=0;i<mapSelection.Count;i++)if(mapSelection.Option(i).Id==id)return Clean(mapSelection.Option(i).Label,44);return Clean(id,44);}
        // "ROOM G H N 8 D W" for a code room; a direct LAN room has no code.
        string RoomName(){string code=R25FriendsFlow.Normalize(onlineUiCode);return code.Length==0?"LAN ROOM":"ROOM "+code;}
        string RoomLine(){return RoomName()+"   |   "+room.Headline()+"   |   "+room.Detail();}
        // ---- match actions ----
        void RequestEndMatch(){if(!MayControlMatch||!pauseRoundActive)return;if(MapLobbyRequested!=null)MapLobbyRequested();}
        void RequestRestartMatch(){if(!MayControlMatch||!pauseRoundActive)return;if(RestartRequested!=null)RestartRequested();}
        void StyleAction(Button button,bool primary,bool enabled)
        {button.Enabled=enabled;SetButtonStyle(button,Hex(!enabled?R25Theme.Disabled:primary?R25Theme.Primary:R25Theme.Raised),Hex(!enabled?R25Theme.TextDisabled:primary?R25Theme.TextOnEmber:R25Theme.Text));}
        // ---- HOME ----
        string HomeCaption(){return RoomHost?"HOME  (ROOM LOBBY)":RoomGuest?"HOME  (LEAVE ROOM)":"HOME";}
        // Leaving a room, ending a running match, or pulling other players out of a world is never one click.
        bool HomeNeedsConfirm{get{return RoomGuest||pauseRoundActive||RoomHost&&room.Members>1;}}
        void RequestHome()
        {
            if(HomeNeedsConfirm&&(Page==R23UiPage.Pause||Page==R23UiPage.Dead)){homeConfirm=true;Build();return;}
            SendHome();
        }
        void SendHome()
        {
            homeConfirm=false;if(MainMenuRequested!=null)MainMenuRequested();
            // A host's HOME is carried by the room transition a frame later; show the menu again until it lands.
            if(Page==R23UiPage.Pause||Page==R23UiPage.Dead)Build();
        }
        void BuildHomeConfirm()
        {
            if(Page==R23UiPage.Dead)Quad(panel,"SettingsBoard",Vector3.zero,new Vector2(1.3f,1.48f),Hex(R25Theme.Panel));
            string heading,body,confirm;
            if(RoomGuest){heading="LEAVE THE ROOM?";body="You leave alone and the room carries on.\nTo come back you need the room code again.";confirm="LEAVE ROOM";}
            else if(RoomHost){heading=pauseRoundActive?"END THE MATCH?":"RETURN TO ROOM LOBBY?";body=(pauseRoundActive?"The match ends for everyone.\n":"")+"Everyone returns to the room lobby together.\nThe room stays open with the same code.";confirm="RETURN ROOM TO LOBBY";}
            else{heading="END THE MATCH?";body="The running match ends\nand you return to the main menu.";confirm="END MATCH AND GO HOME";}
            title.text=heading;
            Text(panel,body,new Vector3(0,.30f,-.01f),.03f,1.16f,Hex(R25Theme.Text));
            var yes=AddButton(panel,confirm,0,.02f,1.0f,.14f,SendHome,.032f);SetButtonStyle(yes,Hex(R25Theme.Raised),Hex(R25Theme.DangerText));
            // CANCEL sits where HOME was, so a hurried second click cancels instead of confirming.
            AddButton(panel,"CANCEL",0,-.255f,1.14f,.19f,delegate{homeConfirm=false;Build();},.034f);
        }
        // ---- pause menu ----
        void BuildPause()
        {
            if(homeConfirm){BuildHomeConfirm();return;}
            title.text="ASHFALL";
            if(InRoomUi)Text(panel,RoomLine(),new Vector3(0,.557f,-.01f),.0185f,1.2f,Hex(R25Theme.Teal));
            MenuCard("RESUME",.42f,"pause0993_0",SetPlaying,.19f);
            MenuCard("SETTINGS",.195f,"pause0993_1",OpenSettings,.19f);
            MenuCard("CALIBRATE HEIGHT",-.03f,"pause0993_2",delegate{if(RecalibrationRequested!=null)RecalibrationRequested();},.19f);
            MenuCard(HomeCaption(),-.255f,"pause0993_3",RequestHome,.19f);
            const float y=-.435f,height=.08f,size=.019f;
            if(RoomGuest)
            {
                // Guests see the room, not the host's controls.
                var note=Text(panel,pauseRoundActive?"THE HOST CONTROLS THE MATCH":"THE HOST PICKS THE MAP AND STARTS THE MATCH",new Vector3(pauseRoundActive?0:-.15f,y,-.01f),.0185f,pauseRoundActive?1.14f:.82f,Hex(R25Theme.TextDim));note.richText=false;
                if(!pauseRoundActive)AddButton(panel,"OUTFITS",.4275f,y,.285f,height,OpenAppearance,size);
            }
            else
            {
                bool act=MayControlMatch;
                StyleAction(AddButton(panel,"MAP SELECT",-.4425f,y,.285f,height,OpenMaps,size),false,act);
                AddButton(panel,"MATCH RULES",-.1475f,y,.285f,height,OpenMatchRules,size);
                if(pauseRoundActive)
                {
                    StyleAction(AddButton(panel,RestartMatchLabel,.1475f,y,.285f,height,RequestRestartMatch,size),false,act);
                    StyleAction(AddButton(panel,EndMatchLabel,.4425f,y,.285f,height,RequestEndMatch,size),true,act);
                }
                else
                {
                    AddButton(panel,"OUTFITS",.1475f,y,.285f,height,OpenAppearance,size);
                    StyleAction(AddButton(panel,StartActionLabel,.4425f,y,.285f,height,delegate{RequestMatchStart();},size),true,act&&CanStartSelectedMatch);
                }
            }
            if(InRoomUi&&roomNotice.Length>0)Text(panel,roomNotice,new Vector3(0,-.502f,-.01f),.016f,1.2f,Hex(R25Theme.DangerText));
            // The bottom button closes the menu (the habit both playtesters had); HOME is no longer down here.
            AddButton(panel,"BACK",0,-.61f,1.14f,.14f,SetPlaying,.036f);
        }
        // ---- room lobby panel (menu city) ----
        void RequestRoomLeave(){if(RoomHost&&room.Members<=1){LeaveRoomNow();return;}roomConfirm=true;Build();}
        void LeaveRoomNow(){roomConfirm=false;FriendsBack();}
        void BuildRoomConfirm()
        {
            bool host=RoomHost;
            FrontText(host?"END THE ROOM FOR EVERYONE?":"LEAVE THE ROOM?",640,230,36,1160).color=Hex(R25Theme.DangerText);
            FrontText(host?"Everyone in the room is sent back to their main menu.\nThe room code stops working.":"You leave alone and the room carries on.\nTo come back you need the room code again.",640,350,28,1160);
            FrontButton(host?"END ROOM":"LEAVE ROOM",380,470,520,80,LeaveRoomNow,Hex(R25Theme.Raised),Hex(R25Theme.DangerText),32);
            FriendButton("CANCEL",40,710,300,delegate{roomConfirm=false;Build();});
        }
        void BuildRoomPanel()
        {
            if(roomConfirm){BuildRoomConfirm();return;}
            bool host=RoomHost;string code=R25FriendsFlow.Normalize(onlineUiCode);
            if(code.Length>0)
            {
                if(!host)FrontText("You are in your friend's room",640,128,26,820);
                FrontText(SpacedFriendCode(code,false),640,198,64,1160).color=Hex(R25Theme.Teal);
            }
            // Rooms are public by default; the code alone is the opt-in private mode.
            if(host&&code.Length>0)FrontButton(roomPrivate?"PRIVATE  (MAKE PUBLIC)":"PUBLIC  (MAKE PRIVATE)",930,104,310,50,delegate{if(RoomPrivacyRequested!=null)RoomPrivacyRequested(!roomPrivate);},Hex(R25Theme.Raised),Hex(roomPrivate?R25Theme.EmberHot:R25Theme.Teal),20);
            else if(code.Length==0){FrontText("LAN ROOM",640,128,26,1160);FrontText(lanAddress,640,198,24,1180).richText=false;}
            FrontText((room.Stage==R26RoomStage.Lobby?"Next map: ":"MAP   ")+MapLabel(room.MapId)+(room.Preset.Length>0?"      MODE   "+PresetLabel(room.Preset):""),640,264,21,1160).color=Hex(R25Theme.TextDim);
            // The real session stage: lobby, loading barrier with ready count, countdown, or a world this player is entering.
            FrontText(room.InRoom?room.Headline()+"      "+room.Detail():lanStatus,640,310,27,1160).color=Hex(R25Theme.Teal);
            string[] rows=lanMembers.Length==0?new[]{"Waiting for the room roster..."}:lanMembers.Split('\n');
            DrawRoomTeamRows(rows,355,26,20);
            if(roomNotice.Length>0)FrontText(roomNotice,640,566,20,1180).color=Hex(R25Theme.DangerText);
            bool lobby=room.Stage==R26RoomStage.Lobby;
            if(host&&lobby)
            {
                bool act=!room.Busy;
                StyleAction(FrontButton("MAP SELECT",40,610,300,80,OpenMaps,Hex(R25Theme.Raised),Hex(R25Theme.Text),27),false,act);
                StyleAction(FrontButton(StartActionLabel,380,610,520,80,delegate{RequestMatchStart();},Hex(R25Theme.Primary),Hex(R25Theme.TextOnEmber),36),true,act&&CanStartSelectedMatch);
                FrontButton("MATCH RULES",940,610,300,80,OpenMatchRules,Hex(R25Theme.Raised),Hex(R25Theme.Text),27);
            }
            else if(host)
                // Behind the barrier, or a world the host could not enter: the host can always pull the room back.
                FrontButton(room.InTransition||room.Busy?"CANCEL  (BACK TO ROOM LOBBY)":"RETURN ROOM TO LOBBY",340,610,600,80,delegate{if(MainMenuRequested!=null)MainMenuRequested();},Hex(R25Theme.Raised),Hex(R25Theme.Text),28);
            else
            {
                string waiting=lobby?"The host picks the map and starts the match.":room.Stage==R26RoomStage.Loading?"Loading with the room. Everyone enters together.":room.Stage==R26RoomStage.Starting?"Get ready.":room.Stage==R26RoomStage.Results?"The match is over. The host starts the next one.":"You enter with the room's next round or map.";
                FrontText(waiting,640,648,27,1160);
            }
            FriendButton(host?"END ROOM":"LEAVE ROOM",40,710,300,RequestRoomLeave);
            FriendButton("OUTFITS",600,710,300,OpenAppearance);
            FriendButton("SETTINGS",940,710,300,OpenSettings);
        }
        // ---- public room list (ROOM-PUBLIC-LIST) ----
        public event Action PublicRoomsRequested;public event Action<bool> RoomPrivacyRequested;
        Online.PublicRoom[] publicRooms=new Online.PublicRoom[0];Online.RoomListState publicRoomsState;string publicRoomsError="";
        bool friendsBrowse,roomPrivate,roomListed;int publicRoomPage;float nextPublicRoomRefresh;
        public bool BrowsingPublicRooms{get{return friendsOpen&&friendsBrowse&&Page==R23UiPage.Ready&&!InRoomUi;}}
        public void SetPublicRooms(Online.PublicRoom[] rooms,Online.RoomListState state,string error)
        {Require();publicRooms=rooms??new Online.PublicRoom[0];publicRoomsState=state;publicRoomsError=Clean(error,90);if(BrowsingPublicRooms)Build();}
        public void SetRoomPrivacy(bool isPrivate,bool listed)
        {Require();if(roomPrivate==isPrivate&&roomListed==listed)return;roomPrivate=isPrivate;roomListed=listed;if(Page==R23UiPage.Ready&&InRoomUi)Build();}
        void OpenPublicRooms(){friendsBrowse=true;publicRoomPage=0;RequestPublicRooms();Build();}
        void RequestPublicRooms(){nextPublicRoomRefresh=Time.unscaledTime+5;if(PublicRoomsRequested!=null)PublicRoomsRequested();}
        // The list refreshes itself every 5 s while it is on screen (the session throttles to the service limits).
        void TickRoomBrowser(){if(BrowsingPublicRooms&&Time.unscaledTime>=nextPublicRoomRefresh)RequestPublicRooms();}
        void JoinPublicRoom(Online.PublicRoom listing)
        {if(listing==null||listing.Full)return;friendsBrowse=false;friendsKeypad=false;friendCode=R25FriendsFlow.Normalize(listing.Code);JoinFriendRoom();}
        void BuildPublicRooms()
        {
            FrontText("PUBLIC ROOMS",640,138,30,1160);
            const int rowsPerPage=5;int pages=Math.Max(1,(publicRooms.Length+rowsPerPage-1)/rowsPerPage);publicRoomPage=Mathf.Clamp(publicRoomPage,0,pages-1);
            if(publicRooms.Length==0)
            {
                string empty=publicRoomsState==Online.RoomListState.Failed?(publicRoomsError.Length>0?publicRoomsError:"CANNOT REACH THE ROOM LIST"):publicRoomsState==Online.RoomListState.Ready?"No public rooms right now. Make one and friends can find it here.":"Looking for rooms...";
                FrontText(empty,640,380,28,1160).color=Hex(publicRoomsState==Online.RoomListState.Failed?R25Theme.DangerText:R25Theme.Text);
            }
            else
            {
                string[] heads={"HOST","MAP","PLAYERS",""};float[] columns={90,470,850,1010};
                for(int i=0;i<heads.Length;i++){var head=FrontText(heads[i],columns[i],192,17,300);head.anchor=TextAnchor.MiddleLeft;head.alignment=TextAlignment.Left;head.color=Hex(R25Theme.TextDim);}
                for(int slot=0;slot<rowsPerPage;slot++)
                {
                    int index=publicRoomPage*rowsPerPage+slot;if(index>=publicRooms.Length)break;var listing=publicRooms[index];float y=214+slot*92;
                    // The whole row is the join button.
                    var row=FrontButton("",60,y,1160,82,delegate{JoinPublicRoom(listing);},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);row.Enabled=!listing.Full;SetHovered(row,false);
                    string[] cells={Clean(listing.Name,24),MapLabel(listing.MapId),listing.Players+" / "+listing.MaxPlayers,listing.Full?"FULL":listing.MatchRunning?"MATCH RUNNING":"IN LOBBY"};
                    for(int i=0;i<cells.Length;i++){var cell=FrontText(cells[i],columns[i],y+41,i==0?27:22,i==0?360:i==1?360:i==2?150:210);cell.anchor=TextAnchor.MiddleLeft;cell.alignment=TextAlignment.Left;cell.richText=false;if(i==3)cell.color=Hex(listing.Full?R25Theme.TextDisabled:listing.MatchRunning?R25Theme.EmberHot:R25Theme.Teal);else if(listing.Full)cell.color=Hex(R25Theme.TextDisabled);}
                }
                if(pages>1)
                {
                    var previous=FrontButton("PREVIOUS",340,716,200,56,delegate{publicRoomPage--;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);previous.Enabled=publicRoomPage>0;
                    var next=FrontButton("NEXT",560,716,200,56,delegate{publicRoomPage++;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);next.Enabled=publicRoomPage<pages-1;
                }
            }
            FriendButton("REFRESH",940,710,300,RequestPublicRooms);
            FriendButton("BACK",40,710,240,delegate{friendsBrowse=false;Build();});
        }
        // Match Rules, HOSTING tab, while in a room: the room itself instead of a MULTIPLAYER button.
        void BuildRoomSummary()
        {
            FrontText(RoomName(),640,262,40,1160).color=Hex(R25Theme.Teal);
            FrontText(room.Headline()+"      "+room.Detail(),640,318,24,1160);
            FrontText((room.Stage==R26RoomStage.Lobby?"Next map: ":"MAP   ")+MapLabel(room.MapId),640,358,20,1160).color=Hex(R25Theme.TextDim);
            string[] rows=lanMembers.Length==0?new string[0]:lanMembers.Split('\n');
            DrawRoomTeamRows(rows,397,24,19);
            if(roomNotice.Length>0)FrontText(roomNotice,640,590,19,1180).color=Hex(R25Theme.DangerText);
        }
    }
}
#endif
