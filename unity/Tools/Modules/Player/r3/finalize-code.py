from pathlib import Path
p=Path('C:/Users/aling/Documents/ashfall/unity/Tools/Modules/Player/r3/run.py');s=p.read_text().replace("r/'unity/Tools/Modules/Player/PlayerTests.cs'", "o/'inputs/PlayerTests.cs'").replace("r/'unity/Tools/Modules/Player/R2Tests.cs'", "o/'inputs/R2Tests.cs'").replace("r/'campaign-staging/20261104-three-chats/unity/Player/r2/Adversarial.cs'", "o/'inputs/Adversarial.cs'")
s=s.replace('print(json.dumps(results,indent=2))', 'print("\\n".join(x["name"]+": compile="+str(x["compileExit"])+", test="+str(x.get("testExit"))+"; "+x.get("output", "").splitlines()[-1] for x in results))\nraise SystemExit(0 if all(x["compileExit"]==0 and x.get("testExit")==0 for x in results) else 1)');p.write_text(s)
p=Path('C:/Users/aling/Documents/ashfall/unity/Assets/Ashfall/Modules/Player/Engine/UnityPlayerDriver.cs');s=p.read_text().replace('''            CurrentFrame = Sample(++tick, dt, stalled);''','''            try { CurrentFrame = Sample(++tick, dt, stalled); }
            catch (System.ArgumentException) { DisarmPublished(); return; }''')
s=s.replace('''            RootMotionProposal proposal = RootMotionSafety.Plan(ToFloat(transform.position), ToFloat(Head.position), translation, yaw, step.OwnsMotion);
            Float3 correction = proposal.PivotCorrection;''','''            RootMotionProposal proposal;
            try { proposal = RootMotionSafety.Plan(ToFloat(transform.position), ToFloat(Head.position), translation, yaw, step.OwnsMotion); }
            catch (System.ArgumentException) { DisarmPublished(); return; }
            Float3 correction = proposal.PivotCorrection;''');p.write_text(s)
