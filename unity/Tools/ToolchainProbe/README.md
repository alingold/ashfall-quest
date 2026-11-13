# Isolated toolchain/conversion probe

Run `unity\Tools\ToolchainProbe\Run-Probe.cmd` from the workspace root. No SDK, package restore, Unity, device or install is needed. Compiler: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (14.8.10001.0, C# 5 only).

Reusable compiler command (PowerShell, workspace root):

```powershell
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /langversion:5 /warn:4 /warnaserror+ /out:unity/Tools/ToolchainProbe/ConversionProbe.exe unity/Tools/ToolchainProbe/ConversionProbe.cs
& ./unity/Tools/ToolchainProbe/ConversionProbe.exe
```

`Run-Probe.ps1` additionally writes compilation and execution logs into the assigned toolchain evidence folder. Script-file execution is disabled by local policy; the `.cmd` entry point avoids any policy change. Initial recorded execution evaluated the saved PowerShell commands in the active shell with the known absolute script directory substituted.

ConversionProbe.cs is an isolated candidate implementation of the frozen coordinate boundary, using row-major matrices and column vectors. It checks forward-axis conversion, round trips, S R S conjugation, scale/shear preservation, 1000 fixed-seed samples, inverse-transpose normals, triangle winding, source index immutability and malformed/nonfinite input rejection. Test geometry is synthetic; it does not establish real map/skin conversion. Near-singular matrices use a fixed 1e-12 determinant rejection threshold; production numeric tolerances still need lead review.

This is C# 5/.NET Framework evidence only. Modern records, nullable references, file-scoped namespaces, SDK projects, UnityEngine APIs, package resolution, editor import, player builds, physics, visuals, XR, network and audio are outside this proof. No shared assemblies, packages, project settings, input assets, releases or source assets are changed.
