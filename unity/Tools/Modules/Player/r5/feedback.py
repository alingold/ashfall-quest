from pathlib import Path
p=Path(r'C:\Users\aling\Documents\ashfall\unity\Assets\Ashfall\Modules\Player\Engine\UnityExclusivePlayerMotor.cs');s=p.read_text();a=s.index('            bool complete=');b=s.index('\n        }',a);s=s[:a]+'''            PlayerMotorResult result=PlayerMotorFeedback.Resolve(p,F(applied),seconds,
                (flags&CollisionFlags.Below)!=0,(flags&CollisionFlags.Above)!=0);
            // A partially applied correction cannot authorize the pivot.
            if(result.Complete)root.Rotate(0,p.YawRadians*Mathf.Rad2Deg,0,Space.World);
            return result;'''+s[b:];p.write_text(s)
p=Path(r'C:\Users\aling\Documents\ashfall\unity\Tools\Modules\Player\r5\IntegrationR5Tests.cs');s=p.read_text().replace(' Console.WriteLine("PASS "+pass+', ''' var proposal=RootMotionSafety.Plan(new Float3(),new Float3(1,1,0),new Float3(0,0,.1f),(float)Math.PI/2,true);
 var partial=PlayerMotorFeedback.Resolve(proposal,new Float3(.01f,0,.02f),.02f,false,false);C(!partial.Complete,"partial move denies yaw");C(Near(partial.Velocity.X,.5f)&&Near(partial.Velocity.Z,1),"partial actual root momentum");
 var full=PlayerMotorFeedback.Resolve(proposal,new Float3(proposal.PivotCorrection.X,0,proposal.PivotCorrection.Z+.1f),.02f,false,false);C(full.Complete&&Near(full.Velocity.Z,5)&&Near(full.Velocity.X,0),"full pivot correction excluded from momentum");
 var ceiling=PlayerMotorFeedback.Resolve(new RootMotionProposal{Translation=new Float3(0,.1f,0)},new Float3(0,.01f,0),.02f,false,true);C(ceiling.Velocity.Y==0&&!ceiling.Complete,"ceiling authoritative clip");
 Console.WriteLine("PASS "+pass+''');p.write_text(s)
