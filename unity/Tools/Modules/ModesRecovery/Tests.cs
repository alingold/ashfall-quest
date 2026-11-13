using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Modes;
using Ashfall.Modules.ModesRecovery;
using Ashfall.RecoveryContracts;

public sealed class PingRecoveryTests : IModeRoster, IAuthorityGate
{
    public Dictionary<string,ModeActor> Actors=new Dictionary<string,ModeActor>();
    public bool Deny;
    public bool TryGet(string id,out ModeActor a){return Actors.TryGetValue(id,out a);}
    public ModeActor[] Snapshot(){var a=new List<ModeActor>(Actors.Values);return a.ToArray();}
    public RuleResult Validate(CommandContext c){return Deny?RuleResult.Reject("test_gate"):RuleResult.Accept();}
    static int checks;
    static void Check(bool b,string label){checks++;if(!b)throw new Exception(label);}
    static CheckpointBinding Binding(){return new CheckpointBinding("frozen-modes-recovery-r1","synthetic-catalog","session","ping-scope",3,2,4,7);}
    static CommandContext C(string actor,string id,long tick){return new CommandContext("session",actor,id,tick,3,true);}
    static PingRecoveryTests Fixture(){var f=new PingRecoveryTests();f.Set("a",1,"alive",true,false);f.Set("b",1,"alive",true,false);f.Set("c",2,"alive",true,false);return f;}
    public void Set(string id,int team,string life,bool loaded,bool spectator){Actors[id]=new ModeActor(id,id,team,life,loaded,spectator);}
    static Float3 Point=new Float3(100,-20,345), Normal=new Float3(1,2,-3);
    static RecoverablePingService New(PingRecoveryTests f,int capacity,double cooldown,int teams){return new RecoverablePingService(f,f,"session",3,"map",teams,cooldown,capacity);}
    static void Same(PingService a,RecoverablePingService b,CommandContext c,RoundStage stage,Float3 point,Float3 normal,int color,int catalog,double time)
    {RuleResult x=a.Place(c,stage,point,normal,color,catalog,time),y=b.Place(c,stage,point,normal,color,catalog,time);Check(x.Accepted==y.Accepted&&x.Reason==y.Reason,"place differential: "+x.Reason+" / "+y.Reason);}
    static void Visible(PingService a,RecoverablePingService b,string viewer,string map,long epoch,double time)
    {
        PingRecord[] x=a.Visible(viewer,map,epoch,time);RecoverablePingRecord[] y=b.Visible(viewer,map,epoch,time);Check(x.Length==y.Length,"visible length");
        for(int i=0;i<x.Length;i++)Check(x[i].OwnerId==y[i].OwnerId&&x[i].MapId==y[i].MapId&&x[i].Epoch==y[i].Epoch&&x[i].Team==y[i].Team&&x[i].Color==y[i].Color&&x[i].Catalog==y[i].Catalog&&x[i].Action==y[i].Action&&x[i].Created==y[i].Created&&x[i].Until==y[i].Until&&V(x[i].Point,y[i].Point)&&V(x[i].Normal,y[i].Normal)&&x[i].SurfaceDecal==y[i].SurfaceDecal,"record exact");
    }
    static bool V(Float3 a,Float3 b){return a.X==b.X&&a.Y==b.Y&&a.Z==b.Z;}
    static RecoverablePingService Roundtrip(RecoverablePingService s,PingRecoveryTests f)
    {byte[] b=s.CaptureCheckpoint(Binding());var t=RecoverablePingService.RestoreCheckpoint(b,Binding(),RecoveryWire.Hash(b),f,f);Check(Convert.ToBase64String(b)==Convert.ToBase64String(t.CaptureCheckpoint(Binding())),"exact private state roundtrip");return t;}
    static void Differential()
    {
        for(int teams=0;teams<=2;teams++)for(int cap=1;cap<=32;cap*=2)
        {
            var f=Fixture();var a=new PingService(f,f,"session",3,"map",teams,2,cap);var b=New(f,cap,2,teams);
            // Directed success, warmup, cooldown, failed commands reusable, equal tick.
            Same(a,b,C("a","first",10),RoundStage.Lobby,Point,Normal,8,2,5);
            Same(a,b,C("a","cool",10),RoundStage.Countdown,Point,Normal,0,-1,6);
            b=Roundtrip(b,f);
            Same(a,b,C("a","cool",10),RoundStage.Match,Point,Normal,0,-1,7);
            Same(a,b,C("a","first",11),RoundStage.Match,Point,Normal,0,-1,100);
            Same(a,b,C("a","stale",9),RoundStage.Match,Point,Normal,0,-1,100);
            double[] times={Double.NaN,Double.PositiveInfinity,-1,0,4,5,7,35,37,37.0001,100};
            string[] ids={"a","b","c","missing"};var random=new Random(825+cap);
            for(int i=0;i<300;i++)
            {
                string actor=ids[random.Next(ids.Length)];string id="cmd"+random.Next(50);long tick=random.Next(100)-1;
                var c=new CommandContext(random.Next(8)==0?"wrong":"session",actor,id,tick,random.Next(8)==0?2:3,random.Next(8)!=0);
                f.Deny=random.Next(12)==0;
                Same(a,b,c,(RoundStage)random.Next(-1,8),random.Next(10)==0?new Float3(Single.NaN,0,0):Point,random.Next(10)==0?new Float3(0,0,0):Normal,random.Next(-1,10),random.Next(-2,32),times[random.Next(times.Length)]);
                foreach(string viewer in ids)Visible(a,b,viewer,i%7==0?"wrong":"map",i%9==0?4:3,times[random.Next(times.Length)]);
                if(i%25==0){b=Roundtrip(b,f);f.Set("b",random.Next(3),i%50==0?"downed":"alive",true,i%75==0);}
            }
            f.Deny=false;for(int i=0;i<50;i++)Same(a,b,C(ids[i%3],"fill"+i,100+i),RoundStage.Match,Point,Normal,i%9,i%30,200+i*3);
            b=Roundtrip(b,f);Visible(a,b,"a","map",3,400);
        }
    }
    static void Refuses(Action a,string label){bool threw=false;try{a();}catch(InvalidDataException){threw=true;}catch(EndOfStreamException){threw=true;}catch(ArgumentException){threw=true;}Check(threw,label);}
    static void Edges()
    {
        var f=Fixture();var a=new PingService(f,f,"session",3,"map",2,0,8192);var b=New(f,8192,0,2);
        for(int i=0;i<8192;i++)Same(a,b,C("a","pressure"+i,i),RoundStage.Countdown,Point,Normal,i%9,i%30,i);
        b=Roundtrip(b,f);Same(a,b,C("a","pressure0",9000),RoundStage.Match,Point,Normal,0,-1,9000);Same(a,b,C("a","new",9000),RoundStage.Match,Point,Normal,0,-1,9000);
        for(int teams=0;teams<=8;teams++)
        {
            f=Fixture();a=new PingService(f,f,"session",3,"map",teams,0,40);b=New(f,40,0,teams);
            for(int stage=-1;stage<=6;stage++)Same(a,b,C("a","stage"+stage,stage+1),(RoundStage)stage,Point,Normal,4,-1,10);
            foreach(double time in new double[]{9.999,10,40,40.0001})foreach(string viewer in new string[]{"a","b","c","missing"})Visible(a,b,viewer,"map",3,time);
            b=Roundtrip(b,f);
            f.Set("a",1,"dead",false,false);Visible(a,b,"b","map",3,20); // Source visibility does not require living/loaded owner.
            f.Set("a",2,"alive",true,false);Visible(a,b,"b","map",3,20);
            f.Set("a",1,"alive",true,true);Visible(a,b,"b","map",3,20);
            f.Actors.Remove("a");Visible(a,b,"b","map",3,20);
            f.Set("a",1,"alive",true,false);Visible(a,b,"b","map",3,20);
            foreach(int color in new int[]{-1,0,8,9})foreach(int cat in new int[]{-2,-1,0,29,30})Same(a,b,C("c","p"+color+"/"+cat,20),RoundStage.Lobby,Point,Normal,color,cat,20);
        }
    }
    static void Malformed()
    {
        var f=Fixture();var s=New(f,20,10,2);s.Place(C("a","old",99),RoundStage.Match,Point,Normal,4,-1,8);s.Place(C("b","second",50),RoundStage.Countdown,Point,Normal,8,29,12);
        byte[] b=s.CaptureCheckpoint(Binding());
        for(int i=0;i<b.Length;i++){byte[] cut=new byte[i];Array.Copy(b,cut,i);Refuses(delegate{RecoverablePingService.RestoreCheckpoint(cut,Binding(),RecoveryWire.Hash(cut),f,f);},"truncation "+i);}
        byte[] extra=new byte[b.Length+1];Array.Copy(b,extra,b.Length);Refuses(delegate{RecoverablePingService.RestoreCheckpoint(extra,Binding(),RecoveryWire.Hash(extra),f,f);},"trailing");
        Refuses(delegate{RecoverablePingService.RestoreCheckpoint(b,Binding(),"wrong",f,f);},"external digest");
        var wrong=new CheckpointBinding("different","synthetic-catalog","session","ping-scope",3,2,4,7);Refuses(delegate{RecoverablePingService.RestoreCheckpoint(b,wrong,RecoveryWire.Hash(b),f,f);},"build binding");
        wrong=new CheckpointBinding("frozen-modes-recovery-r1","synthetic-catalog","session","ping-scope",3,2,4,8);Refuses(delegate{RecoverablePingService.RestoreCheckpoint(b,wrong,RecoveryWire.Hash(b),f,f);},"freshness binding");
        // Mutate schema and string lengths with a matching external digest: parsing must reject.
        foreach(int offset in new int[]{0,4+RecoverablePingService.Codec.Length}){byte[] m=(byte[])b.Clone();m[offset]=255;Refuses(delegate{RecoverablePingService.RestoreCheckpoint(m,Binding(),RecoveryWire.Hash(m),f,f);},"codec mutation");}
        int mapOffset,teamsOffset,cooldownOffset,timeOffset,capacityOffset,seenCountOffset,tickCountOffset,markCountOffset;
        using(var r=new BinaryReader(new MemoryStream(b)))
        {
            RecoveryWire.Text(r);r.ReadInt32();Binding().Check(r);mapOffset=(int)r.BaseStream.Position;RecoveryWire.Text(r);teamsOffset=(int)r.BaseStream.Position;r.ReadInt32();cooldownOffset=(int)r.BaseStream.Position;r.ReadDouble();timeOffset=(int)r.BaseStream.Position;r.ReadDouble();capacityOffset=(int)r.BaseStream.Position;r.ReadInt32();seenCountOffset=(int)r.BaseStream.Position;int n=r.ReadInt32();for(int i=0;i<n;i++)RecoveryWire.Text(r);tickCountOffset=(int)r.BaseStream.Position;n=r.ReadInt32();for(int i=0;i<n;i++){RecoveryWire.Text(r);r.ReadInt64();}markCountOffset=(int)r.BaseStream.Position;
        }
        foreach(int offset in new int[]{mapOffset,teamsOffset,capacityOffset,seenCountOffset,tickCountOffset,markCountOffset})
        {byte[] m=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(Int32.MaxValue),0,m,offset,4);Refuses(delegate{RecoverablePingService.RestoreCheckpoint(m,Binding(),RecoveryWire.Hash(m),f,f);},"count/config range mutation");}
        foreach(int offset in new int[]{cooldownOffset,timeOffset})
        {byte[] m=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(Double.NaN),0,m,offset,8);Refuses(delegate{RecoverablePingService.RestoreCheckpoint(m,Binding(),RecoveryWire.Hash(m),f,f);},"nonfinite private state");}
        {byte[] m=(byte[])b.Clone();m[mapOffset+4]=255;Refuses(delegate{RecoverablePingService.RestoreCheckpoint(m,Binding(),RecoveryWire.Hash(m),f,f);},"invalid UTF8");}
        // Duplicate accepted command IDs: equal-length original IDs retain layout.
        {var duplicate=New(f,20,0,2);duplicate.Place(C("a","one",1),RoundStage.Match,Point,Normal,0,-1,1);duplicate.Place(C("a","two",2),RoundStage.Match,Point,Normal,0,-1,2);byte[] m=duplicate.CaptureCheckpoint(Binding());byte[] needle=System.Text.Encoding.UTF8.GetBytes("1:atwo"),replacement=System.Text.Encoding.UTF8.GetBytes("1:aone");for(int i=0;i<=m.Length-needle.Length;i++){bool match=true;for(int j=0;j<needle.Length;j++)match&=m[i+j]==needle[j];if(match)Array.Copy(replacement,0,m,i,replacement.Length);}Refuses(delegate{RecoverablePingService.RestoreCheckpoint(m,Binding(),RecoveryWire.Hash(m),f,f);},"duplicate replay entries");}
        var restored=Roundtrip(s,f);Check(!restored.Place(C("a","old",100),RoundStage.Match,Point,Normal,0,-1,50).Accepted,"replay survives");Check(restored.Place(C("a","new",100),RoundStage.Match,Point,Normal,0,-1,17).Reason=="cooldown","hidden cooldown survives");Check(restored.Place(C("b","new",49),RoundStage.Match,Point,Normal,0,-1,50).Reason=="stale_tick","tick survives");Check(restored.Place(C("b","new",51),RoundStage.Match,Point,Normal,0,-1,11).Reason=="time","global time survives");
        Check(restored.Visible("a","map",3,1000).Length==0,"expired invisible");Check(restored.CaptureCheckpoint(Binding()).Length==b.Length,"expired retained");
        Refuses(delegate{New(f,8193,0,0).CaptureCheckpoint(Binding());},"unsupported original capacity explicit refusal");
    }
    public static int Main(string[] args)
    {
        try
        {
            var f=Fixture();
            if(args.Length>0&&args[0]=="write")
            {var s=New(f,4,10,2);s.Place(C("a","old",99),RoundStage.Lobby,Point,Normal,4,-1,8);s.Place(C("b","other",10),RoundStage.Match,Point,Normal,8,29,12);byte[] b=s.CaptureCheckpoint(Binding());File.WriteAllBytes(args[1],b);File.WriteAllText(args[1]+".digest",RecoveryWire.Hash(b));Console.WriteLine("cold capture");return 0;}
            if(args.Length>0&&args[0]=="read")
            {byte[] b=File.ReadAllBytes(args[1]);var s=RecoverablePingService.RestoreCheckpoint(b,Binding(),File.ReadAllText(args[1]+".digest"),f,f);Check(s.Place(C("a","old",100),RoundStage.Match,Point,Normal,0,-1,100).Reason=="replay","cold replay");Check(s.Place(C("a","next",100),RoundStage.Match,Point,Normal,0,-1,17).Reason=="cooldown","cold cooldown");Check(s.Place(C("b","next",9),RoundStage.Match,Point,Normal,0,-1,20).Reason=="stale_tick","cold watermark");Check(s.Visible("a","map",3,12).Length==2,"cold marks");Check(Convert.ToBase64String(b)==Convert.ToBase64String(s.CaptureCheckpoint(Binding())),"cold full byte identity");Console.WriteLine("cold PASS "+checks);return 0;}
            if(args.Length>0&&args[0]=="guard"){Refuses(delegate{New(f,5,0,2).CaptureCheckpoint(Binding());},"new private field guard");Console.WriteLine("guard PASS");return 0;}
            Differential();Edges();Malformed();Console.WriteLine("PASS "+checks);return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
