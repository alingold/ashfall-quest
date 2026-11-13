$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$evidence = Join-Path $root 'docs/agent-runs/20261104-0020/lead-u/toolchain'
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$binary = Join-Path $PSScriptRoot 'ConversionProbe.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw "Missing compiler: $compiler" }
$arguments = @('/nologo','/target:exe','/langversion:5','/warn:4','/warnaserror+',('/out:' + $binary),(Join-Path $PSScriptRoot 'ConversionProbe.cs'))
('Compiler: ' + $compiler + "`nArguments: " + ($arguments -join ' ')) | Set-Content (Join-Path $evidence 'compile.log')
& $compiler @arguments 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'compile.log') -Append
$compileExit = $LASTEXITCODE
"Compiler exit: $compileExit" | Add-Content (Join-Path $evidence 'compile.log')
if ($compileExit -ne 0) { throw "Compilation failed: $compileExit" }
& $binary 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'run.log')
$runExit = $LASTEXITCODE
"Executable exit: $runExit" | Add-Content (Join-Path $evidence 'run.log')
if ($runExit -ne 0) { throw "Probe failed: $runExit" }
