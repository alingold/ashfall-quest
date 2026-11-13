using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
sealed class ProducerOpenR2 : IAuthorityGate,IHealthWorld,IInteractionQuery
{
    public double Now {get;set;}public bool InMatch {get{return true;}}public bool TeamChangesAllowed {get{return false;}}
    public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
    public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float r){return true;}
    public bool TryGetPose(string actor,out RescuePose pose){pose=new RescuePose();return false;}
    long serial;CommandContext C(string actor){return new CommandContext("s",actor,"open-"+(++serial),0,1,true);}
    static void Require(bool value){if(!value)throw new Exception("open-gate setup failed");}
    int Run()
    {
        var h=new HealthService("s",1,0,true,this,this,this);
        Require(h.Register(C("host"),"a",-1,false).Accepted);Require(h.Register(C("host"),"b",-1,false).Accepted);
        Require(h.ApplyLoss(C("host"),"a",h.Read("a").Revision,10,0,false).Accepted);
        Require(h.ApplyLoss(C("host"),"b",h.Read("b").Revision,100,100,false).Accepted);h.DrainTransitions();Now=5;
        var a=h.Read("a");var b=h.Read("b");HealthService.PreparedGain gain;HealthService.PreparedRespawn respawn;
        Require(h.PrepareGain(C("a"),"open-gain","a",a.Revision,a.LifeGeneration,a.ConnectionGeneration,10,0,out gain));
        bool prepared=h.PrepareRespawn(C("host"),"b",b.Revision,b.LifeGeneration,b.ConnectionGeneration,6,out respawn);
        if(!prepared){gain.Abort();Console.WriteLine("producer exclusion observed; new independent review still required");return 0;}
        return h.Serialize(delegate {
            bool both=gain.Validate()&&respawn.Validate();Console.WriteLine("OPEN legacy lifecycle Prepare accepted while global gain lease outstanding; bothValidate="+both);
            if(both){gain.CommitValidated();respawn.CommitValidated();var events=h.DrainTransitions();Console.WriteLine("actual transition sequences="+events[0].Sequence+","+events[1].Sequence);}
            gain.Abort();respawn.Abort();return 1;
        });
    }
    static int Main(){return new ProducerOpenR2().Run();}
}
