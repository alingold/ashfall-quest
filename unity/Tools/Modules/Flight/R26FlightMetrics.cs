using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
sealed class R26FlightMetrics:IAuthorityGate
{
    public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
    static void Main(){new R26FlightMetrics().Run();}
    void Run()
    {
        Console.WriteLine("hz,pose,terminal,t90,speed1,speed2,speed3,speed5");
        foreach(int hz in new[]{72,90,120})foreach(bool dive in new[]{false,true})
        {
            var m=new FlightMachine("metrics","a",0,this,6.5f,1);float dt=1f/hz,terminal=dive?R26FreefallTuning.DiveTerminalSpeed:R26FreefallTuning.BellyTerminalSpeed;double t90=0;var speeds=new float[5];
            for(int i=1;i<=60*hz;i++)
            {
                var f=new FlightFrame{DeltaSeconds=dt,InputEnabled=true,Tracked=true,XR=true,HandsReady=true,Forward=new Float3(0,0,1),LeftHand=new Float3(-.2f,1.2f,0),RightHand=new Float3(.2f,1.2f,0),DiveAxis=dive?1:0,Velocity=m.Velocity};
                var r=m.Step(new CommandContext("metrics","a","f"+i,i,0,true),f);if(!r.Accepted)throw new Exception(r.Reason);
                float speed=-m.Velocity.Y;if(hz==90&&i%9==0&&i<=20*hz)Console.WriteLine("SAMPLE,"+(dive?"dive":"belly")+","+((double)i/hz).ToString("F1")+","+speed.ToString("F5"));if(t90==0&&speed>=.9f*terminal)t90=(double)i/hz;
                if(i%hz==0&&i<=5*hz)speeds[i/hz-1]=speed;
            }
            Console.WriteLine(hz+","+(dive?"dive":"belly")+","+terminal+","+t90.ToString("F4")+","+speeds[0].ToString("F4")+","+speeds[1].ToString("F4")+","+speeds[2].ToString("F4")+","+speeds[4].ToString("F4"));
        }
    }
}
