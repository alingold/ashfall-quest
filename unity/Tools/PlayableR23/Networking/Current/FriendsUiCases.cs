using System;
using Ashfall.Integration.Playable.UI;
using Ashfall.Integration.Playable.Online;
partial class LobbyCases
{
    static void FriendsUiCases()
    {
        Need(R25FriendsFlow.Normalize(" b c d 6 7 8 ")=="BCD678","Friends normalize case and whitespace");
        foreach(string bad in new[]{"BCD6789","BC!678","BC?678","ABC678","BCD012"})Need(R25FriendsFlow.Normalize(bad)=="","Friends rejects malformed "+bad);
        Need(R25FriendsFlow.Keys.Length==20,"Keypad offers only the 20 Relay join-code characters");
        string code="";foreach(char key in "BCD678")code=R25FriendsFlow.Edit(code,key.ToString());
        Need(code=="BCD678" && R25FriendsFlow.Edit(code,"W")==code && R25FriendsFlow.Edit("","O")=="","Six keys complete and bounded; look-alikes refused");
        Need(R25FriendsFlow.Edit(code,"DEL")=="BCD67" && R25FriendsFlow.Edit(code,"CLEAR")=="","Delete and clear");
        Need(R25FriendsFlow.Edit("","DEL")=="","Delete empty");
        foreach(char key in R25FriendsFlow.Keys)Need(R25FriendsFlow.Edit("",key.ToString())==key.ToString(),"Every letter and digit "+key);
        foreach(OnlineRoomState state in Enum.GetValues(typeof(OnlineRoomState)))
        {
            FriendsScreen expected=state==OnlineRoomState.Idle?FriendsScreen.Choice:state==OnlineRoomState.Failed?FriendsScreen.Error:state==OnlineRoomState.RoomCode||state==OnlineRoomState.Connected?FriendsScreen.Room:FriendsScreen.Waiting;
            Need(R25FriendsFlow.Screen(state,false)==expected,"Screen for "+state);
        }
        Need(R25FriendsFlow.Screen(OnlineRoomState.Idle,true)==FriendsScreen.Keypad,"Idle code entry");
        foreach(string error in new[]{"That room code was not found","The room is full","No internet connection","The host left","Online play is not set up in this build","LAN connection rejected (version or protocol)"})
            Need(R25FriendsFlow.Error(error)!="We could not connect. Please try again.","Specific error "+error);
        Need(R25FriendsFlow.Error("LAN connection rejected (version or protocol)").Contains("newest version"),"Version rejection copy");
    }
}
