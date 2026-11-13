using System;
using Ashfall.Integration.Playable.UI;
class MapFlowChecks
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        var flow=new R23MapSelection();
        Check(!flow.BeginSpawn()&&flow.ConfirmSpawn(false)==null,"Empty options cannot enter");
        var options=new[]{new R23MapOption("valley","RELAY VALLEY"),new R23MapOption("range","SANDBOX RANGE")};
        flow.SetOptions(options);Check(flow.Selected.Id=="valley","Host first map is default");
        options[0]=new R23MapOption("other","OTHER");Check(flow.Option(0).Id=="valley","Options copied");
        Check(flow.Select("range")&&flow.Selected.Id=="range","Pending map selection");
        Check(!flow.ChoosingSpawn&&flow.ConfirmSpawn(true)==null,"Choosing map cannot request entry");
        Check(!flow.Select("missing")&&flow.Selected.Id=="range","Unknown map preserves selection");
        Check(flow.BeginSpawn(),"READY opens spawn choice");flow.CancelSpawn();
        Check(flow.ConfirmSpawn(true)==null&&flow.Selected.Id=="range","BACK cancels entry and keeps map");
        flow.BeginSpawn();var lobby=flow.ConfirmSpawn(false);
        Check(lobby!=null&&lobby.Id=="range"&&!lobby.GameSpawn,"Lobby spawn intent carries pending map");
        Check(flow.ConfirmSpawn(true)==null,"Repeated confirmation cannot double enter");
        flow.BeginSpawn();var game=flow.ConfirmSpawn(true);
        Check(game!=null&&game.Id=="range"&&game.GameSpawn,"Game spawn intent distinct");
        flow.BeginSpawn();flow.SetOptions(new[]{new R23MapOption("range","NEW LABEL"),new R23MapOption("cyber_bird","CYBER BIRD")});
        Check(flow.Selected.Label=="NEW LABEL"&&flow.ChoosingSpawn,"Option refresh preserves same pending ID");
        flow.SetOptions(new[]{new R23MapOption("cyber_bird","CYBER BIRD")});
        Check(flow.Selected.Id=="cyber_bird"&&!flow.ChoosingSpawn,"Removed selection cancels stale spawn confirmation");
        Check(flow.ConfirmSpawn(true)==null,"Removed map cannot enter from old dialog");
        flow.BeginSpawn();flow.Select("cyber_bird");Check(!flow.ChoosingSpawn,"Host synchronization requires fresh confirmation");
        flow.SetOptions(null);Check(flow.Count==0&&flow.Selected==null&&!flow.ChoosingSpawn,"Empty host catalog resets choice");
        bool rejected=false;try{flow.SetOptions(new[]{new R23MapOption("same","A"),new R23MapOption("same","B")});}catch(ArgumentException){rejected=true;}
        Check(rejected&&flow.Count==0,"Duplicate IDs rejected atomically");
        Check(new R23MapOption("valley","A").ThumbnailResource=="R23MapThumbnails/valley","Actual source thumbnail mapping");
        Check(new R23MapOption("archive:custom","A").ThumbnailResource==null,"No fake thumbnail for other maps");
        foreach(var field in typeof(R23MapEntry).GetFields())Check(field.IsInitOnly,"Immutable entry output");
        Console.WriteLine("PASS "+checks+" map-flow checks: pending selection, separate READY/spawn confirmation, cancel, duplicate intent guard, host refresh and exact source thumbnail IDs");
    }
}
