# Read-only metadata inspection. No Unity/Hub/license/device commands or contents.
$ErrorActionPreference = 'Stop'
$paths = @('C:\Program Files\Unity','C:\Program Files\Unity Hub',
  "$env:APPDATA\UnityHub\editors.json", "$env:APPDATA\UnityHub\secondaryInstallPath.json",
  "$env:APPDATA\UnityHub\installations.json", "$env:LOCALAPPDATA\Unity", "$env:APPDATA\Unity",
  "$env:PROGRAMDATA\Unity", "$env:PROGRAMDATA\Unity\Unity_lic.ulf", "$env:USERPROFILE\.unity3d")
$uninstalls = @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
  'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*')
[ordered]@{
  observedUtc = [DateTime]::UtcNow.ToString('o')
  paths = @($paths | ForEach-Object { [ordered]@{ path=$_; exists=(Test-Path -LiteralPath $_) } })
  uninstallEntries = @($uninstalls | ForEach-Object { Get-ItemProperty $_ -ErrorAction SilentlyContinue } |
    Where-Object { $_.DisplayName -match 'Unity' } | Select-Object DisplayName,DisplayVersion,InstallLocation)
  unityRegistryKeys = @('HKCU:\Software\Unity','HKCU:\Software\Unity Technologies',
    'HKLM:\Software\Unity Technologies','HKLM:\Software\WOW6432Node\Unity Technologies') |
    ForEach-Object { [ordered]@{ path=$_; exists=(Test-Path -LiteralPath $_) } }
  processes = @(Get-Process | Where-Object { $_.ProcessName -match 'Unity|Hub' } | Select-Object ProcessName,Id)
  policy = @(Get-ExecutionPolicy -List | Select-Object Scope,ExecutionPolicy)
  drives = @(Get-PSDrive -Name C,D | Select-Object Name,Free,Used)
  compiler = Get-Item 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' |
    Select-Object FullName,@{Name='Version';Expression={$_.VersionInfo.FileVersion}}
} | ConvertTo-Json -Depth 6
