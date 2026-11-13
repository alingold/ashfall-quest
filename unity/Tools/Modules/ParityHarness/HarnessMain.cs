using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using Ashfall.Core;
using Ashfall.ToolchainProbe;

namespace Ashfall.Modules.ParityHarness
{
    // Read-only historical public API, compiled from its original source in the
    // owned output directory. This does not run its Main or rewrite its binary.
    public sealed class HistoricalConversionAdapter : IConversionPort
    {
        private static Vec In(Float3 v) { return new Vec(v.X,v.Y,v.Z); }
        private static Float3 Out(Vec v) { return new Float3((float)v.X,(float)v.Y,(float)v.Z); }
        public Float3 Point(Float3 v) { return Out(Conversion.Reflect(In(v))); }
        public double[] Basis(double[] m) { return Conversion.Basis(m); }
        public Float3 Normal(double[] m, Float3 n) { return Out(Conversion.Normal(m,In(n))); }
        public int[] Winding(int[] indices) { return Conversion.Winding(indices); }
    }
    public static class HarnessMain
    {
        public static int Main(string[] args)
        {
            try {
                if (args.Length == 1 && args[0] == "--self-test") return HarnessSelfTests.Run();
                if (args.Length < 2 || args.Length > 4) throw new ArgumentException("Usage: Harness.exe <report.json> <repetitions> [--strict] [provider-assembly-qualified-type]");
                int repeats = Int32.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
                bool strict = false;
                Subjects subjects = new Subjects { Conversion = new HistoricalConversionAdapter() };
                for (int i = 2; i < args.Length; i++) {
                    if (args[i] == "--strict") strict = true;
                    else {
                        Type type = Type.GetType(args[i], true);
                        ISubjectProvider provider = Activator.CreateInstance(type) as ISubjectProvider;
                        if (provider == null) throw new ArgumentException("Provider must implement harness-owned ISubjectProvider");
                        subjects = provider.Create();
                    }
                }
                RunReport report = ScenarioRunner.Run(RepresentativeScenarios.Create(), subjects, repeats);
                File.WriteAllText(args[0], new JavaScriptSerializer().Serialize(report));
                foreach (ScenarioResult r in report.Results) Console.WriteLine(r.Status + " " + r.Id + (r.Detail.Length == 0 ? "" : " : " + r.Detail));
                Console.WriteLine("PASS=" + report.Passed + " FAIL=" + report.Failed + " UNKNOWN=" + report.Unknown + " complete=" + report.Complete);
                return report.ExitCode(strict);
            } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
