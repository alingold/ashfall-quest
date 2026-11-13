using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Modules.ParityHarness
{
    public static class LinkedMain
    {
        static Scenario C(string id,Action<Subjects> run) { return new Scenario(id,"production FlightMachine/WeaponHandling public APIs; reviewer r1 binding requirements","real state/rejection regression",run); }
        static void SameFlight(FlightMachine m,Float3 v,FlightMode mode,float speed) { Verify.Near(m.Velocity.X,v.X,"vx"); Verify.Near(m.Velocity.Y,v.Y,"vy"); Verify.Near(m.Velocity.Z,v.Z,"vz"); Verify.That(m.Mode==mode && m.GlideSpeed==speed,"Rejected launch changed flight state"); }
        static void SameGun(HandlingSnapshot a,HandlingSnapshot b) { Verify.That(a.Equals(b),"Rejected authority changed full public weapon snapshot"); }
        public static int Main(string[] args)
        {
            try {
                HarnessSelfTests.Run();
                IList<Scenario> cases=RepresentativeScenarios.Create();
                cases.Add(C("LINK-FLIGHT-AUTH-01",delegate {
                    using(var f=new FlightFixture(new PadInitialState {InputEnabled=true,Gliding=true})) {
                        var m=f.Machine; Float3 before=m.Velocity; FlightMode mode=m.Mode; float speed=m.GlideSpeed;
                        foreach(var c in new[] { new CommandContext("wrong","owner","bad",10,1,true), new CommandContext("parity","wrong","bad",10,1,true), new CommandContext("parity","owner","bad",10,2,true),new CommandContext("parity","owner","bad",10,1,false) }) {
                            Verify.That(!m.Launch(c,new Float3(0,1,0),45,true,false,false).Accepted,"Foreign launch accepted"); SameFlight(m,before,mode,speed);
                        }
                        var valid=f.Next(); Verify.That(m.Launch(valid,new Float3(0,1,0),45,true,false,false).Accepted,"Valid launch");
                        before=m.Velocity; mode=m.Mode;
                        Verify.That(!m.Launch(valid,new Float3(1,0,0),99,true,false,false).Accepted,"Replay accepted"); SameFlight(m,before,mode,speed);
                        foreach(float bad in new[] {0f,-1f,1001f,float.NaN,float.PositiveInfinity}) { Verify.That(!m.Launch(f.Next(),new Float3(0,1,0),bad,true,false,false).Accepted,"Bad launch speed"); SameFlight(m,before,mode,speed); }
                        Verify.That(!m.Launch(f.Next(),new Float3(0,.316f,0),10,true,false,false).Accepted,"Below length boundary"); SameFlight(m,before,mode,speed);
                        Verify.That(m.Launch(f.Next(),new Float3(0,.317f,0),1000,true,false,false).Accepted,"Valid boundary rejected");
                        Verify.Near(m.Velocity.Y,1000,"speed ceiling");
                    }
                }));
                cases.Add(C("LINK-FLIGHT-UNAVAILABLE-01",delegate {
                    bool unknown=false; try { using(var f=new FlightFixture(new PadInitialState {InputEnabled=true,Gliding=true,Diving=true})) {} } catch(UnknownEvidenceException) { unknown=true; }
                    Verify.That(unknown,"Impossible construction must remain UNKNOWN");
                }));
                cases.Add(C("LINK-WEAPON-AUTH-01",delegate {
                    using(var f=new WeaponFixture(7)) {
                        HandlingSnapshot before=f.Machine.Snapshot;
                        foreach(var c in new[] {new CommandContext("wrong","owner","bad",100,1,true),new CommandContext("parity","wrong","bad",100,1,true),new CommandContext("parity","owner","bad",100,2,true),new CommandContext("parity","owner","bad",100,1,false)}) {
                            f.Input.Sequence=100; Verify.That(!f.Machine.Apply(c,HandlingAction.Sample,f.Input).Accepted,"Foreign weapon command"); SameGun(before,f.Machine.Snapshot); Verify.That(f.Reserve==7,"Reserve changed");
                        }
                        CommandContext valid=f.Next(); Verify.That(f.Machine.Apply(valid,HandlingAction.Sample,f.Input).Accepted,"Valid sample"); before=f.Machine.Snapshot;
                        f.Input.Sequence++; Verify.That(!f.Machine.Apply(valid,HandlingAction.Eject,f.Input).Accepted,"Replay accepted"); SameGun(before,f.Machine.Snapshot);
                    }
                }));
                cases.Add(C("LINK-WEAPON-GESTURE-01",delegate {
                    using(var f=new WeaponFixture(7)) {
                        f.GrabMagazine(); Verify.That(f.Stage=="held_mag" && f.Reserve==7,"Grab phase");
                        f.At(0,-.25f,.15f,true); Verify.That(f.Stage=="held_mag","Short insert stroke accepted");
                        f.InsertMagazine(); Verify.That(f.Seated==7 && f.Ammo==0 && !f.Chambered,"Seat phase");
                        f.At(-.045f,.064f,.10f,false); f.At(-.045f,.064f,.10f,true); f.At(-.045f,.064f,.04f,true);
                        Verify.That(!f.Chambered,"Short rack accepted");
                        f.Input.SupportTracked=false; Verify.That(f.Send(HandlingAction.Sample).Accepted,"Tracking sample"); Verify.That(f.Machine.Snapshot.BoltTravel==0 && !f.Machine.Snapshot.ClaimsSupport,"Tracking loss retained contact");
                        f.Input.SupportTracked=true; f.At(-.045f,.064f,.02f,true); Verify.That(!f.Chambered,"Stale grip completed rack");
                        f.RackBolt(); Verify.That(f.Chambered && f.Ammo==7 && f.Seated==0 && f.Reserve==0,"Fresh rack failed");
                    }
                }));
                cases.Add(C("LINK-WEAPON-SAFETY-01",delegate {
                    for(int blocker=0;blocker<4;blocker++) using(var f=new WeaponFixture(7)) {
                        f.GrabMagazine(); f.Input.Paused=blocker==0; f.Input.PrimaryClimbing=blocker==1; f.Input.SupportReserved=blocker==2; f.Input.SupportTracked=blocker!=3;
                        f.Input.SupportLocal=new Float3(0,-.18f,.15f); f.Send(HandlingAction.Sample);
                        Verify.That(f.Stage=="needs_mag" && f.Reserve==7 && f.Seated==0 && !f.Chambered,"Unsafe sample seated magazine");
                    }
                }));
                RunReport report=ScenarioRunner.Run(cases,new LinkedSubjectProvider().Create(),5);
                File.WriteAllText(args[0],new JavaScriptSerializer().Serialize(report));
                foreach(var row in report.Results) Console.WriteLine(row.Status+" "+row.Id+" "+row.Detail);
                Console.WriteLine("PASS="+report.Passed+" FAIL="+report.Failed+" UNKNOWN="+report.Unknown+" Complete="+report.Complete+" strict="+report.ExitCode(true));
                return report.ExitCode(args.Length>1 && args[1]=="--strict");
            } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
