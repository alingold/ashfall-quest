from pathlib import Path
p=Path(r'C:\Users\aling\Documents\ashfall\unity\Tools\Modules\Player\r5\IntegrationR5Tests.cs');s=p.read_text();s=s.replace(' static InputFrame Fheld()', ''' static InputFrame Fheld()''')
s=s.replace(' var input=new PlayerInput();',''' foreach(var tuning in new[]{new PlayerTuning(),PlayerTuning.SkinRuntime()})
 {
  var heading=new MovementHeading();var motion=new Locomotion(tuning);
  foreach(int scenario in new[]{0,1,2,3,4})
  {
   bool ready=scenario!=3;Float3 hand=scenario==0?new Float3(1,0,0):scenario==1?new Float3(.01f,1,0):new Float3(-1,0,0);
   float angle=heading.Read(ForwardReference.Hand,true,ready,new Float3(0,0,1),hand,new Float3(0,1,0),0);
   var moved=motion.Step(F(),new MotionEnvironment{Grounded=true,IsXr=true,HeadingRadians=angle},.02f);
   C(scenario==3?moved.Velocity.Z>0:scenario==4?moved.Velocity.X<0:moved.Velocity.X>0,"heading to actual movement transition "+scenario);
  }
 }
 var input=new PlayerInput();''')
p.write_text(s)
