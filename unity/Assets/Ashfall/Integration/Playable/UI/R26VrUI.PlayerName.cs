#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Integration.Playable.UI
{
    // R26 (playtest 2026-11-15 PLAYER-NAMES): NAME row on SETTINGS / GENERAL and the
    // on-screen keyboard sub-page that edits it. Keys are 11.2 x 10 cm on the settings
    // board at 1.6 m (the slider rows are 6.5 cm tall), so a laser hits them easily.
    public sealed partial class R23VrUI
    {
        // Settings sub-pages: -1 root, 0 movement, 1 general, 2 weapons, 3 legacy, 4 name keyboard.
        const int GeneralSettingsPage=1,NameKeyboardSettingsPage=4;
        static readonly string[] KeyboardKeyRows={"1234567890","QWERTYUIOP","ASDFGHJKL-","ZXCVBNM_"};
        readonly List<KeyValuePair<Button,char>> nameLetterKeys=new List<KeyValuePair<Button,char>>();
        string nameDraft="";int nameShift;// 0 lower case, 1 next letter upper case, 2 caps lock
        TextMesh nameDisplay,nameHint;Button nameShiftKey,nameDone;
        void OpenNameKeyboard(){nameDraft=R26PlayerName.Current;nameShift=0;settingsPage=NameKeyboardSettingsPage;Build();}
        void CloseNameKeyboard(){settingsPage=GeneralSettingsPage;Build();}
        void SaveNameKeyboard(){if(R26PlayerName.Set(nameDraft))CloseNameKeyboard();}
        void BuildNameKeyboard()
        {
            title.text="ASHFALL / PLAYER NAME";nameLetterKeys.Clear();
            nameDisplay=Text(panel,"",new Vector3(0,.475f,-.025f),.058f,1.12f,Hex(R25Theme.Teal));
            nameHint=Text(panel,"",new Vector3(0,.40f,-.025f),.019f,1.12f,Hex(R25Theme.TextDim));
            BuildKeyboardCharacters((caption,column,row,action)=>NameKeyButton(caption,column,.29f-row*.115f,1,action),
                key=>TypeName(key.ToString()),(button,key)=>nameLetterKeys.Add(new KeyValuePair<Button,char>(button,key)));
            NameKeyButton("DEL",8.5f,-.055f,2,delegate{TypeName("DEL");});
            nameShiftKey=NameKeyButton("SHIFT",.5f,-.17f,2,delegate{nameShift=(nameShift+1)%3;RefreshNameKeyboard();});
            NameKeyButton("SPACE",4.5f,-.17f,6,delegate{TypeName("SPACE");});
            NameKeyButton("CLEAR",8.5f,-.17f,2,delegate{TypeName("CLEAR");});
            AddButton(panel,"CANCEL",-.30f,-.33f,.50f,.11f,CloseNameKeyboard,.032f);
            nameDone=AddButton(panel,"DONE",.30f,-.33f,.50f,.11f,SaveNameKeyboard,.032f);SetButtonStyle(nameDone,Hex(R25Theme.Primary),Hex(R25Theme.TextOnEmber));
            Text(panel,"Other players see this name in rooms, on scoreboards and above you.\nName change applies next room.",new Vector3(0,-.46f,-.025f),.019f,1.16f,Hex(R25Theme.TextDim));
            RefreshNameKeyboard();
        }
        void BuildKeyboardCharacters(Func<string,int,int,Action,Button> add,Action<char> type,Action<Button,char> registerLetter=null)
        {
            for(int row=0;row<KeyboardKeyRows.Length;row++)for(int column=0;column<KeyboardKeyRows[row].Length;column++)
            {
                char key=KeyboardKeyRows[row][column];var button=add(key.ToString(),column,row,delegate{type(key);});
                if(registerLetter!=null&&Char.IsLetter(key))registerLetter(button,key);
            }
        }
        // Ten 12 cm columns across the 1.3 m board; column is the key's centre slot.
        Button NameKeyButton(string caption,float column,float y,int slots,Action action)
        {return AddButton(panel,caption,-.54f+column*.12f,y,slots*.12f-.008f,.10f,action,caption.Length==1?.036f:.026f);}
        // Typing updates captions in place: no page rebuild, so the pointer keeps its key.
        void TypeName(string key)
        {
            bool letter=key.Length==1&&Char.IsLetter(key[0]);
            string next=R26PlayerNameRules.Edit(nameDraft,letter?(nameShift>0?key.ToUpperInvariant():key.ToLowerInvariant()):key);
            if(letter&&next!=nameDraft&&nameShift==1)nameShift=0;
            if(nameShift==0&&(next.Length==0||next[next.Length-1]==' '))nameShift=1;
            nameDraft=next;RefreshNameKeyboard();
        }
        void RefreshNameKeyboard()
        {
            if(nameDisplay==null)return;
            bool empty=nameDraft.Length==0,full=nameDraft.Length>=R26PlayerNameRules.MaximumLength,valid=R26PlayerNameRules.Clean(nameDraft).Length>0;
            nameDisplay.text=empty?"TYPE YOUR NAME":nameDraft+(full?"":"_");nameDisplay.color=Hex(empty?R25Theme.TextDisabled:R25Theme.Teal);
            nameHint.text=(valid?"Letters, numbers, spaces, - and _":"Use at least "+R26PlayerNameRules.MinimumLength+" characters")+"      "+nameDraft.Length+" / "+R26PlayerNameRules.MaximumLength;
            foreach(var key in nameLetterKeys)key.Key.Caption.text=nameShift>0?key.Value.ToString():Char.ToLowerInvariant(key.Value).ToString();
            nameShiftKey.Caption.text=nameShift==2?"CAPS":"SHIFT";SetButtonStyle(nameShiftKey,Hex(nameShift>0?R25Theme.RaisedSelected:R25Theme.Raised),Hex(R25Theme.Text));
            nameDone.Enabled=valid;SetButtonStyle(nameDone,Hex(valid?R25Theme.Primary:R25Theme.Disabled),Hex(valid?R25Theme.TextOnEmber:R25Theme.TextDisabled));
        }
    }
}
#endif
