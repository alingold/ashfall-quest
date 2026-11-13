using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.PersonalSettings;
class SettingsCompositionTests
{
 static void Main()
 {
  var input=new PlayerInput();var binding=new PlayerSettingsBinding(input);
  var values=new Dictionary<string,string>{{"dominant_hand","left"},{"sprint_binding","left_grip"},{"forward_reference","hand"},{"comfort_speed","true"},{"walk_speed_multiplier","2.5"},{"snap_degrees","30"},{"smooth_turn_speed","360"},{"smooth_analog","true"},{"smooth_ease_in","false"},{"turning_style","smooth"}};
  binding.Apply(new EffectiveSettings(values,SettingsSource.Quest));var bridge=new PlayerEffectiveSettingsPort(binding);var l=new Locomotion(binding.CreateTuning()){Settings=bridge};
  if(input.Ownership.Dominant!=DominantHand.Left||input.Ownership.Sprint!=SprintBinding.LeftGrip||bridge.ForwardReference!=ForwardReference.Hand)throw new Exception("binding ownership");
  var step=l.Step(new InputFrame{Tracked=true,Tick=1,Sequence=1,MoveY=1},new MotionEnvironment{Grounded=true},.02f);
  if(Math.Abs(step.Velocity.Z-6.5f)>.001)throw new Exception("actual comfort gain");
  binding.SetPresentationPaused(true);var f=binding.Read(2,.02f,new DeviceSample{Focused=true,MoveY=1});if(!f.Paused||f.MoveY!=0)throw new Exception("actual pause");
  Console.WriteLine("PASS 3 actual settings/Player composition checks");
 }
}
