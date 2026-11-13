using System;using System.IO;using System.Text;using Ashfall.Modules.Persistence;using Ashfall.Modules.SaveMigration;using Ashfall.Integration.MigrationRuntime;
class ReconciliationTests {
 static string root;static int count;static string P(string n){return Path.Combine(root,n);}
 static void C(bool b,string s){if(!b)throw new Exception(s);Console.WriteLine("PASS "+(++count)+" "+s);}
 sealed class Port:IAtomicSettingsFiles {
  readonly PhysicalSettingsFiles real=new PhysicalSettingsFiles();public int Cut,Calls,Mode;public bool BadRead;public int Reads; public string Target;
  void Hit(){if(++Calls==Cut)throw new IOException("cut");}
  public IDisposable AcquireFamily(string p){Hit();return real.AcquireFamily(p);}
  public bool Exists(string p){Hit();return real.Exists(p);}
  public byte[] Read(string p,int n){Hit();if(BadRead)throw new IOException("read failed");var b=real.Read(p,n);if(Mode==5&&p==Target&&++Reads==2){File.WriteAllBytes(p,new byte[]{7});}return b;}
  public void CreateDurable(string p,byte[] b){Hit();if(Mode==1){real.CreateDurable(p,new byte[]{0});throw new IOException("partial");}real.CreateDurable(p,b);if(Mode==2)throw new IOException("pending complete");}
  public void Commit(string p,string d,string b,bool r){Hit();real.Commit(p,d,b,r);if(Mode==3)throw new IOException("published");}
 }
 static SettingsMigrationRuntime R(string n,Port port){return new SettingsMigrationRuntime(P(n),SourceProfile.Quest,port,10000);}
 static void Main(string[] args){root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
  if(args.Length>1){
   var cold=R("baseline",new Port());var cp=cold.PreviewLobby(P("source"),null);var cc=cold.Commit(cp);
   C(cc.State==CommitState.Conflict,"cold process cannot overwrite committed family");
   C(cold.Reconcile(cp,cc.Receipt).State==PublicationState.Unresolved,"cold conflict receipt cannot manufacture publication history");
   var read=SemanticMigration.ReadCommitted(cold.Destination,"lobby",false);C(read.State==MigrationState.Ready&&read.SemanticJson==cp.SemanticJson,"cold actual semantic readback");
   var store=new AtomicSettingsStore(cold.Destination);string archive;C(store.Load(false).Accepted&&store.TryRead("semantic.originals.base64",out archive)&&archive.Contains(Convert.ToBase64String(File.ReadAllBytes(P("source")))),"cold exact original archive");return;
  }
  File.WriteAllText(P("source"),"{\"walk\":4,\"unknown\":1.23000000000000000000001}",new UTF8Encoding(true));string original=Convert.ToBase64String(File.ReadAllBytes(P("source")));
  var trace=new Port();var baseline=R("baseline",trace);var bp=baseline.PreviewLobby(P("source"),null);var br=baseline.Commit(bp);int total=trace.Calls;
  C(br.State==CommitState.Committed,"legacy success");C(baseline.Reconcile(bp,br.Receipt).State==PublicationState.CommittedReadbackVerified,"success actual readback");
  for(int i=1;i<=total;i++){
   var port=new Port{Cut=i};var r=R("cut"+i,port);var p=r.PreviewLobby(P("source"),null);var c=r.Commit(p);C(c.State==CommitState.IoFailure,"cut classification "+i);port.Cut=0;
   C(r.Reconcile(p,c.Receipt).State==(c.Receipt.PublicationAttempted?PublicationState.Unresolved:PublicationState.ValidatedNotPublished),"cut disposition "+i);
   C(!File.Exists(r.Destination)&&!File.Exists(r.Destination+".backup"),"cut protected absence "+i);
   var retry=r.Commit(p);C(retry.State==CommitState.Committed&&r.Reconcile(p,retry.Receipt).State==PublicationState.CommittedReadbackVerified,"retry exact readback "+i);
  }
  for(int m=1;m<=3;m++){
   var port=new Port{Mode=m};var r=R("partial"+m,port);var p=r.PreviewLobby(P("source"),null);var c=r.Commit(p);C(c.State==CommitState.IoFailure,"partial IO "+m);port.Mode=0;
   C(r.Reconcile(p,c.Receipt).State==(m==3?PublicationState.CommittedReadbackVerified:PublicationState.ValidatedNotPublished),"partial actual disposition "+m);
   C(Convert.ToBase64String(p.ReadOriginal(P("source")))==original,"immutable original "+m);
   if(m==3){
    var bytes=File.ReadAllBytes(r.Destination);port.BadRead=true;C(r.Reconcile(p,c.Receipt).State==PublicationState.Unresolved,"read failure unresolved");port.BadRead=false;
    using(new PhysicalSettingsFiles().AcquireFamily(r.Destination))C(r.Reconcile(p,c.Receipt).State==PublicationState.Unresolved,"contended family unresolved");
    File.WriteAllBytes(r.Destination+".backup",new byte[]{1});C(r.Reconcile(p,c.Receipt).State==PublicationState.Unresolved,"backup tamper unresolved");File.Delete(r.Destination+".backup");
    File.WriteAllBytes(r.Destination,new byte[]{2});C(r.Reconcile(p,c.Receipt).State==PublicationState.Unresolved,"primary tamper unresolved");File.WriteAllBytes(r.Destination,bytes);
    File.AppendAllText(r.Destination,"\n",new UTF8Encoding(false));C(r.Reconcile(p,c.Receipt).State==PublicationState.Unresolved,"valid store with changed exact bytes unresolved");File.WriteAllBytes(r.Destination,bytes);
    port.Target=r.Destination;port.Mode=5;port.Reads=0;C(r.Reconcile(p,c.Receipt).State==PublicationState.Unresolved,"mid-read tamper unresolved");port.Mode=0;File.WriteAllBytes(r.Destination,bytes);
    C(r.Commit(p).State==CommitState.Conflict,"publish retry conflicts");C(r.Reconcile(p,c.Receipt).State==PublicationState.CommittedReadbackVerified,"retained receipt repeat reconciliation");
    File.Delete(r.Destination);C(r.Reconcile(p,c.Receipt).State==PublicationState.Unresolved,"publication then disappearance never rollback");
   }
  }
  var foreign=R("baseline",new Port());var fp=foreign.PreviewLobby(P("source"),null);
  C(foreign.Reconcile(fp,br.Receipt).State==PublicationState.Unresolved,"foreign receipt refuses");
  C(baseline.Reconcile(baseline.PreviewLobby(P("source"),null),br.Receipt).State==PublicationState.Unresolved,"different same-owner preview refuses");
  C(baseline.Reconcile(bp,null).State==PublicationState.Unresolved,"null receipt refuses");
  C(Convert.ToBase64String(File.ReadAllBytes(P("source")))==original,"source bytes preserved throughout");
  Console.WriteLine("TOTAL "+count);
 }
}
