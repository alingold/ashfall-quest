using System;
using System.IO;
using System.Globalization;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.TeamLaunch;
using Ashfall.Integration.Playable.MapStyles;
partial class LobbyCases
{
    static float PlayfixY(string json)
    {
        return Single.Parse(R25StyleJson.Array(json)[1], CultureInfo.InvariantCulture);
    }
    static void PlayfixCases()
    {
        int tested=0, oldFailures=0;bool melipop=false;
        foreach(string path in Directory.GetFiles(SolidPayloadRoot(), "map.json", SearchOption.AllDirectories))
        {
            var row=R25StyleJson.Object(File.ReadAllText(path));
            if(!row.ContainsKey("boundsMin"))continue;
            float bottom=PlayfixY(row["boundsMin"]);
            foreach(string key in new[]{"spawn","gameSpawn","warmupSpawn","recordedSpawn"})
            {
                if(!row.ContainsKey(key)||row[key]=="null")continue;
                float y=PlayfixY(row[key]),level=R25OfflineFallPolicy.Level(bottom,y);
                Need(y>=level+11.99f,"Entry survives offline plane: "+path+" "+key);
                tested++;
                if(y<bottom-12)oldFailures++;
                if(row["mapId"].Contains("melipop-gods-poplightning")&&key=="warmupSpawn")
                {
                    Need(Math.Abs(y-5.120104f)<.001f&&Math.Abs(bottom-158.449219f)<.001f,"Exact real Melipop inputs");
                    Need(y<bottom-12&&Math.Abs(level+6.879896f)<.001f,"Reproduces old immediate death and corrected plane");var before=new R23RoundState(new string[0],new ReceiptPorts(),true);
                    var after=new R23RoundState(new string[0],new ReceiptPorts(),true);
                    for(int tick=0;tick<144;tick++)
                    {
                        if(y<bottom-12)before.Damage(1000);
                        if(y<R25OfflineFallPolicy.Level(bottom,y))after.Damage(1000);
                    }
                    Need(before.Health==0&&after.Health==100&&after.Shield==100,"Two seconds stationary: actual offline Health dies before, survives after");
                    after.Damage(1000);Need(after.Health==0,"Corrected entry still permits lethal ordinary damage");melipop=true;
                }
            }
        }
        Need(melipop&&tested>60&&oldFailures>0,"Real shipped-map scan includes reproduced Melipop");
        Console.WriteLine("P2 shipped entry tests="+tested+" old lethal entries="+oldFailures);
        Need(R25OfflineFallPolicy.Level(0,2)==-12,"Ordinary arena plane unchanged");
        Need(R25OfflineFallPolicy.Level(100,5)==-7 && -7.01f<R25OfflineFallPolicy.Level(100,5),"Below-room fall remains lethal");
        Need(R25OfflineFallPolicy.Level(17,19)-R25OfflineFallPolicy.Level(0,2)==17,"World map translation preserved");
        int frames=0;
        foreach(float x in new[]{-2f,-.5f,0f,.5f,2f})
        foreach(float z in new[]{-2f,-.5f,0f,.5f,2f})
        foreach(float height in new[]{.7f,1.1f,1.7f,2.2f})
        foreach(int primary in new[]{0,1})
        {
            var runtime=new R24TeamLaunchRuntime(1);
            runtime.Configure(new R24LaunchMap{Id="archive:alpha",Minimum=new Float3(-100,0,-100),Maximum=new Float3(100,0,100),Top=20,OriginY=0},new R24LaunchRules{Live=true,Mode="revive",Stage="match",Teams=2,Epoch=9});
            var plan=new CapsulePlan("9:0:0",0,0,new Float3(0,70,0),new Float3(0,75,10),new Float3(10,75,10),new Float3(10,0,10),new Float3(10,0,8),new Float3(0,0,-1),4);
            runtime.AdoptHostRide("player",plan,2,0,plan.Start);
            var player=new R24LaunchPlayer{Id="player",Team=0,Alive=true,Position=plan.Start};
            for(int frame=0;frame<3;frame++)
            {
                // Beginning/middle/end of flight; physical lean and crouch also vary.
                double clock=2+frame*1.9;var at=plan.Position(clock-2);
                var head=new Float3(at.X+x+frame*.1f,at.Y+height-frame*.05f,at.Z+z);
                var forward=frame==0?new Float3(0,1,1):frame==1?new Float3(1,0,0):new Float3(0,-1,-1);
                var control=R24TeamLaunchRuntime.RiderControl(head,forward);var delta=R24LaunchMath.Sub(control,head);
                Need(Math.Abs(delta.Y+.32f)<.0001f&&Math.Abs(R24LaunchMath.Length(delta)-.51225f)<.001f,"Comfortable head-relative reach, either handedness");
                var input=new R24LaunchInput{Enabled=true,XR=true,HeadTracked=true,Head=head,HeadForward=forward};
                runtime.SampleInput(player,clock,input); // release edge before either hand
                input.Enabled=false;input.Head=new Float3(head.X+.1f,head.Y-.1f,head.Z);
                Need(runtime.SampleInput(player,clock,input).Kind==R24LaunchIntentKind.None && runtime.ReadRide("player").Control.Equals(R24TeamLaunchRuntime.RiderControl(input.Head,forward)),"Focus/disabled input still tracks control without ejecting");
                input.Enabled=true;input.Head=head;runtime.SampleInput(player,clock,input); // rearm after focus returns
                var hand=new R24LaunchHand{Tracked=true,Position=control,Forward=forward,Trigger=1};
                if(primary==0)input.Left=hand;else input.Right=hand;
                Need(runtime.SampleInput(player,clock,input).Kind==R24LaunchIntentKind.Eject,"Either primary hand reaches eject at every ride sample");
                Need(runtime.ReadRide("player").Control.Equals(control),"Presenter and input share exact control point");frames++;
            }
            Need(Object.ReferenceEquals(runtime.ReadRide("player").Plan,plan),"Placement never changes flight plan");
        }
        Console.WriteLine("P2 head-relative control samples="+frames);
    }
}
