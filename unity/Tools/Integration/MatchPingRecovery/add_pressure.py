import pathlib
p=pathlib.Path('unity/Tools/Integration/MatchPingRecovery/Tests.cs');t=p.read_text();pos=t.index('    static int Main')
extra=r'''
    static void Pressure()
    {
        var r=new Rig("respawn",0,10,100,2,8);r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");
        var owner=r.D.CreateRecoveryPingOwner(0,8192);var point=new Float3(0,0,0);var normal=new Float3(0,1,0);
        for(int i=0;i<8192;i++)Check(owner.Place(new CommandContext("test","b","pressure"+i,i,1,true),point,normal,0,-1).Accepted,"maximum actual private history "+i);
        Check(owner.Place(new CommandContext("test","b","overflow",8192,1,true),point,normal,0,-1).Reason=="journal_full","capacity never evicts replay");
        string path=PathFor("pressure");var store=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(store.Load(false).Accepted,"empty actual physical family");byte[] family=ReceiptFamilyArchive.Capture(path,new Ashfall.Modules.Persistence.PhysicalSettingsFiles());
        byte[] bytes=owner.CapturePartialArchive(AB(),family);var x=RestorePair(bytes,r.E,r.H);
        Check(!x.ProbePing(new CommandContext("test","b","pressure0",0,1,true),RoundStage.Match,point,normal,0,-1,0).Accepted,"oldest replay retained at capacity");
        Check(x.ProbePing(new CommandContext("test","b","overflow",8192,1,true),RoundStage.Match,point,normal,0,-1,0).Reason=="journal_full","cold saturation retained");
        foreach(var b in new[]{new Ashfall.RecoveryContracts.CheckpointBinding("build","catalog","test","match",1,1,0,100),new Ashfall.RecoveryContracts.CheckpointBinding("build","catalog","test","match",1,0,1,100),new Ashfall.RecoveryContracts.CheckpointBinding("build","catalog","test","match",1,0,0,101)})Refuse(()=>RestorePair(bytes,r.E,r.H,b),"mixed owner generation binding");
        byte[] trailing=new byte[bytes.Length+1];Array.Copy(bytes,trailing,bytes.Length);Refuse(()=>RestorePair(trailing,r.E,r.H),"trailing composite");
        using(var m=new System.IO.MemoryStream((byte[])bytes.Clone()))using(var rd=new System.IO.BinaryReader(m)){
            Ashfall.RecoveryContracts.RecoveryWire.Text(rd);AB().Check(rd);long time=m.Position;
            foreach(double value in new[]{double.NaN,double.PositiveInfinity,-1,1}){
                byte[] bad=(byte[])bytes.Clone();Array.Copy(BitConverter.GetBytes(value),0,bad,time,8);Refuse(()=>RestorePair(bad,r.E,r.H),"forged composite logical time");
            }
        }
        Console.WriteLine("PRESSURE imageBytes="+bytes.Length+" commands=8192");
    }
'''
t=t[:pos]+extra+t[pos:];t=t.replace('            bool gate=false;','            Pressure();\n            bool gate=false;');p.write_text(t)
