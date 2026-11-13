using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Modules.InventoryItems;
namespace Ashfall.Integration.Playable.Activities
{
    public struct R23TravelVectors
    {
        public readonly Float3 Forward,Direction;
        public R23TravelVectors(Float3 forward,Float3 direction){Forward=forward;Direction=direction;}
    }
    public static class R23TravelHeading
    {
        // Shared by walking/glide composition: retain analog magnitude and this frame's turn.
        public static R23TravelVectors Resolve(float heading,float turn,float x,float y)
        {
            if(!PlayerFinite(heading)||!PlayerFinite(turn)||!PlayerFinite(x)||!PlayerFinite(y))throw new ArgumentException("travel heading");
            double angle=((double)heading+turn)%(Math.PI*2);float sin=(float)Math.Sin(angle),cos=(float)Math.Cos(angle);
            return new R23TravelVectors(new Float3(sin,0,cos),new Float3(cos*x+sin*y,0,-sin*x+cos*y));
        }
        static bool PlayerFinite(float value){return !Single.IsNaN(value)&&!Single.IsInfinity(value);}
    }
    // Source eligibility is separate from geometry, charge and accepted placement.
    public static class R23BuildInputRules
    {
        public static bool Pressed(float trigger){return !Single.IsNaN(trigger)&&!Single.IsInfinity(trigger)&&trigger>.55f;}
        public static bool Eligible(bool inputEnabled,bool holstered,bool explosiveHeld,bool reloadAllowsBuilding,bool mantling,bool headClear)
        {return inputEnabled&&holstered&&!explosiveHeld&&reloadAllowsBuilding&&!mantling&&headClear;}
        public static bool HandFree(bool ready,bool tracked,bool occupied,bool objectiveOrReloadReserved,bool climbing)
        {return ready&&tracked&&!occupied&&!objectiveOrReloadReserved&&!climbing;}
    }
    // Tap advances once on release; a hold cancels once and never also taps.
    public sealed class R25BuildSelection
    {
        public int Piece {get;private set;}
        bool held,cancelled;float elapsed;
        public bool Step(bool active,bool button,float seconds,bool expanded)
        {
            if(!active){held=button;elapsed=0;cancelled=false;return false;}
            if(Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<0)return false;
            if(button){if(!held){elapsed=0;cancelled=false;}held=true;elapsed+=Math.Min(.1f,seconds);
                if(!cancelled&&elapsed>=.4f-.000001f){cancelled=true;return true;}}
            else {if(held&&!cancelled&&expanded)Piece=(Piece+1)%5;held=false;elapsed=0;cancelled=false;}
            return false;
        }
    }
    // Hold preview/release commit. Track fresh edges PER physical hand: a held
    // throw/use trigger must never become a fresh build press when custody frees it.
    public sealed class R23BuildGesture
    {
        readonly bool[] previous={true,true},armed=new bool[2];
        public int Hand {get;private set;} // 0 none, 1 left, 2 right
        public bool Active {get{return Hand!=0;}}
        public bool Commit {get;private set;}
        public void Cancel(){Hand=0;Commit=false;for(int i=0;i<2;i++){previous[i]=true;armed[i]=false;}}
        bool Edge(int index,bool pressed,bool free,bool cancel)
        {
            if(!free||cancel){previous[index]=pressed;armed[index]=false;return false;}
            bool fresh=armed[index]&&pressed&&!previous[index];
            if(!pressed)armed[index]=true;
            previous[index]=pressed;return fresh;
        }
        public void Step(bool leftTrigger,bool rightTrigger,bool leftFree,bool rightFree,bool cancelLeft,bool cancelRight,bool enabled,int preferredHand=2)
        {
            Commit=false;
            if(!enabled){Cancel();return;}
            if(Active)
            {
                bool free=Hand==1?leftFree:rightFree,cancel=Hand==1?cancelLeft:cancelRight,pressed=Hand==1?leftTrigger:rightTrigger;
                if(!free||cancel){Cancel();return;}
                bool release=!pressed&&previous[Hand-1];
                Edge(0,leftTrigger,leftFree,cancelLeft);Edge(1,rightTrigger,rightFree,cancelRight);
                if(release){Cancel();Commit=true;}return;
            }
            bool leftFresh=Edge(0,leftTrigger,leftFree,cancelLeft),rightFresh=Edge(1,rightTrigger,rightFree,cancelRight);
            if(preferredHand==1){if(leftFresh)Hand=1;else if(rightFresh)Hand=2;}
            else {if(rightFresh)Hand=2;else if(leftFresh)Hand=1;}
        }
    }
    // Local single-player source initial999 entitlement and the existing placement/debit owners.
    public sealed class R23ConstructionSession
    {
        sealed class Gate:IAuthorityGate,IBuildGrantAuthority
        {
            public string SupplyReceipt;public int SupplyAmount;
            public RuleResult Validate(CommandContext c){return c.IsAuthority&&c.SessionId=="r23-playable"&&c.ActorId=="player"&&c.AuthorityEpoch==0&&c.Tick>=0?RuleResult.Accept():RuleResult.Reject("local_scope");}
            public bool Validate(CommandContext c,string receipt,string kind,int amount){return Validate(c).Accepted&&(receipt=="r23-initial999"&&kind=="source-initial999"&&amount==999||kind=="builds"&&receipt==SupplyReceipt&&amount==SupplyAmount);}
        }
        readonly CandidateService service;readonly BuildChargeLedger charges;readonly Gate gate;long serial;
        internal bool ReconcileNetworkPreview(PlacedPanel[] panels){return service.ReconcileNetworkPreview(panels);}
        internal bool ReconcileNetworkBalance(int value){return charges.ReconcileNetworkBalance(value);}
        public int Balance {get{return charges.Balance;}}
        public PlacedPanel[] Snapshot(){return service.Snapshot();}
        CommandContext Context(long tick){return new CommandContext("r23-playable","player","build-"+(++serial),tick,0,true);}
        public R23ConstructionSession(IConstructionWorld world,bool infiniteBuilds=false)
        {
            gate=new Gate();service=new CandidateService("r23-playable",0,gate,world,infiniteBuilds);
            charges=new BuildChargeAccounts("r23-playable",0,gate,gate).ForActor("player");
            BuildChargeLedger.PreparedCharge seed;
            if(!charges.PrepareGrant(Context(0),"r23-initial999","source-initial999",999,0,out seed).Accepted||!seed.Validate(0))throw new InvalidOperationException("source_initial_charge");
            seed.CommitValidated();
        }
        public int GrantSupply(string receipt,int amount,long tick)
        {
            if(String.IsNullOrEmpty(receipt)||receipt.Length>256||amount<1||amount>6||tick<0||serial==long.MaxValue)return 0;
            gate.SupplyReceipt=receipt;gate.SupplyAmount=amount;
            try{BuildChargeLedger.PreparedCharge grant;if(!charges.PrepareGrant(Context(tick),receipt,"builds",amount,tick,out grant).Accepted)return 0;if(!grant.Validate(tick)){grant.Abort();return 0;}grant.CommitValidated();return amount;}
            finally{gate.SupplyReceipt=null;gate.SupplyAmount=0;}
        }
        public int WithdrawSupply(int requested,long tick)
        {
            int amount=Math.Min(6,Math.Min(requested,Balance));if(amount<1||tick<0||serial==long.MaxValue)return 0;
            BuildChargeLedger.PreparedCharge debit;if(!charges.PrepareSupplyWithdrawal(Context(tick),amount,tick,out debit).Accepted)return 0;
            if(!debit.Validate(tick)){debit.Abort();return 0;}debit.CommitValidated();return amount;
        }
        public RuleResult Evaluate(BuilderState pose,long tick,bool commit,out PlacedPanel[] panels,out int cost)
        {
            panels=new PlacedPanel[0];cost=0;
            if(tick<0||serial==long.MaxValue)return RuleResult.Reject("tick/serial");
            var context=Context(tick);CandidateService.PreparedChange plan;
            var result=service.PreparePlace(new ConstructionCommand(context,serial),pose,out plan);
            if(!result.Accepted)return result;
            panels=plan.Panels;cost=plan.Cost;
            BuildChargeLedger.PreparedCharge debit;
            result=charges.PrepareDebit(context,cost,tick,out debit);
            if(!result.Accepted){plan.Abort();return result;}
            if(!plan.Validate()||!debit.Validate(tick)){plan.Abort();debit.Abort();return RuleResult.Reject("placement_changed");}
            if(commit){plan.CommitValidated();debit.CommitValidated();}else{plan.Abort();debit.Abort();}
            return RuleResult.Accept();
        }
        public PlacedPanel DismantleTarget(BuilderState pose){return service.DismantleTarget("player",pose);}
        public RuleResult Dismantle(BuilderState pose,long tick)
        {
            CandidateService.PreparedChange plan;var context=Context(tick);
            var result=service.PrepareDismantle(new ConstructionCommand(context,serial),pose,out plan);if(!result.Accepted)return result;
            int refund=-plan.Cost;BuildChargeLedger.PreparedCharge grant=null;
            gate.SupplyReceipt="dismantle/"+serial;gate.SupplyAmount=refund;
            try{
                if(refund>0){result=charges.PrepareGrant(context,gate.SupplyReceipt,"builds",refund,tick,out grant);if(!result.Accepted){plan.Abort();return result;}}
                if(!plan.Validate()||grant!=null&&!grant.Validate(tick)){plan.Abort();if(grant!=null)grant.Abort();return RuleResult.Reject("dismantle_changed");}
                plan.CommitValidated();if(grant!=null)grant.CommitValidated();return RuleResult.Accept();
            }finally{gate.SupplyReceipt=null;gate.SupplyAmount=0;}
        }
        // CandidateService remains the only health/support-clock owner. Damage
        // and timed removals never touch the construction charge ledger.
        public RuleResult Damage(string id,float amount,long tick)
        {
            if(tick<0||serial==long.MaxValue)return RuleResult.Reject("tick/serial");
            CandidateService.PreparedChange change;
            var result=service.PrepareDamage(Context(tick),id,amount,service.Revision,out change);
            if(!result.Accepted)return result;
            if(!change.Validate()){change.Abort();return RuleResult.Reject("structure_changed");}
            change.CommitValidated();return RuleResult.Accept();
        }
        public RuleResult Advance(float seconds,bool paused)
        {
            CandidateService.PreparedChange change;var result=service.PrepareAdvance(seconds,paused,out change);
            if(!result.Accepted)return result;
            if(!change.Validate()){change.Abort();return RuleResult.Reject("structure_changed");}
            change.CommitValidated();return RuleResult.Accept();
        }
    }
}
