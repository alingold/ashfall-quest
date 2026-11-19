#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Integration.Playable.World;
using UnityEngine;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        string mapSearch="";
        bool mapKeyboardOpen;
        void ResetMapSearch(){mapSearch="";mapKeyboardOpen=false;mapPage=0;}
        void EditMapSearch(string key)
        {
            if(key=="CLEAR")mapSearch="";
            else if(key=="DEL"){if(mapSearch.Length==0)return;mapSearch=mapSearch.Substring(0,mapSearch.Length-1);}
            else{if(mapSearch.Length>=48)return;mapSearch+=key;}
            mapPage=0;Build();
        }
        void ToggleMapKeyboard(){mapKeyboardOpen=!mapKeyboardOpen;mapPage=0;Build();}
        void BuildMapSearchPicker()
        {
            var results=new List<R23MapOption>();string query=mapSearch.Trim();
            for(int index=0;index<mapSelection.Count;index++)
            {
                var choice=mapSelection.Option(index);
                if(query.Length==0||choice.Label.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||R24ArchiveCatalog.Creator(choice.Id).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)results.Add(choice);
            }
            int pageSize=mapKeyboardOpen?2:4,pages=Math.Max(1,(results.Count+pageSize-1)/pageSize);
            mapPage=Math.Min(mapPage,pages-1);
            title.text="";var heading=FrontText("SELECT MAP  "+(mapPage+1)+" / "+pages,42,42,28,1196);
            heading.anchor=TextAnchor.MiddleLeft;heading.alignment=TextAlignment.Left;
            if(LanMapHostOnly)FrontText("The host opens the shared map.",450,42,20,370).color=Hex(R25Theme.Teal);
            MapPickerButton(mapKeyboardOpen?"HIDE KEYS":query.Length==0?"SEARCH":"EDIT SEARCH",1014,31,216,59,ToggleMapKeyboard,22);
            if(mapKeyboardOpen||mapSearch.Length>0)
            {
                var queryText=FrontText(mapSearch.Length==0?"Search by map name or creator":mapSearch,436,105,24,754);
                queryText.richText=false;queryText.color=Hex(mapSearch.Length==0?R25Theme.TextDim:R25Theme.Teal);
                StyleAction(MapPickerButton("CLEAR",1070,98,160,44,delegate{EditMapSearch("CLEAR");},19),false,mapSearch.Length>0);
            }
            if(results.Count==0)FrontText("NO MAPS FOUND",mapKeyboardOpen?310:640,384,27,520);
            for(int slot=0;slot<pageSize&&mapPage*pageSize+slot<results.Count;slot++)
            {
                var choice=results[mapPage*pageSize+slot];
                float x=mapKeyboardOpen?90:90+(slot%2)*590,y=145+(mapKeyboardOpen?slot:slot/2)*270;
                MapThumbnail(choice,x,y);
                var card=FrontButton(Clean(choice.Label,48),x,y+180,440,60,delegate{ChooseMap(choice.Id);},Hex(choice.Id==SelectedMapId?R25Theme.RaisedSelected:R25Theme.Raised),Hex(R25Theme.Text),23);
                card.Frames=ThemeFrameAt(panel,FrontPoint(x+220,y+85),new Vector2(440*R25Theme.MetresPerPixel,170*R25Theme.MetresPerPixel));SetHovered(card,false);
                if(LanMapHostOnly)StyleAction(card,false,false);
                string creator=R24ArchiveCatalog.Creator(choice.Id);
                if(creator.Length>0){var by=FrontText(creator,x+220,y+254,17,440);by.richText=false;by.color=Hex(R25Theme.TextDim);}
            }
            if(mapKeyboardOpen)BuildMapSearchKeyboard();
            StyleAction(MapPickerButton("PREVIOUS",390,710,220,58,delegate{mapPage--;Build();},22),false,mapPage>0);
            StyleAction(MapPickerButton("NEXT",670,710,220,58,delegate{mapPage++;Build();},22),false,mapPage+1<pages);
            MapPickerButton("BACK",40,720,170,58,delegate{SetPage(returnPage);},22);
        }
        void BuildMapSearchKeyboard()
        {
            // Same key rows as the player name keyboard, with punctuation for creator names.
            BuildKeyboardCharacters((caption,column,row,action)=>MapPickerButton(caption,640+column*59,244+row*64,54,58,action,25),
                key=>EditMapSearch(Char.ToLowerInvariant(key).ToString()));
            MapPickerButton(".",1112,436,54,58,delegate{EditMapSearch(".");},25);
            MapPickerButton("'",1171,436,54,58,delegate{EditMapSearch("'");},25);
            MapPickerButton("SPACE",640,500,349,58,delegate{EditMapSearch(" ");},23);
            MapPickerButton("DEL",994,500,231,58,delegate{EditMapSearch("DEL");},23);
        }
        Button MapPickerButton(string caption,float x,float y,float width,float height,Action action,int size)
        {return FrontButton(caption,x,y,width,height,action,Hex(R25Theme.Raised),Hex(R25Theme.Text),size);}
    }
}
#endif
