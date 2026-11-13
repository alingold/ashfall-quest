"""Compiler/CPU-only MapNativeAppearanceR2 entrypoint; never launches Unity."""
import pathlib,subprocess,sys
ROOT=pathlib.Path(__file__).resolve().parents[4]
if len(sys.argv)<2:
    raise SystemExit("Usage: run_checks.py <fresh-output-tag> [--compile-only]")
script=ROOT/"campaign-staging/20261104-three-chats/unity/MapNativeAppearanceR2/check.py"
raise SystemExit(subprocess.call([sys.executable,"-B",str(script)]+sys.argv[1:]))
