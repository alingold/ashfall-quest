# Neutral Toolchain candidate

From any working directory, run the absolute command:

```powershell
& 'C:\Users\aling\Documents\ashfall\unity\Tools\Modules\Toolchain\Run.cmd' discover
& 'C:\Users\aling\Documents\ashfall\unity\Tools\Modules\Toolchain\Run.cmd' core
& 'C:\Users\aling\Documents\ashfall\unity\Tools\Modules\Toolchain\Run.cmd' Toolchain
& 'C:\Users\aling\Documents\ashfall\unity\Tools\Modules\Toolchain\Run.cmd' all
& 'C:\Users\aling\Documents\ashfall\unity\Tools\Modules\Toolchain\Test.cmd'
```

To compile and execute an explicitly selected neutral probe, append its quoted absolute .cs path to `core`, `all` or a module name. For example:

```powershell
& 'C:\Users\aling\Documents\ashfall\unity\Tools\Modules\Toolchain\Run.cmd' core 'C:\Users\aling\Documents\ashfall\unity\Tools\ToolchainProbe\ConversionProbe.cs'
```

`Run.cmd` bootstraps the owned CLI using the absolute Framework64 compiler. It never runs the old script, edits the old probe or changes execution policy. The CLI includes frozen Core plus neutral Assets module files; it recursively excludes Engine/ and Tests/. It does not discover Tools tests or automatically execute unknown module entry points. `all` compiles one combined assembly to catch cross-module name conflicts. Discovery lists observed directories, so compare its list against the central ownership.json; an empty or unfinished module is not parity coverage.

Every compile uses a fresh C: Toolchain staging child with quoted absolute response-file arguments, C#5, warnings as errors and explicit Framework references (System, System.Core, System.Xml, System.Xml.Linq, System.Runtime.Serialization; mscorlib implicit). `/noconfig` is on the command line. Inputs, compiler/reference hashes, arguments, stdout, stderr and exit/timeout status are saved. Sources are rehashed after compilation to detect concurrent edits. This is repeatable invocation and evidence, not deterministic binary bytes: Framework csc timestamps/identities can differ between builds.

Owned APIs: `ICompilerRunner.Compile(string[], string, bool)`, `IModuleDiscovery.Discover(string)`, `CompilerRunner.Execute(string,string,string,int)`. A caller must supply an owned output root. The compiler refuses existing run directories, output escapes, linked ancestors and duplicate/missing/non-C# inputs. Toolchain/2 rejects ambiguous paths before normalization. All input, output, executable, discovery and working-directory paths must be canonical fully qualified C:\ paths (UNC, device paths, forward slashes, relative roots and drive-relative names are refused). Output paths must be fresh descendants of the caller-supplied owned root. Execute accepts only absolute executable paths and runs directly without a shell, with a 1–60000 ms process deadline. CompileResult adds OutputIncomplete, TerminationIncomplete and CleanupError; Success also rejects incomplete drain/termination. A drain deadline reports TimedOut even if the direct parent exited with code zero. Complete diagnostic lines received before cancellation are retained; an unfinished line can be absent when OutputIncomplete is true. Framework asynchronous readers are cancelled without joining them; inherited handles can remain alive in descendants until those descendants exit. It is intended for trusted direct probes; it does not isolate malicious executables or manage descendant process trees. Bootstrap and compile outputs use fresh directories under campaign-staging/20261104-three-chats/unity/Toolchain/r2; existing r1 binaries and evidence are preserved.

`Inspect.ps1` is read-only metadata inspection and emits JSON to stdout. Where script-file policy blocks it, its reviewed content can be evaluated in the existing PowerShell session without changing policy:

```powershell
& ([scriptblock]::Create((Get-Content -LiteralPath 'C:\Users\aling\Documents\ashfall\unity\Tools\Modules\Toolchain\Inspect.ps1' -Raw)))
```

Both Assets sources are guarded by UNITY_EDITOR || !UNITY_6000_0_OR_NEWER. Neutral and editor compiles retain the API; player-symbol compiles omit it. This is source/compiler evidence; actual Unity assembly membership remains to be verified centrally. No shared integration signatures are missing for neutral use. Unity import, package resolution, license validity, Windows/Quest builds and devices remain unverified.
