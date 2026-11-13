using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Toolchain;

namespace Ashfall.Modules.Toolchain.Tests
{
    public static class Tests
    {
        private static int checks;
        private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
        private static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; } catch (IOException) { rejected = true; }
            Check(rejected, message);
        }
        public static int Main(string[] args)
        {
            try
            {
                string root = args[0];
                var runner = new CompilerRunner(root, 10000);
                string fixtures = Path.Combine(root, "fixtures"); Directory.CreateDirectory(fixtures);
                string good = Path.Combine(fixtures, "space name.cs");
                File.WriteAllText(good, "class Probe { static int Main() { System.Console.WriteLine(6 * 7); System.Console.Error.WriteLine(\"diagnostic\"); return 0; } }");
                var result = runner.Compile(new string[] {good}, Path.Combine(root, "good"), true);
                Check(result.Success, "quoted absolute source with spaces compiles C#5");
                var run = CompilerRunner.Execute(result.Artifact, "", root, 5000);
                Check(run.ExitCode == 0 && run.StandardOutput.Trim() == "42" && run.StandardError.Trim() == "diagnostic", "compiled production runner executes and captures both streams");
                Reject(delegate { runner.Compile(new string[] {good}, Path.Combine(root, "good"), true); }, "existing artifacts preserved");
                string bad = Path.Combine(fixtures, "bad.cs"); File.WriteAllText(bad, "class Bad { this is invalid }");
                Check(!runner.Compile(new string[] {bad}, Path.Combine(root, "bad"), false).Success, "syntax error returns failure");
                File.WriteAllText(bad, "class Bad { public int Value => 3; }");
                Check(!runner.Compile(new string[] {bad}, Path.Combine(root, "modern"), false).Success, "C#6 expression body rejected by actual compiler");
                Reject(delegate { runner.Compile(new string[] {good}, Path.GetDirectoryName(root), false); }, "output escape rejected");
                Reject(delegate { runner.Compile(new string[] {good, good}, Path.Combine(root, "duplicate"), false); }, "duplicate inputs rejected");
                string module = Path.Combine(fixtures, "module"); Directory.CreateDirectory(module);
                File.Copy(good, Path.Combine(module, "Keep.cs"));
                Directory.CreateDirectory(Path.Combine(module, "Engine")); File.Copy(bad, Path.Combine(module, "Engine", "Unity.cs"));
                Directory.CreateDirectory(Path.Combine(module, "Tests")); File.Copy(bad, Path.Combine(module, "Tests", "Test.cs"));
                var files = new ModuleDiscovery().Discover(module);
                Check(files.Length == 1 && runner.Compile(files, Path.Combine(root, "discovered"), true).Success, "discovery excludes nonneutral Engine and Tests fixtures");
                File.WriteAllText(bad, "class Probe { static void Main() { System.Threading.Thread.Sleep(10000); } }");
                result = runner.Compile(new string[] {bad}, Path.Combine(root, "timeout"), true);
                Check(result.Success && CompilerRunner.Execute(result.Artifact, "", root, 100).TimedOut, "hung executable is terminated and reported");
                Reject(delegate { CompilerRunner.Execute(@"\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe", "", root, 100); }, "root-relative executable rejected");
                Reject(delegate { CompilerRunner.Execute("/Windows/csc.exe", "", root, 100); }, "slash-root executable rejected");
                Reject(delegate { CompilerRunner.Execute("C:relative.exe", "", root, 100); }, "drive-relative executable rejected");
                Reject(delegate { CompilerRunner.Execute(@"\\server\share\probe.exe", "", root, 100); }, "UNC executable rejected");
                Reject(delegate { CompilerRunner.Execute(result.Artifact, "", "C:relative", 100); }, "drive-relative working directory rejected");
                Reject(delegate { new CompilerRunner("C:relative", 100); }, "drive-relative output root rejected");
                Reject(delegate { runner.Compile(new string[] { good.Substring(2) }, Path.Combine(root, "ambiguous-source"), false); }, "root-relative source rejected before output creation");
                Reject(delegate { new ModuleDiscovery().Discover("C:relative"); }, "ambiguous discovery root rejected");
                string inherited = Path.Combine(fixtures, "Inherited.cs");
                File.WriteAllText(inherited, "using System; using System.Diagnostics; using System.Threading; class Inherited { static void Main(string[] a) { if(a.Length>0 && a[0]==\"child\") {Thread.Sleep(2500); return;} Console.WriteLine(\"parent output\"); Console.Error.WriteLine(\"parent error\"); Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,\"child\") {UseShellExecute=false,CreateNoWindow=true}); if(a.Length==0) Thread.Sleep(10000); } }");
                var inheritedBuild = runner.Compile(new string[] { inherited }, Path.Combine(root, "inherited"), true);
                Check(inheritedBuild.Success, "production inherited-handle fixture compiles");
                foreach (string mode in new string[] { "", "exit" })
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var inheritedRun = CompilerRunner.Execute(inheritedBuild.Artifact, mode, root, 200);
                    watch.Stop();
                    Console.WriteLine("INHERITED mode=" + mode + " elapsedMs=" + watch.ElapsedMilliseconds + " exit=" + inheritedRun.ExitCode);
                    Check(inheritedRun.TimedOut && inheritedRun.OutputIncomplete && !inheritedRun.TerminationIncomplete && watch.ElapsedMilliseconds < 1500,
                        "bounded inherited-pipe cleanup parent mode=" + mode);
                    Check(inheritedRun.StandardOutput.Contains("parent output") && inheritedRun.StandardError.Contains("parent error"), "available diagnostics retained mode=" + mode);
                    if (mode == "exit") Check(inheritedRun.ExitCode == 0, "normally exited parent retains exit code while drain times out");
                }
                File.WriteAllText(bad, "class Probe { static int Main() {System.Console.Write(\"tail\"); System.Console.Error.Write(\"error-tail\"); return 7;} }");
                var nonzero = runner.Compile(new string[] { bad }, Path.Combine(root, "nonzero"), true);
                var nonzeroRun = CompilerRunner.Execute(nonzero.Artifact, "", root, 5000);
                Check(!nonzeroRun.TimedOut && !nonzeroRun.OutputIncomplete && nonzeroRun.ExitCode == 7 && nonzeroRun.StandardOutput.Trim() == "tail" && nonzeroRun.StandardError.Trim() == "error-tail", "EOF flushes unterminated diagnostics and preserves failure exit");
                Check(Float3.FromGodot(2, 3, -4).Z == 4 && !new Float3(float.NaN, 0, 0).IsFinite, "real frozen Core conversion and finite guard execute");
                Console.WriteLine("RESULT " + checks + " PASS; neutral evidence only"); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
