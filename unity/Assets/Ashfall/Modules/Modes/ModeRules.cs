using System;

namespace Ashfall.Modules.Modes
{
    public sealed class ModeRules
    {
        public readonly string Mode;
        public readonly int Teams, ScoreLimit, TimeLimit, Rounds;
        public readonly bool Catalog, InfiniteAmmo, InfiniteBuilds, ZoneEnabled;
        public readonly float Damage;
        public ModeRules(string mode, int teams, int scoreLimit, int timeLimit, int rounds)
            : this(mode,teams,scoreLimit,timeLimit,rounds,false,false,false,true,1f) { }
        private ModeRules(string mode,int teams,int score,int time,int rounds,bool catalog,bool ammo,bool builds,bool zone,float damage)
        {
            Mode=mode=="respawn" ? "respawn" : "revive";
            Teams=Math.Max(0,Math.Min(8,teams)); ScoreLimit=Math.Max(0,Math.Min(500,score));
            TimeLimit=Math.Max(0,Math.Min(3600,time)); Rounds=Math.Max(1,Math.Min(10,rounds));
            Catalog=catalog; InfiniteAmmo=ammo; InfiniteBuilds=builds; ZoneEnabled=zone; Damage=damage;
        }
        public ModeRules WithLimits(int score,int time)
        { return new ModeRules(Mode,Teams,score,time,Rounds,Catalog,InfiniteAmmo,InfiniteBuilds,ZoneEnabled,Damage); }
        public static ModeRules Default { get { return new ModeRules("revive",2,25,600,1); } }
        public static ModeRules Preset(string id)
        {
            if(id=="practice") return new ModeRules("respawn",2,0,0,1,true,true,true,false,0);
            if(id=="team_respawn") return new ModeRules("respawn",2,25,600,1);
            if(id=="free_for_all") return new ModeRules("respawn",0,25,600,1);
            return Default;
        }
        public bool CanRespawn { get { return Mode=="respawn"; } }
        public bool CanDown { get { return Mode=="revive" && Teams>0; } }
    }
}
