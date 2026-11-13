$ErrorActionPreference='Stop'
& 'C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' (Join-Path $PSScriptRoot 'Run-R2.py')
if($LASTEXITCODE -ne 0) { throw 'Networking r2 verification failed' }
