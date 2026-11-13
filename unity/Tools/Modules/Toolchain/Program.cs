using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Modules.Toolchain;

namespace Ashfall.Modules.Toolchain.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length < 2 || args.Length > 3) throw new ArgumentException("Usage: Toolchain.exe <workspace> <discover|core|all|ModuleName> [existing-probe-source.cs]");
                string workspace = Guard.CanonicalCPath(args[0]);
                string output = Path.Combine(workspace, @"campaign-staging\20261104-three-chats\unity\Toolchain\r2");
                var runner = new CompilerRunner(output, 30000);
                string modules = Path.Combine(workspace, @"unity\Assets\Ashfall\Modules");
                string core = Path.Combine(workspace, @"unity\Assets\Ashfall\Core\Contracts.cs");
                var discovery = new ModuleDiscovery();
                string[] directories = Directory.GetDirectories(modules);
                Array.Sort(directories, StringComparer.Ordinal);
                if (args[1] == "discover")
                {
                    foreach (string module in directories)
                        Console.WriteLine(Path.GetFileName(module) + "\t" + discovery.Discover(module).Length);
                    return 0;
                }
                var sources = new List<string>(); sources.Add(core);
                if (args[1] != "core")
                {
                    if (args[1] == "all")
                    {
                        foreach (string module in directories) sources.AddRange(discovery.Discover(module));
                    }
                    else
                    {
                        if (args[1].IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || args[1] == "." || args[1] == "..") throw new ArgumentException("Invalid module name");
                        string module = Path.Combine(modules, args[1]);
                        string[] found = discovery.Discover(module);
                        if (found.Length == 0) throw new ArgumentException("No neutral module sources");
                        sources.AddRange(found);
                    }
                }
                if (args.Length == 3) sources.Add(Guard.CanonicalCPath(args[2]));
                string run = Path.Combine(output, "run-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
                CompileResult result = runner.Compile(sources.ToArray(), run, args.Length == 3);
                Console.Write(result.StandardOutput); Console.Error.Write(result.StandardError);
                Console.WriteLine("Evidence: " + run);
                if (!result.Success) return 1;
                Console.WriteLine("COMPILE PASS: " + sources.Count + " sources; " + Guard.Hash(result.Artifact));
                if (args.Length == 3)
                {
                    CompileResult execution = CompilerRunner.Execute(result.Artifact, "", run, 30000);
                    File.WriteAllText(Path.Combine(run, "run-stdout.txt"), execution.StandardOutput);
                    File.WriteAllText(Path.Combine(run, "run-stderr.txt"), execution.StandardError);
                    File.WriteAllText(Path.Combine(run, "run-result.txt"), "ExitCode=" + execution.ExitCode + "\nTimedOut=" + execution.TimedOut);
                    Console.Write(execution.StandardOutput); Console.Error.Write(execution.StandardError);
                    return execution.ExitCode == 0 && !execution.TimedOut ? 0 : 1;
                }
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 2; }
        }
    }
}
