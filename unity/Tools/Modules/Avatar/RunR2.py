"""Direct compiler runner; no PowerShell policy changes. Refuses a frozen output."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / "campaign-staging/20261104-three-chats/unity/Avatar/r2"
EVIDENCE = ROOT / "docs/agent-runs/20261104-three-chats/unity/Avatar/evidence/r2"
if (EVIDENCE / "candidate-manifest.json").exists():
    raise SystemExit("r2 is frozen: allocate a fresh revision rather than overwrite it")
OUT.mkdir(parents=True, exist_ok=True)
EVIDENCE.mkdir(parents=True, exist_ok=True)
compiler = Path("C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe")
sources = [ROOT / "unity/Assets/Ashfall/Core/Contracts.cs"]
sources += sorted((ROOT / "unity/Assets/Ashfall/Modules/Avatar").rglob("*.cs"))
for test in ("AvatarTests", "AvatarR2Tests"):
    exe = OUT / (test + ".exe")
    args = [str(compiler), "/nologo", "/langversion:5", "/warnaserror+", "/target:exe", "/out:" + str(exe)]
    args += [str(p) for p in sources] + [str(Path(__file__).parent / (test + ".cs"))]
    with (EVIDENCE / (test + ".txt")).open("w", encoding="utf-8") as log:
        log.write("COMMAND " + " ".join('"' + a + '"' for a in args) + "\n")
        log.flush()
        result = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        log.write(result.stdout + "\nCOMPILE EXIT " + str(result.returncode) + "\n")
        if result.returncode:
            raise SystemExit(result.stdout)
        log.write('COMMAND "' + str(exe) + '"\n')
        result = subprocess.run([str(exe)], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        log.write(result.stdout + "\nRUN EXIT " + str(result.returncode) + "\n")
        print(result.stdout)
        if result.returncode:
            raise SystemExit(result.returncode)
