using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.ParityHarness
{
    public static class HarnessSelfTests
    {
        private sealed class Lease : IDisposable { readonly Action close; public Lease(Action close) { this.close=close; } public void Dispose() { close(); } }
        private sealed class BrokenConversion : IConversionPort
        {
            private readonly IConversionPort real = new HistoricalConversionAdapter();
            public Float3 Point(Float3 v) { return v; } // Inject missing Z reflection.
            public double[] Basis(double[] m) { return real.Basis(m); }
            public Float3 Normal(double[] m, Float3 n) { return n; } // Inject absent inverse transpose.
            public int[] Winding(int[] i) { return i; } // Inject absent winding correction.
        }
        private sealed class InverseOnly : IConversionPort
        {
            readonly HistoricalConversionAdapter real = new HistoricalConversionAdapter();
            public Float3 Point(Float3 v) { return real.Point(v); }
            public double[] Basis(double[] m) { return real.Basis(m); }
            public int[] Winding(int[] i) { return real.Winding(i); }
            public Float3 Normal(double[] m, Float3 n) { return real.Normal(new double[] {m[0],m[3],m[6],m[1],m[4],m[7],m[2],m[5],m[8]},n); }
        }
        private static Scenario Case(string id, Action<Subjects> action) { return new Scenario(id,"self-test","runner control flow",action); }
        public static int Run()
        {
            int calls = 0;
            RunReport result = ScenarioRunner.Run(new Scenario[] {
                Case("fail",delegate { throw new InvalidOperationException("injected"); }),
                Case("unknown",delegate { throw new UnknownEvidenceException("missing fixture"); }),
                Case("pass",delegate { calls++; })
            },new Subjects(),3);
            Verify.That(result.Failed == 1 && result.Unknown == 1 && result.Passed == 1 && calls == 3,"Runner did not continue/repeat or misclassified evidence");
            Verify.That(result.ExitCode(false) == 1 && !result.Complete,"Failure must dominate unknown");
            RunReport unknown = ScenarioRunner.Run(new Scenario[] { Case("unknown",delegate { throw new UnknownEvidenceException("missing"); }) },new Subjects(),1);
            Verify.That(unknown.ExitCode(false) == 0 && unknown.ExitCode(true) == 2 && !unknown.Complete,"Unknown masqueraded as complete");
            Verify.Reject(delegate { ScenarioRunner.Run(new Scenario[] { Case("same",delegate {}), Case("same",delegate {}) },new Subjects(),1); });
            Verify.Reject(delegate { ScenarioRunner.Run(new Scenario[0],new Subjects(),0); });
            Verify.Reject(delegate { ScenarioRunner.Run(new Scenario[0],new Subjects(),1); });
            RunReport partial = ScenarioRunner.Run(new[] { Case("arbitrary",delegate {}) },new Subjects(),1);
            Verify.That(!partial.Complete && partial.ExitCode(true)==2 && partial.MissingRequiredIds.Count>0,"Arbitrary nonempty suite passed completeness");
            List<Scenario> missing = new List<Scenario>(RepresentativeScenarios.Create()); missing.RemoveAt(0);
            RunReport omitted = ScenarioRunner.Run(missing,new Subjects(),1);
            Verify.That(omitted.MissingRequiredIds.Count==1 && omitted.MissingRequiredIds[0]=="CORE-TRANSFER-01","Omitted required ID not detected");
            RunReport inverse = ScenarioRunner.Run(RepresentativeScenarios.Create(),new Subjects { Conversion=new InverseOnly() },1);
            Verify.That(inverse.Results.Exists(delegate(ScenarioResult x) { return x.Id=="CONVERT-NORMAL-SHEAR-01" && x.Status=="FAIL"; }),"Missing transpose mutation escaped");
            bool disposed=false; int continued=0;
            RunReport cleanup=ScenarioRunner.Run(new[] { Case("cleanup",delegate { using(new Lease(delegate { disposed=true; })) { throw new Exception("assert"); } }), Case("continue",delegate { continued++; }) },new Subjects(),3);
            Verify.That(disposed && cleanup.Failed==1 && continued==3,"Reviewer cleanup regression");
            RunReport mutation = ScenarioRunner.Run(RepresentativeScenarios.Create(),new Subjects { Conversion = new BrokenConversion() },1);
            HashSet<string> detected = new HashSet<string>();
            foreach (ScenarioResult r in mutation.Results) if (r.Status == "FAIL") detected.Add(r.Id);
            Verify.That(detected.Contains("CONVERT-POINT-01") && detected.Contains("CONVERT-NORMAL-01") && detected.Contains("CONVERT-WINDING-01"),"Representative cases failed to detect geometry mutations");
            int attempt = 0;
            RunReport intermittent = ScenarioRunner.Run(new Scenario[] { Case("intermittent",delegate { if (++attempt == 2) throw new Exception("second-run regression"); }) },new Subjects(),5);
            Verify.That(intermittent.Failed == 1 && intermittent.Results[0].Executions == 2,"Repeat failure was hidden");
            Console.WriteLine("PASS runner classification, continuation, repeats, strict unknown, duplicate/invalid registry, intermittent failure, empty/omitted required IDs, cleanup, inverse-only and three geometry mutation detections");
            return 0;
        }
    }
}
