param([string]$RunName = ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')))
$ErrorActionPreference = 'Stop'
# Fresh r2 output directory; Python runner refuses to overwrite prior evidence.
& 'C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' (Join-Path $PSScriptRoot 'run_r2.py') $RunName
if ($LASTEXITCODE -ne 0) { throw 'WeaponHandling r2 compile/tests failed' }
