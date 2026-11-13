using System;
using Ashfall.Integration.Playable.Online;
namespace Ashfall.Integration.Playable.UI
{
    public enum FriendsScreen { Choice, Keypad, Waiting, Room, Error }
    // Pure presentation rules, shared by the real UI and CPU cases.
    public static class R25FriendsFlow
    {
        // Unity Relay join codes use only these characters (no vowels, no 0-5), so the
        // keypad never offers look-alikes such as O/0 or I/1.
        public const string Keys="BCDFGHJKLMNPQRTW6789";
        public static string Normalize(string value)
        {
            string result="";
            foreach(char c in (value??"").ToUpperInvariant())
            {
                if(Char.IsWhiteSpace(c))continue;
                if(Keys.IndexOf(c)<0 || result.Length==6)return "";
                result+=c;
            }
            return result;
        }
        public static string Edit(string code,string key)
        {
            code=Normalize(code);
            if(key=="CLEAR")return "";
            if(key=="DEL")return code.Length==0?"":code.Substring(0,code.Length-1);
            return code.Length<6 && key!=null && key.Length==1 && Keys.Contains(key)?code+key:code;
        }
        public static FriendsScreen Screen(OnlineRoomState state,bool keypad)
        {
            switch(state)
            {
                case OnlineRoomState.Failed:return FriendsScreen.Error;
                case OnlineRoomState.RoomCode:case OnlineRoomState.Connected:return FriendsScreen.Room;
                case OnlineRoomState.SigningIn:case OnlineRoomState.CreatingRoom:case OnlineRoomState.Joining:return FriendsScreen.Waiting;
                default:return keypad?FriendsScreen.Keypad:FriendsScreen.Choice;
            }
        }
        public static string Error(string reason)
        {
            string r=(reason??"").ToLowerInvariant();
            if(r.Contains("version")||r.Contains("protocol"))return "You and your friend have different versions of ASHFALL.\nBoth of you need the newest version.";
            if(r.Contains("not found"))return "That room code was not found.\nCheck the code with your friend.";
            if(r.Contains("full")||r.Contains("capacity"))return "The room is full.";
            if(r.Contains("internet")||r.Contains("network"))return "No internet connection.";
            if(r.Contains("not set up"))return "Online play is not set up in this build.";
            if(r.Contains("left")||r.Contains("disconnected"))return "Your friend left the room.";
            if(r.Contains("timed out"))return "Connecting took too long. Please try again.";
            return "We could not connect. Please try again.";
        }
    }
}
