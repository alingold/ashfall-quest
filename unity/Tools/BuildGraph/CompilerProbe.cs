using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Modules.Toolchain;

// Owned CLI adapter to the exact accepted Toolchain/2, never the old wrappers.
public static class CompilerProbe
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3 && args.Length != 4) throw new ArgumentException("root fresh-child source-list [reference-list]");
            if (CompilerRunner.ApiVersion != "Toolchain/2") throw new InvalidOperationException("API drift");
            if (args.Length == 4) return Separate(args);
            var runner = new CompilerRunner(args[0], 30000);
            CompileResult result = runner.Compile(File.ReadAllLines(args[2]), args[1], false);
            Console.Write(result.StandardOutput); Console.Error.Write(result.StandardError);
            return result.Success ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 2; }
    }
    private static int Separate(string[] args)
    {
        string root = Guard.CanonicalCPath(args[0]);
        string output = Guard.CanonicalCPath(args[1]);
        if (!output.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) || Directory.Exists(output) || File.Exists(output))
            throw new IOException("Fresh owned child required");
        var lines = new List<string> { "/nologo", "/langversion:5", "/warn:4", "/warnaserror+", "/target:library" };
        var hashes = new List<string>();
        string artifact = Path.Combine(output, Path.GetFileName(output).Replace("separate-", "") + ".dll");
        lines.Add("/out:" + Guard.Quote(artifact));
        foreach (string source in File.ReadAllLines(args[2]))
        {
            string path = Guard.CanonicalCPath(source);
            lines.Add(Guard.Quote(path)); hashes.Add(Guard.Hash(path) + "  " + path);
        }
        var refs = new List<string>(File.ReadAllLines(args[3]));
        foreach (string name in new string[] { "System", "System.Core", "System.Xml", "System.Xml.Linq", "System.Runtime.Serialization" })
            refs.Add(Path.Combine(Path.GetDirectoryName(CompilerRunner.FrameworkCompiler), name + ".dll"));
        foreach (string reference in refs)
        {
            string path = Guard.CanonicalCPath(reference);
            lines.Add("/reference:" + Guard.Quote(path)); hashes.Add(Guard.Hash(path) + "  " + path);
        }
        hashes.Add(Guard.Hash(CompilerRunner.FrameworkCompiler) + "  " + CompilerRunner.FrameworkCompiler);
        Directory.CreateDirectory(output);
        string rsp = Path.Combine(output, "compile.rsp");
        File.WriteAllLines(rsp, lines);
        File.WriteAllLines(Path.Combine(output, "inputs.sha256"), hashes);
        string command = "/noconfig @" + Guard.Quote(rsp);
        File.WriteAllText(Path.Combine(output, "command.txt"), Guard.Quote(CompilerRunner.FrameworkCompiler) + " " + command);
        CompileResult result = CompilerRunner.Execute(CompilerRunner.FrameworkCompiler, command, output, 30000);
        File.WriteAllText(Path.Combine(output, "stdout.txt"), result.StandardOutput);
        File.WriteAllText(Path.Combine(output, "stderr.txt"), result.StandardError);
        File.WriteAllText(Path.Combine(output, "result.txt"), "ExitCode=" + result.ExitCode + "\nTimedOut=" + result.TimedOut + "\nOutputIncomplete=" + result.OutputIncomplete + "\nTerminationIncomplete=" + result.TerminationIncomplete);
        foreach (string entry in hashes)
            if (Guard.Hash(entry.Substring(66)) != entry.Substring(0, 64)) throw new IOException("Input drift");
        Console.Write(result.StandardOutput); Console.Error.Write(result.StandardError);
        return result.ExitCode == 0 && !result.TimedOut && !result.OutputIncomplete && !result.TerminationIncomplete && File.Exists(artifact) ? 0 : 1;
    }
}
