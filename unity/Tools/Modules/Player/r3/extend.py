from pathlib import Path
p=Path('C:/Users/aling/Documents/ashfall/unity/Tools/Modules/Player/r3/R3Tests.cs')
s=p.read_text().replace('  var desktop=new PlayerInput();','''  var changed=new PlayerInput();var held=new DeviceSample{IsXr=true,Focused=true,HeadTracked=true,Left=new HandSample{Tracked=true},Right=new HandSample{Tracked=true}};
  for(int i=0;i<4;i++)changed.Read(++tick,.02f,held);
  held.Jump=true;C(changed.Read(++tick,.02f,held).Jump,"setup ready right dominance");
  changed.Ownership.Dominant=DominantHand.Left;
  for(int i=0;i<6;i++)C(!changed.Read(++tick,.02f,held).Jump,"changing handedness held jump disarms new support");
  held.Jump=false;for(int i=0;i<4;i++)changed.Read(++tick,.02f,held);held.Jump=true;C(changed.Read(++tick,.02f,held).Jump,"new support neutral rearms");
  var gripOwner=new HandActionOwnership{Sprint=SprintBinding.LeftGrip};var physical=new DeviceSample{IsXr=true,Left=new HandSample{Grip=.7f}};
  C(HandActionAdapter.Bind(physical,gripOwner).Sprint,"left grip sprint binding explicit");
  gripOwner.Sprint=SprintBinding.LeftClick;C(!HandActionAdapter.Bind(physical,gripOwner).Sprint,"click binding does not infer grip sprint");
  var blocked=new Locomotion(PlayerTuning.SkinRuntime()).Step(F(),new MotionEnvironment{DesktopYawRadians=.2f,TurningBlocked=true,ExternalMotionOwner=true},.02f);
  C(!blocked.OwnsYaw&&blocked.YawRadians==0&&!blocked.OwnsMotion,"external turning block suppresses desktop yaw");
  var desktop=new PlayerInput();''')
p.write_text(s)
p=Path('C:/Users/aling/Documents/ashfall/unity/Assets/Ashfall/Modules/Player/Engine/UnityPlayerDriver.cs');s=p.read_text().replace('xrSessionFocused = false; DisarmPublished();','xrSessionFocused = false; DisarmPublished(UseXr);');p.write_text(s)
