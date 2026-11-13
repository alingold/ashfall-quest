"""MapPreviewIntegrityR3 compiler/CPU gate. Never launches an engine."""
import pathlib,subprocess,sys
root=pathlib.Path(__file__).resolve().parents[4]
if len(sys.argv)<2:raise SystemExit('Usage: run_integrity_r3.py <fresh-tag> [--full]')
raise SystemExit(subprocess.call([sys.executable,'-B',str(root/'campaign-staging/20261104-three-chats/unity/MapPreviewIntegrityR3/check.py')]+sys.argv[1:]))
