#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        public bool SetBotDifficulty(float value)
        {
            Require();
            if(LanMapHostOnly||float.IsNaN(value)||float.IsInfinity(value))return false;
            Publish(Settings.With(botDifficulty:Mathf.Round(Mathf.Clamp01(value)*100)/100),true);
            return true;
        }
        string BotDifficultyCaption()
        {
            float value=Settings.BotDifficulty;
            string level=value<.34f?"EASIER":value>.66f?"CHALLENGING":"MODERATE";
            return "BOT DIFFICULTY    "+Mathf.RoundToInt(value*100)+"% | "+level;
        }
        void BuildBotDifficultyRow(int row)
        {
            bool available=!LanMapHostOnly;
            const string scope="Applies to local Relay Valley bots, including the current encounter. This setting is not shared with other players.";
            string help=available?scope:"The host controls this match menu. Bot difficulty is a local Relay Valley setting.";
            var caption=FrontText(BotDifficultyCaption(),130,258+row*43,21,800);
            caption.anchor=TextAnchor.MiddleLeft;caption.alignment=TextAlignment.Left;
            if(!available)caption.color=Hex(R25Theme.TextDisabled);
            var button=FrontButton("",810,235+row*43,345,32,null,Color.clear,Hex(R25Theme.Text),21);
            button.Enabled=available;button.Help=help;RegisterSettingsHelp(button.Surface,Vector3.zero,button.Size,help);
            float width=345*2.2f/1280;Transform fill,knob;BuildSettingsSlider(button,width,available,out fill,out knob);
            button.SliderChanged=delegate(float value){SetBotDifficulty(value);};
            numericRows.Add(new NumericRow{Caption=BotDifficultyCaption,Value=delegate{return Settings.BotDifficulty;},Min=0,Max=1,Text=caption,Fill=fill,Knob=knob,Width=width});
        }
    }
}
#endif
