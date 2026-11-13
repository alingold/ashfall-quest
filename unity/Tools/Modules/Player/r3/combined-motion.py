from pathlib import Path
p=Path('C:/Users/aling/Documents/ashfall/unity/Assets/Ashfall/Modules/Player/RootMotionSafety.cs');s=p.read_text().replace('''            if (!proposal.OwnsTranslation''','''            if (!new Float3(proposal.Translation.X + proposal.PivotCorrection.X,
                proposal.Translation.Y + proposal.PivotCorrection.Y, proposal.Translation.Z + proposal.PivotCorrection.Z).IsFinite)
                return RuleResult.Reject("Combined root motion overflow");
            if (!proposal.OwnsTranslation''');p.write_text(s)
p=Path('C:/Users/aling/Documents/ashfall/unity/Tools/Modules/Player/r3/R3Tests.cs');s=p.read_text().replace('''  Console.WriteLine("PASS''','''  var overflow=new RootMotionProposal{OwnsTranslation=true,Translation=new Float3(float.MaxValue,0,0),PivotCorrection=new Float3(float.MaxValue,0,0)};
  int calls=q.Calls;C(!RootMotionSafety.Approve(overflow,true,q).Accepted&&q.Calls==calls,"combined correction overflow rejects before geometry and mutation");
  foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}) {
   var sim=new Locomotion(PlayerTuning.SkinRuntime());var f=F();bool rejected=false;
   try{sim.Step(f,new MotionEnvironment{HeadingRadians=invalid},.02f);}catch(ArgumentException){rejected=true;}
   C(rejected&&sim.Step(f,new MotionEnvironment(),.02f).OwnsMotion,"invalid heading does not consume frame");
  }
  Console.WriteLine("PASS''');p.write_text(s)
