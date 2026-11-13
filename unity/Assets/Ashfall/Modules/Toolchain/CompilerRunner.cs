#if UNITY_EDITOR || !UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Ashfall.Modules.Toolchain
{
    public sealed class CompileResult
    {
        public int ExitCode;
        public bool TimedOut, OutputIncomplete, TerminationIncomplete;
        public string CleanupError;
        public string Artifact, StandardOutput, StandardError;
        public bool Success { get { return !TimedOut && !OutputIncomplete && !TerminationIncomplete && ExitCode == 0 && File.Exists(Artifact); } }
    }

    public sealed class ModuleDiscovery : IModuleDiscovery
    {
        public string[] Discover(string moduleDirectory)
        {
            var files = new List<string>();
            Walk(Guard.CanonicalCPath(moduleDirectory), files);
            files.Sort(StringComparer.Ordinal);
            return files.ToArray();
        }
        private static void Walk(string directory, List<string> files)
        {
            Guard.NoLinks(directory);
            foreach (string file in Directory.GetFiles(directory, "*.cs"))
            {
                Guard.NoLinks(file);
                files.Add(Path.GetFullPath(file));
            }
            foreach (string child in Directory.GetDirectories(directory))
            {
                string name = Path.GetFileName(child);
                if (String.Equals(name, "Engine", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(name, "Tests", StringComparison.OrdinalIgnoreCase)) continue;
                Walk(child, files);
            }
        }
    }

    public static class Guard
    {
        // No current-drive resolution, UNC/device paths, ADS, or normalization surprises.
        public static string CanonicalCPath(string path)
        {
            if (String.IsNullOrEmpty(path) || path.Length < 3 ||
                !path.StartsWith(@"C:\", StringComparison.OrdinalIgnoreCase) ||
                path.IndexOfAny(new char[] {'/', '"', '\r', '\n'}) >= 0 || path.IndexOf(':', 2) >= 0)
                throw new ArgumentException("Fully qualified canonical C: path required");
            string full = Path.GetFullPath(path);
            if (!String.Equals(full, path, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Noncanonical path refused: " + path);
            NoLinks(full);
            return full;
        }
        public static void NoLinks(string path)
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                if ((Directory.Exists(current) || File.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse point refused: " + current);
                current = Path.GetDirectoryName(current);
            }
        }
        public static string Quote(string value)
        {
            if (String.IsNullOrEmpty(value) || value.IndexOfAny(new char[] {'"', '\r', '\n'}) >= 0 || value.EndsWith("\\", StringComparison.Ordinal))
                throw new ArgumentException("Unsafe argument");
            return "\"" + value + "\"";
        }
        public static string Hash(string file)
        {
            using (var stream = File.OpenRead(file))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }

    public sealed class CompilerRunner : ICompilerRunner
    {
        public const string FrameworkCompiler = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe";
        private readonly string outputRoot;
        private readonly int timeoutMilliseconds;
        public CompilerRunner(string ownedOutputRoot, int timeoutMilliseconds)
        {
            outputRoot = Guard.CanonicalCPath(ownedOutputRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (Path.GetPathRoot(outputRoot) != @"C:\") throw new ArgumentException("Outputs must be on C:");
            Guard.NoLinks(outputRoot);
            if (timeoutMilliseconds < 1 || timeoutMilliseconds > 60000) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            this.timeoutMilliseconds = timeoutMilliseconds;
        }
        public CompileResult Compile(string[] sources, string outputDirectory, bool executable)
        {
            string directory = Guard.CanonicalCPath(outputDirectory);
            if (!directory.StartsWith(outputRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output must be a new child of the owned output root");
            Guard.NoLinks(directory);
            if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Output already exists; use a fresh run directory");
            if (sources == null || sources.Length == 0) throw new ArgumentException("No source files");
            var lines = new List<string>();
            var hashes = new List<string>();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string artifact = Path.Combine(directory, executable ? "probe.exe" : "module.dll");
            lines.Add("/nologo"); lines.Add("/langversion:5");
            lines.Add("/warn:4"); lines.Add("/warnaserror+");
            lines.Add("/reference:" + Guard.Quote(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll"));
            lines.Add("/reference:" + Guard.Quote(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll"));
            lines.Add("/reference:" + Guard.Quote(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xml.dll"));
            lines.Add("/reference:" + Guard.Quote(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xml.Linq.dll"));
            lines.Add("/reference:" + Guard.Quote(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.Serialization.dll"));
            lines.Add(executable ? "/target:exe" : "/target:library");
            lines.Add("/out:" + Guard.Quote(artifact));
            foreach (string source in sources)
            {
                string full = Guard.CanonicalCPath(source);
                Guard.NoLinks(full);
                if (!File.Exists(full) || !String.Equals(Path.GetExtension(full), ".cs", StringComparison.OrdinalIgnoreCase) || !unique.Add(full))
                    throw new ArgumentException("Missing, duplicate or non-C# source: " + full);
                lines.Add(Guard.Quote(full)); hashes.Add(Guard.Hash(full) + "  " + full);
            }
            Directory.CreateDirectory(directory);
            string response = Path.Combine(directory, "compile.rsp");
            File.WriteAllLines(response, lines.ToArray(), new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(directory, "inputs.sha256"), hashes.ToArray());
            var toolHashes = new List<string>();
            foreach (string name in new string[] {"csc.exe", "mscorlib.dll", "System.dll", "System.Core.dll", "System.Xml.dll", "System.Xml.Linq.dll", "System.Runtime.Serialization.dll"})
            {
                string file = Path.Combine(Path.GetDirectoryName(FrameworkCompiler), name);
                toolHashes.Add(Guard.Hash(file) + "  " + file);
            }
            File.WriteAllLines(Path.Combine(directory, "toolchain.sha256"), toolHashes.ToArray());
            File.WriteAllText(Path.Combine(directory, "command.txt"), Guard.Quote(FrameworkCompiler) + " /noconfig @" + Guard.Quote(response));
            CompileResult result = Execute(FrameworkCompiler, "/noconfig @" + Guard.Quote(response), directory, timeoutMilliseconds);
            result.Artifact = artifact;
            File.WriteAllText(Path.Combine(directory, "stdout.txt"), result.StandardOutput);
            File.WriteAllText(Path.Combine(directory, "stderr.txt"), result.StandardError);
            File.WriteAllText(Path.Combine(directory, "result.txt"), "ExitCode=" + result.ExitCode + "\nTimedOut=" + result.TimedOut + "\nOutputIncomplete=" + result.OutputIncomplete + "\nTerminationIncomplete=" + result.TerminationIncomplete + "\nCleanupError=" + result.CleanupError);
            foreach (string entry in hashes)
            {
                string full = entry.Substring(66);
                if (Guard.Hash(full) != entry.Substring(0, 64)) throw new IOException("Source changed during compile: " + full);
            }
            return result;
        }
        // No shell, policy changes, installs, devices or Unity startup.
        public static CompileResult Execute(string program, string arguments, string directory, int timeoutMilliseconds)
        {
            program = Guard.CanonicalCPath(program);
            directory = Guard.CanonicalCPath(directory);
            if (!File.Exists(program) || !Directory.Exists(directory)) throw new IOException("Executable/directory missing");
            if (timeoutMilliseconds < 1 || timeoutMilliseconds > 60000) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            var stdout = new StringBuilder(); var stderr = new StringBuilder();
            object gate = new object();
            bool outputEnded = false, errorEnded = false, frozen = false;
            var watch = Stopwatch.StartNew();
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo(program, arguments) {
                    WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true };
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                    lock (gate) { if (frozen) return; if (e.Data == null) outputEnded = true; else stdout.AppendLine(e.Data); } };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                    lock (gate) { if (frozen) return; if (e.Data == null) errorEnded = true; else stderr.AppendLine(e.Data); } };
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                bool finished = process.WaitForExit(Remaining(watch, timeoutMilliseconds));
                var result = new CompileResult { ExitCode = -1, TimedOut = !finished };
                if (!finished)
                {
                    try { process.Kill(); }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception ex) { result.CleanupError = ex.Message; }
                    // Only the directly launched process is owned. Never enumerate or kill others.
                    finished = process.WaitForExit(Remaining(watch, timeoutMilliseconds + CleanupGraceMilliseconds));
                }
                result.TerminationIncomplete = !finished;
                if (finished) result.ExitCode = process.ExitCode;
                // A normally exited parent can leave inherited pipes open too.
                int drainDeadline = result.TimedOut ? timeoutMilliseconds + CleanupGraceMilliseconds : timeoutMilliseconds;
                while (Remaining(watch, drainDeadline) > 0)
                {
                    lock (gate) { if (outputEnded && errorEnded) break; }
                    System.Threading.Thread.Sleep(Math.Min(10, Remaining(watch, drainDeadline)));
                }
                lock (gate)
                {
                    result.OutputIncomplete = !outputEnded || !errorEnded;
                    result.TimedOut |= result.OutputIncomplete;
                    frozen = true;
                    result.StandardOutput = stdout.ToString(); result.StandardError = stderr.ToString();
                }
                // Cancellation marks the Framework async readers cancelled; it does not join them.
                // Process.Dispose closes the process handle without waiting for descendants/EOF.
                try { process.CancelOutputRead(); } catch (InvalidOperationException) { }
                try { process.CancelErrorRead(); } catch (InvalidOperationException) { }
                return result;
            }
        }
        public const int CleanupGraceMilliseconds = 250;
        public const string ApiVersion = "Toolchain/2";
        private static int Remaining(Stopwatch watch, int deadline)
        { return (int)Math.Max(0, deadline - watch.ElapsedMilliseconds); }
    }
}

#endif
