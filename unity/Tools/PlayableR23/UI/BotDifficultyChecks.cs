using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Ashfall.Core;
using Ashfall.Integration.Playable.UI;
using Ashfall.Integration.Playable.Bots;
class BotDifficultyChecks
{
    static int checks;
    static void Need(bool value,string label){if(!value)throw new Exception(label);checks++;}
    static void Near(float value,float expected,string label){Need(Math.Abs(value-expected)<.00001f,label);}
    static void Main()
    {
        var legacy=R23UiSettingsCodec.Decode("r23.ui.v1;turning_style=snap;walk_speed_multiplier=3.75;sfx=0;dominant_hand=left");
        Near(legacy.BotDifficulty,.5f,"Legacy saved settings retain source moderate default");
        Need(legacy.Turning==R23Turning.Snap&&legacy.WalkMultiplier==3.75f&&legacy.SfxVolume==0&&legacy.DominantHand==0,"Legacy controls/zero SFX preserved");
        var culture=Thread.CurrentThread.CurrentCulture;
        try{
            Thread.CurrentThread.CurrentCulture=new CultureInfo("fr-FR");
            foreach(float difficulty in new[]{0f,.01f,.33f,.34f,.5f,.66f,.67f,1f}){
                var value=legacy.With(botDifficulty:difficulty);string encoded=R23UiSettingsCodec.Encode(value);
                Need(encoded.Length<=R23UiSettingsCodec.MaximumLength,"Bounded preference record");
                Need(R23UiSettingsCodec.SameValue(value,R23UiSettingsCodec.Decode(encoded)),"Difficulty and every existing setting restart roundtrip");
                Need(value.With(sfx:.2f).BotDifficulty==difficulty&&value.With(damping:1.6f).BotDifficulty==difficulty,"Unrelated setting edit preserves difficulty");
            }
        }finally{Thread.CurrentThread.CurrentCulture=culture;}
        foreach(string invalid in new[]{"NaN","Infinity","-Infinity","wrong"})Near(R23UiSettingsCodec.Decode("r23.ui.v1;difficulty="+invalid).BotDifficulty,.5f,"Invalid saved difficulty source fallback");
        Near(legacy.With(botDifficulty:-5).BotDifficulty,0,"Difficulty lower clamp");Near(legacy.With(botDifficulty:5).BotDifficulty,1,"Difficulty upper clamp");
        var position=new Float3(0,0,0);var approach=new Float3(0,0,-8);var brain=new R24KestrelBrain(0,0,position,approach,.5f,173);var twin=new R24KestrelBrain(0,0,position,approach,.5f,173);
        brain.TakeDamage(19,approach,new Float3(0,1.6f,-8));twin.TakeDamage(19,approach,new Float3(0,1.6f,-8));
        var fields=typeof(R24KestrelBrain).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);var saved=new Dictionary<FieldInfo,object>();foreach(var field in fields)saved.Add(field,field.GetValue(brain));
        brain.SetDifficulty(1);
        foreach(var pair in saved)if(pair.Key.Name!="<Difficulty>k__BackingField")Need(Object.Equals(pair.Value,pair.Key.GetValue(brain)),"Live difficulty edit retains "+pair.Key.Name);
        Near(brain.WindupSeconds,.56f,"Source challenging reaction");Near(brain.FixedDodgeSeconds,.28f,"Source challenging dodge window");Need(brain.DamageScale==3,"Source challenging damage threshold");
        brain.SetDifficulty(0);Near(brain.WindupSeconds,1.34f,"Source easier reaction");Need(brain.DamageScale==1,"Source easier damage");
        brain.SetDifficulty(.5f);Near(brain.WindupSeconds,1.04f,"Source moderate reaction");
        for(int i=0;i<200;i++){
            float a,b;Need(brain.SensorDue(.02f,out a)==twin.SensorDue(.02f,out b)&&a==b,"Difficulty changes do not consume source sensor RNG");
            var move=brain.Tick(.02f,position);var other=twin.Tick(.02f,position);Need(move.X==other.X&&move.Y==other.Y&&move.Z==other.Z&&brain.State==twin.State&&brain.Health==twin.Health,"Restored moderate brain exactly matches untouched seeded lifecycle");
        }
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){brain.SetDifficulty(invalid);Near(brain.Difficulty,.5f,"Source nonfinite difficulty fallback");}
        brain.TakeDamage(1000,approach,new Float3(0,1.6f,-8));brain.SetDifficulty(1);Need(brain.State==R24KestrelState.Dead&&brain.Health==0,"Difficulty update does not revive defeated enemy");
        Console.WriteLine("PASS "+checks+" bot-difficulty source/persistence/live-state/RNG checks; no Unity engine/HMD execution");
    }
}
