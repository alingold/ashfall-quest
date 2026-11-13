using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.ParityHarness
{
    public static class RepresentativeScenarios
    {
        public static IEnumerable<string> RequiredIds { get { return new string[] { "CORE-TRANSFER-01","CORE-FINITE-01","CONVERT-POINT-01","CONVERT-BASIS-01","CONVERT-NORMAL-01","CONVERT-NORMAL-SHEAR-01","CONVERT-WINDING-01","CONVERT-REJECT-01","FLIGHT-PAD-01","FLIGHT-PAD-02","FLIGHT-PAD-03","FLIGHT-PAD-DIVE-01","FLIGHT-PAD-VELOCITY-01","RELOAD-PHASE-01","RELOAD-EMPTY-01","GATE-UNITY-IMPORT","GATE-DESKTOP-PLAY","GATE-PCVR-TRACKING","GATE-QUEST-BUILD-DEVICE","GATE-TWO-PLAYER-WAN","GATE-AUDIO-LISTENING","GATE-MAP-ROUTES","GATE-SKIN-POSES" }; } }
        private const string Transfer = "unity/Documentation/CONTRACTS-v1.md#Transfer and identity; unity/Tools/ToolchainProbe/ConversionProbe.cs:Conversion";
        private const string Pad = "quest-r22/source/scripts/player.gd:584-589 launch_from_pad_r10 (bdba6e30012e1733efd5021fd8ded593f82f26e6)";
        private const string Reload = "quest-r22/source/scripts/weapon.gd:440-463 grab_magazine/insert_magazine/rack_bolt; game/scripts/weapon.gd:439-462";
        private static void Vector(Float3 a, float x, float y, float z)
        { Verify.Near(a.X, x, "x"); Verify.Near(a.Y, y, "y"); Verify.Near(a.Z, z, "z"); }
        private static IConversionPort Conversion(Subjects s)
        { if (s.Conversion == null) throw new UnknownEvidenceException("No conversion public API adapter"); return s.Conversion; }
        private static IGameplayFixtures Gameplay(Subjects s)
        { if (s.Gameplay == null) throw new UnknownEvidenceException("No coordinator-bound gameplay public API adapter"); return s.Gameplay; }
        private static IPadFixture NewPad(Subjects s, PadInitialState state)
        { IPadFixture f = Gameplay(s).CreatePad(state); if (f == null) throw new UnknownEvidenceException("Pad adapter unavailable"); return f; }
        private static IReloadFixture NewReload(Subjects s, int reserve)
        { IReloadFixture f = Gameplay(s).CreateReload(reserve); if (f == null) throw new UnknownEvidenceException("Reload adapter unavailable"); return f; }

        public static IList<Scenario> Create()
        {
            List<Scenario> cases = new List<Scenario>();
            cases.Add(new Scenario("CORE-TRANSFER-01", Transfer, "Godot forward becomes Unity forward; asymmetric SI point keeps scale", delegate(Subjects s) {
                Vector(Float3.FromGodot(0, 0, -1), 0, 0, 1);
                Vector(Float3.FromGodot(1.25f, -3, 7.5f), 1.25f, -3, -7.5f);
            }));
            cases.Add(new Scenario("CORE-FINITE-01", "unity/Assets/Ashfall/Core/Contracts.cs:Float3.IsFinite", "Every axis rejects NaN and both infinities; finite extremes remain valid", delegate(Subjects s) {
                foreach (float v in new float[] { Single.NaN, Single.PositiveInfinity, Single.NegativeInfinity }) {
                    Verify.That(!new Float3(v, 0, 0).IsFinite && !new Float3(0, v, 0).IsFinite && !new Float3(0, 0, v).IsFinite, "Invalid coordinate escaped finite gate");
                }
                Verify.That(new Float3(Single.MaxValue, Single.MinValue, 0).IsFinite, "Finite SI extremes rejected");
            }));
            cases.Add(new Scenario("CONVERT-POINT-01", Transfer, "Historical converter preserves SI units and forward reflection", delegate(Subjects s) {
                Vector(Conversion(s).Point(new Float3(1.25f, -3, -7.5f)), 1.25f, -3, 7.5f);
            }));
            cases.Add(new Scenario("CONVERT-BASIS-01", Transfer, "Quarter-turn yaw maps Unity forward onto negative X; nonuniform/sheared matrix retains all coefficients", delegate(Subjects s) {
                double[] input = { 0, 0, 1, 0, 1, 0, -1, 0, 0 };
                double[] actual = Conversion(s).Basis(input);
                double[] expected = { 0, 0, -1, 0, 1, 0, 1, 0, 0 };
                Verify.That(actual.Length == 9, "Basis length");
                for (int i = 0; i < 9; i++) Verify.Near(actual[i], expected[i], "yaw coefficient");
                Verify.Near(input[2], 1, "source basis mutation");
                double[] shear = { 2, .3, .5, .1, 3, .7, .2, .4, 4 };
                double[] frozen = { 2, .3, -.5, .1, 3, -.7, -.2, -.4, 4 };
                actual = Conversion(s).Basis(shear);
                for (int i = 0; i < 9; i++) Verify.Near(actual[i], frozen[i], "shear coefficient");
            }));
            cases.Add(new Scenario("CONVERT-NORMAL-01", Transfer, "For scale (2,1,1), normal (1,1,0) becomes unit (1/sqrt(5),2/sqrt(5),0)", delegate(Subjects s) {
                Float3 normal = Conversion(s).Normal(new double[] { 2,0,0, 0,1,0, 0,0,1 }, new Float3(1,1,0));
                Verify.Near(normal.X, 1 / Math.Sqrt(5), "normal x"); Verify.Near(normal.Y, 2 / Math.Sqrt(5), "normal y"); Verify.Near(normal.Z, 0, "normal z");
                Verify.Near(2 * normal.X - normal.Y, 0, "Transformed normal must be perpendicular to transformed tangent (2,-1,0)");
            }));
            cases.Add(new Scenario("CONVERT-NORMAL-SHEAR-01", Transfer, "Nonsymmetric analytic shear normal (4,-4,1)/sqrt(33), unit and perpendicular to two independent transformed tangents", delegate(Subjects s) {
                Float3 n = Conversion(s).Normal(new double[] {2,1,0,0,3,1,0,0,4}, new Float3(1,-1,0));
                Verify.Near(n.X,4/Math.Sqrt(33),"analytic shear x"); Verify.Near(n.Y,-4/Math.Sqrt(33),"analytic shear y"); Verify.Near(n.Z,1/Math.Sqrt(33),"analytic shear z");
                Verify.Near(n.X*n.X+n.Y*n.Y+n.Z*n.Z,1,"unit normal");
                Verify.Near(3*n.X+3*n.Y,0,"tangent (1,1,0)"); Verify.Near(n.Y+4*n.Z,0,"tangent (0,0,1)");
                n = Conversion(s).Normal(new double[] {2,.3,.5,.1,3,.7,.2,.4,4},new Float3(1,-1,0));
                Verify.Near(2.3*n.X+3.1*n.Y+.6*n.Z,0,"reviewer shear tangent one");
                Verify.Near(.5*n.X+.7*n.Y+4*n.Z,0,"reviewer shear tangent two");
                Verify.Near(n.X*n.X+n.Y*n.Y+n.Z*n.Z,1,"reviewer shear unit");
            }));
            cases.Add(new Scenario("CONVERT-WINDING-01", Transfer, "Two triangles reverse exactly once without modifying source indices", delegate(Subjects s) {
                int[] source = { 0, 1, 2, 2, 1, 3 }; int[] wanted = { 0, 2, 1, 2, 3, 1 };
                int[] actual = Conversion(s).Winding(source);
                Verify.That(actual.Length == 6 && !Object.ReferenceEquals(source, actual), "Output must be independent triangle storage");
                for (int i = 0; i < 6; i++) Verify.That(actual[i] == wanted[i], "Triangle winding");
                Verify.That(source[1] == 1 && source[4] == 1, "Source mesh mutated");
            }));
            cases.Add(new Scenario("CONVERT-REJECT-01", Transfer, "Malformed basis, singular normal, partial triangles, negative indices and nonfinite points reject", delegate(Subjects s) {
                IConversionPort c = Conversion(s);
                Verify.Reject(delegate { c.Basis(new double[8]); });
                Verify.Reject(delegate { c.Normal(new double[9], new Float3(0,1,0)); });
                Verify.Reject(delegate { c.Winding(new int[] { 0,1 }); });
                Verify.Reject(delegate { c.Winding(new int[] { 0,-1,2 }); });
                Verify.Reject(delegate { c.Point(new Float3(0,0,Single.NaN)); });
            }));
            cases.Add(new Scenario("FLIGHT-PAD-01", Pad, "Reachable glide start; tilted pad replaces prior velocity with (0,27,36), clears glide/dive; repeated launch remains usable", delegate(Subjects s) {
                using (IPadFixture f = NewPad(s, new PadInitialState { InputEnabled = true, Gliding = true, Velocity = new Float3(0,-12f/7,12) })) {
                    Verify.That(f.Gliding && !f.Diving,"Glide setup not observed");
                    Verify.That(f.Launch(45, new Float3(0,3,4)), "Tilted launch rejected"); Vector(f.Velocity,0,27,36);
                    Verify.That(!f.Gliding && !f.Diving, "Flight state not cleared");
                    Verify.That(f.Launch(10,new Float3(1,0,0)), "Repeat launch rejected"); Vector(f.Velocity,10,0,0);
                }
            }));
            cases.Add(new Scenario("FLIGHT-PAD-DIVE-01", Pad, "Reachable dive start clears on launch", delegate(Subjects s) {
                using (IPadFixture f = NewPad(s,new PadInitialState { InputEnabled=true, Diving=true })) {
                    Verify.That(f.Diving && !f.Gliding,"Dive setup not observed");
                    Verify.That(f.Launch(45,new Float3(0,3,4)),"Dive launch rejected"); Vector(f.Velocity,0,27,36);
                    Verify.That(!f.Gliding && !f.Diving,"Dive not cleared");
                }
            }));
            cases.Add(new Scenario("FLIGHT-PAD-VELOCITY-01", Pad, "Arbitrary velocity is replaced, never added", delegate(Subjects s) {
                using (IPadFixture f = NewPad(s,new PadInitialState { InputEnabled=true, Velocity=new Float3(8,70,-9) })) {
                    Vector(f.Velocity,8,70,-9); Verify.That(f.Launch(45,new Float3(0,3,4)),"Launch rejected"); Vector(f.Velocity,0,27,36);
                }
            }));
            cases.Add(new Scenario("FLIGHT-PAD-02", Pad, "Disabled input, climbing and mantling each reject without changing velocity", delegate(Subjects s) {
                for (int i = 0; i < 3; i++) using (IPadFixture f = NewPad(s, new PadInitialState { InputEnabled = i != 0, Climbing = i == 1, Mantling = i == 2, Velocity = new Float3(2,3,4) })) {
                    Verify.That(!f.Launch(45,new Float3(0,1,0)), "Blocked state launched"); Vector(f.Velocity,2,3,4);
                }
            }));
            cases.Add(new Scenario("FLIGHT-PAD-03", Pad, "Invalid speed/direction rejects atomically", delegate(Subjects s) {
                using (IPadFixture f = NewPad(s, new PadInitialState { InputEnabled = true, Velocity = new Float3(2,3,4) })) {
                    foreach (float speed in new float[] { 0,-1,Single.NaN,Single.PositiveInfinity }) { Verify.That(!f.Launch(speed,new Float3(0,1,0)), "Invalid speed accepted"); Vector(f.Velocity,2,3,4); Verify.That(!f.Gliding && !f.Diving,"Rejected launch changed mode"); }
                    foreach (Float3 direction in new Float3[] { new Float3(0,0,0), new Float3(0,.1f,0), new Float3(Single.NaN,1,0) }) { Verify.That(!f.Launch(45,direction), "Invalid direction accepted"); Vector(f.Velocity,2,3,4); Verify.That(!f.Gliding && !f.Diving,"Rejected direction changed mode"); }
                    Vector(f.Velocity,2,3,4);
                }
            }));
            cases.Add(new Scenario("RELOAD-PHASE-01", Reload, "Rack before seating does nothing; 7 reserve rounds require grab, insert, then rack before ammo is usable", delegate(Subjects s) {
                using (IReloadFixture f = NewReload(s,7)) {
                    Verify.That(f.Stage == "needs_mag" && f.Ammo == 0 && f.Reserve == 7,"Fixture initial state");
                    f.RackBolt(); Verify.That(f.Stage == "needs_mag" && f.Ammo == 0,"Premature rack chambered");
                    f.GrabMagazine(); Verify.That(f.Stage == "held_mag" && f.Reserve == 7,"Grab spent reserve");
                    f.InsertMagazine(); Verify.That(f.Stage == "needs_rack" && f.Ammo == 0 && f.Seated == 7 && !f.Chambered && f.Reserve == 0,"Seat bypassed bolt or reserve conservation");
                    f.RackBolt(); Verify.That(f.Stage == "ready" && f.Ammo == 7 && f.Seated == 0 && f.Chambered,"Rack did not chamber partial magazine");
                    f.RackBolt(); Verify.That(f.Ammo == 7 && f.Reserve == 0,"Repeated rack created ammo");
                }
            }));
            cases.Add(new Scenario("RELOAD-EMPTY-01", Reload, "No reserve leaves empty rifle in needs_mag throughout grab/insert/rack", delegate(Subjects s) {
                using (IReloadFixture f = NewReload(s,0)) {
                    f.GrabMagazine(); f.InsertMagazine(); f.RackBolt();
                    Verify.That(f.Stage == "needs_mag" && f.Ammo == 0 && f.Reserve == 0,"Empty reserve created ammunition");
                }
            }));
            foreach (string gate in new string[] { "UNITY-IMPORT", "DESKTOP-PLAY", "PCVR-TRACKING", "QUEST-BUILD-DEVICE", "TWO-PLAYER-WAN", "AUDIO-LISTENING", "MAP-ROUTES", "SKIN-POSES" }) {
                string captured = gate;
                cases.Add(new Scenario("GATE-" + gate, "docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md:M1-M4", "Actual matched runtime/device evidence required", delegate(Subjects s) { throw new UnknownEvidenceException(captured + " evidence unavailable to neutral harness"); }));
            }
            return cases;
        }
    }
}
