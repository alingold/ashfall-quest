"""Exercise the actual pure policy against existing generated Gradle settings."""
from pathlib import Path
import hashlib, subprocess
here=Path(__file__).resolve().parent
root=here.parents[3]
source=root/'unity/Assets/Ashfall/Editor/Playable/R24QuestStreamingCompression.cs'
properties=root/'unity/Library/Bee/Android/Prj/IL2CPP/Gradle/gradle.properties'
before=hashlib.sha256(properties.read_bytes()).hexdigest()
compiler='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
output=here/'CompressionCases.exe'
compile=subprocess.run([compiler,'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(output),str(source),str(here/'CompressionCases.cs')],capture_output=True,text=True)
assert compile.returncode==0,compile.stdout+compile.stderr
run=subprocess.run([str(output),str(properties)],capture_output=True,text=True)
print(run.stdout+run.stderr)
assert run.returncode==0
assert hashlib.sha256(properties.read_bytes()).hexdigest()==before
