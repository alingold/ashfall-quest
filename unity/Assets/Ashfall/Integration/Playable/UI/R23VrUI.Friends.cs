#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Integration.Playable.Online;
using Ashfall.Integration.Playable.Networking;
using UnityEngine;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        public event Action OnlineHostRequested;
        public event Action<string> OnlineJoinRequested;
        bool friendsOpen, friendsKeypad, friendsOther, friendHost;
        string friendCode="", onlineUiCode="", onlineUiReason="";
        int friendRosterCount;
        OnlineRoomState onlineUiState;
        TextMesh friendWaiting;
        public void OpenFriends(){friendsOpen=true;if(titleRoot!=null)titleRoot.gameObject.SetActive(false);SetPage(R23UiPage.Ready);}
        public void SetOnlineRoom(OnlineRoomState state,string code,string reason)
        {
            if(onlineUiState==state && onlineUiCode==code && onlineUiReason==reason)return;
            onlineUiState=state;onlineUiCode=code;onlineUiReason=reason;
            if(friendsOpen && (Page==R23UiPage.Ready||Page==R23UiPage.SpawnChoice))Build();
        }
        void FriendsBack()
        {
            roomConfirm=false;friendsBrowse=false;RequestLanLeave();friendsKeypad=false;friendsOther=false;enteringLanAddress=false;friendCode="";
            OpenFriends();
        }
        void MakeFriendRoom()
        {
            friendHost=true;friendsKeypad=false;friendsOther=false;
            if(OnlineHostRequested!=null)OnlineHostRequested();Build();
        }
        void JoinFriendRoom()
        {
            friendHost=false;
            if(OnlineJoinRequested!=null)OnlineJoinRequested(friendCode);Build();
        }
        void FriendKey(string key)
        {
            if(onlineUiState!=OnlineRoomState.Idle)return;
            friendCode=R25FriendsFlow.Edit(friendCode,key);
            if(friendCode.Length==6)JoinFriendRoom();else Build();
        }
        void FriendButton(string caption,float x,float y,float width,Action action,bool primary=false)
        {
            FrontButton(caption,x,y,width,72,action,Hex(primary?R25Theme.Primary:R25Theme.Raised),Hex(primary?R25Theme.TextOnEmber:R25Theme.Text),27);
        }
        void BuildFriends()
        {
            title.text="";friendWaiting=null;
            Quad(panel,"Friends panel backdrop",new Vector3(0,0,.004f),new Vector2(2.2f,1.375f),Hex(R25Theme.Panel));
            Quad(panel,"Friends header backdrop",new Vector3(0,.612f,-.002f),new Vector2(2.2f,.151f),Hex(R25Theme.Header));
            Quad(panel,"Friends header accent",new Vector3(0,.536f,-.004f),new Vector2(.206f,.007f),Hex(R25Theme.Ember));
            FrontText("MULTIPLAYER",640,60,38,1160);
            // In a room (code room or direct LAN) the panel is the room lobby, whatever opened it.
            if(InRoomUi){BuildRoomPanel();return;}
            if(friendsOther)
            {
                BuildLanFallback();FriendButton("BACK",40,710,240,FriendsBack);return;
            }
            FriendsScreen screen=R25FriendsFlow.Screen(onlineUiState,friendsKeypad);
            if(screen==FriendsScreen.Choice&&friendsBrowse){BuildPublicRooms();return;}
            if(screen==FriendsScreen.Choice)
            {
                FrontText("Make a room, find a public room, or use your friend's code.",640,196,28,1160);
                FriendButton("MAKE A ROOM",320,262,640,MakeFriendRoom,true);
                FriendButton("FIND A ROOM",320,358,640,OpenPublicRooms);
                FriendButton("JOIN WITH A CODE",320,454,640,delegate{friendsKeypad=true;friendCode="";Build();});
                FrontButton("Other ways to connect",400,582,480,56,delegate{friendsOther=true;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
                FriendButton("BACK",40,710,240,delegate{friendsOpen=false;Build();});return;
            }
            if(screen==FriendsScreen.Keypad)
            {
                FrontText("Enter your friend's 6-character code",640,150,28,1160);
                FrontText(SpacedFriendCode(friendCode,true),640,223,64,1120).color=Hex(R25Theme.Teal);
                for(int i=0;i<R25FriendsFlow.Keys.Length;i++)
                {
                    string key=R25FriendsFlow.Keys.Substring(i,1);
                    FrontButton(key,108+(i%5)*216,290+(i/5)*86,200,76,delegate{FriendKey(key);},Hex(R25Theme.Raised),Hex(R25Theme.Text),40);
                }
                FriendButton("DEL",350,650,260,delegate{FriendKey("DEL");});
                FriendButton("CLEAR",650,650,280,delegate{FriendKey("CLEAR");});
                FrontButton("BACK",40,736,240,56,FriendsBack,Hex(R25Theme.Raised),Hex(R25Theme.Text),25);return;
            }
            if(screen==FriendsScreen.Waiting)
            {
                string caption=onlineUiState==OnlineRoomState.SigningIn?"Getting ready":onlineUiState==OnlineRoomState.CreatingRoom?"Making your room":"Joining";
                friendWaiting=FrontText(caption+"...",640,340,42,1160);
                FriendButton("CANCEL",400,590,480,FriendsBack);return;
            }
            if(screen==FriendsScreen.Error)
            {
                FrontText("COULD NOT CONNECT",640,220,34,1160).color=Hex(R25Theme.DangerText);
                string explanation=R25FriendsFlow.Error(onlineUiReason);
                FrontText(explanation,640,340,30,1150);
                if(explanation.Contains("not set up")||explanation.Contains("newest version"))
                {FriendButton("BACK",320,500,640,FriendsBack,true);return;}
                FriendButton("TRY AGAIN",320,500,640,delegate{RequestLanLeave();if(friendHost)MakeFriendRoom();else{friendsKeypad=true;friendCode="";Build();}},true);
                FriendButton("BACK",40,710,240,FriendsBack);return;
            }
            // The room code is known a frame before the lobby reports hosting/connected.
            BuildRoomPanel();
        }
        static string SpacedFriendCode(string code,bool slots)
        {
            string normalized=R25FriendsFlow.Normalize(code), result="";
            for(int i=0;i<(slots?6:normalized.Length);i++)result+=(i==0?"":"   ")+(i<normalized.Length?normalized[i].ToString():"_");
            return result;
        }
        void AnimateFriendsWaiting()
        {
            TickRoomBrowser();
            if(friendWaiting==null)return;
            string caption=onlineUiState==OnlineRoomState.SigningIn?"Getting ready":onlineUiState==OnlineRoomState.CreatingRoom?"Making your room":"Joining";
            friendWaiting.text=caption+new string('.',1+(int)(Time.unscaledTime*2)%3);
        }
    }
}
#endif
