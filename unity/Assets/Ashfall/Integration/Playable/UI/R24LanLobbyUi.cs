#if UNITY_6000_0_OR_NEWER
using System;using Ashfall.Integration.Playable.Networking;using UnityEngine;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        public event Action LanHostRequested,LanLeaveRequested;public event Action<string> LanJoinRequested;
        public event Action LanMapRequested;
        R24LanLobbyState lanState;string lanStatus="Not connected",lanRoom="",lanAddress="",lanMembers="",joinAddress="";bool enteringLanAddress;
        public R24LanLobbyState LanState{get{return lanState;}}
        public string LanStatus{get{return lanStatus;}}
        public string LanMembers{get{return lanMembers;}}
        bool LanMapHostOnly{get{return lanState==R24LanLobbyState.Connecting||lanState==R24LanLobbyState.Connected;}}
        public void SetLanLobby(R24LanLobbyState state,string status,string room,string[] members,string address)
        {
            string names=String.Join("\n",members??new string[0]);status=Clean(status,160);room=Clean(room,64);address=Clean(address,200);
            bool changed=lanState!=state||lanStatus!=status||lanRoom!=room||lanMembers!=names||lanAddress!=address;
            if(friendsOpen && onlineUiState!=Ashfall.Integration.Playable.Online.OnlineRoomState.Idle && members!=null && members.Length>1 && members.Length>friendRosterCount && MenuAudioActive())menuAudio.Play(R23MenuCue.Click);
            friendRosterCount=members==null?0:members.Length;
            lanState=state;lanStatus=status;lanRoom=room;lanMembers=names;lanAddress=address;
            if(changed&&(Page==R23UiPage.MatchRules&&matchTab==0||Page==R23UiPage.Maps||Page==R23UiPage.Ready))Build();
        }
        public void RequestLanHost(){if(LanHostRequested!=null)LanHostRequested();}
        public void RequestLanJoin(string address){if(LanJoinRequested!=null)LanJoinRequested(address);}
        public void RequestLanLeave(){if(LanLeaveRequested!=null)LanLeaveRequested();}
        void BuildLanHosting()
        {
            if(!friendsOther){FrontButton("PLAY WITH FRIENDS",340,330,600,88,OpenFriends,Hex(R25Theme.Primary),Hex(R25Theme.TextOnEmber),32);return;}
            BuildLanFallback();
        }
        void BuildLanFallback()
        {
            if(enteringLanAddress){BuildLanAddress();return;}
            bool active=lanState!=R24LanLobbyState.Offline;
            string heading=lanState==R24LanLobbyState.Hosting?"HOSTING LAN LOBBY":lanState==R24LanLobbyState.Connected?"CONNECTED LAN LOBBY":lanState==R24LanLobbyState.Connecting?"CONNECTING":"LOCAL / LAN";
            FrontText(heading,640,245,30,1140).color=Hex(R25Theme.Teal);
            FrontText(lanStatus,640,285,19,1140).richText=false;
            if(active){
                FrontText(lanRoom,640,325,23,1140).richText=false;
                FrontText(lanAddress,640,360,17,1180).richText=false;
                string[] rows=lanMembers.Length==0?new[]{"Waiting for the host roster"}:lanMembers.Split('\n');
                for(int i=0;i<rows.Length;i++)FrontText(rows[i],rows.Length>4?(i<4?365:915):640,411+(i%4)*29,21,rows.Length>4?500:1140).richText=false;
                if(lanState==R24LanLobbyState.Hosting){FrontButton("OPEN SELECTED MAP",70,545,430,46,delegate{if(LanMapRequested!=null)LanMapRequested();},Hex(R25Theme.RaisedSelected),Hex(R25Theme.Text),21);FrontButton("LEAVE LAN",530,545,250,46,RequestLanLeave,Hex(R25Theme.Raised),Hex(R25Theme.Text),21);}
                else FrontButton("LEAVE LAN",370,545,300,46,RequestLanLeave,Hex(R25Theme.Raised),Hex(R25Theme.Text),21);
                FrontButton("JOIN ANOTHER IP",lanState==R24LanLobbyState.Hosting?810:730,545,350,46,delegate{enteringLanAddress=true;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),21);
            }else{
                FrontText("Host a public LAN connection lobby, or enter the host's IPv4 address.",640,335,21,1180);
                FrontButton("HOST LAN",390,410,360,64,RequestLanHost,Hex(R25Theme.Raised),Hex(R25Theme.Text),23);
                FrontButton("JOIN IP",800,410,360,64,delegate{enteringLanAddress=true;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),23);
                FrontText("Use the same local network. Default UDP port: 24760.",640,510,19,1180).color=Hex(R25Theme.TextDim);
            }
            FrontText(active?"Shared combat, loot, food, revives, builds and pods.\nThe host owns damage, ammunition and shared items.":"Host or join to share maps, players and round starts.\nSTART MATCH starts a solo match when offline.",640,623,18,1200).color=Hex(R25Theme.TextDim);
        }
        void BuildLanAddress()
        {
            FrontText("JOIN LAN / HOST IPv4",640,242,27,1140);
            FrontText(joinAddress.Length==0?"ENTER HOST IP ADDRESS":joinAddress,640,282,28,1140).color=Hex(R25Theme.Teal);
            string[] keys={"1","2","3","4","5","6","7","8","9",".","0","DEL"};
            for(int i=0;i<keys.Length;i++){string key=keys[i];FrontButton(key,430+(i%3)*205,330+(i/3)*58,180,49,delegate{if(key=="DEL"){if(joinAddress.Length>0)joinAddress=joinAddress.Substring(0,joinAddress.Length-1);}else if(joinAddress.Length<15)joinAddress+=key;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),23);}
            FrontButton("CANCEL",405,565,300,46,delegate{enteringLanAddress=false;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),21);
            var connect=FrontButton("CONNECT",760,565,330,46,delegate{enteringLanAddress=false;RequestLanJoin(joinAddress);Build();},Hex(R25Theme.RaisedSelected),Hex(R25Theme.Text),21);connect.Enabled=R24LanPeer.IsAddress(joinAddress);connect.Help="Enter the host's four-part IPv4 address.\nBoth devices must use the same LAN.";if(!connect.Enabled)SetButtonStyle(connect,Hex(R25Theme.Disabled),Hex(R25Theme.TextDisabled));
            FrontText("Join a LAN lobby to play together with shared loot.",640,637,17,1180).color=Hex(R25Theme.TextDim);
        }
    }
}
#endif
