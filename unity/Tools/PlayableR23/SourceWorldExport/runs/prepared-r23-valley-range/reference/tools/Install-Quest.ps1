param([string]$Serial='', [switch]$VerifyOnly, [switch]$PlanOnly)
$ErrorActionPreference='Stop'
$bundleRoot=$PSScriptRoot
if(-not(Test-Path -LiteralPath (Join-Path $bundleRoot 'PACKAGE.json'))){throw 'Run this script from the extracted Quest delivery folder.'}
$manifest=Get-Content -LiteralPath (Join-Path $bundleRoot 'PACKAGE.json') -Raw|ConvertFrom-Json
$package=[string]$manifest.android_package
if($package -notmatch '^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$'){throw 'Invalid app package in manifest.'}
if($package-ne'org.alingold.ashfallquest'){throw 'This installer only targets the separate ASHFALL R16 package.'}
$apkFiles=@($manifest.files|Where-Object{$_.kind-eq'apk'-and$_.path-eq$manifest.apk})
$mapFiles=@($manifest.files|Where-Object{$_.kind-eq'map-pack'})
if($apkFiles.Count-ne1-or$mapFiles.Count-ne2){throw 'Manifest must contain the verified APK and both map packs.'}
$adb=Join-Path $bundleRoot 'platform-tools/adb.exe'
function Get-PayloadHash([string]$Path){
 $digest=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($Path)
 try{return ([BitConverter]::ToString($digest.ComputeHash($stream))).Replace('-','').ToLowerInvariant()}
 finally{$stream.Dispose();$digest.Dispose()}
}
Write-Host 'ASHFALL R16 Quest installer - APK plus all 105 archive maps.'
Write-Host 'Checking the download before accessing a headset...'
foreach($file in $manifest.files){
 $path=Join-Path $bundleRoot $file.path
 $absolute=[IO.Path]::GetFullPath($path)
 if(-not$absolute.StartsWith([IO.Path]::GetFullPath($bundleRoot)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest path leaves the delivery folder.'}
 if(-not(Test-Path -LiteralPath $path)){throw ('Missing '+$file.path+'. Extract the entire delivery folder first.')}
 if((Get-Item -LiteralPath $path).Length-ne[long]$file.bytes){throw ('Wrong size: '+$file.path)}
 if((Get-PayloadHash $path)-ne$file.sha256){throw ('Checksum failed: '+$file.path)}
 Write-Host ('Verified '+$file.path)
}
if($VerifyOnly){Write-Host 'PAYLOAD_VERIFIED - no headset commands were executed.';exit 0}
if($PlanOnly){
 Write-Host ('PLAN: select one authorized USB device; install '+$manifest.apk+' as '+$package+'; copy and verify both packs in files/map-packs; no uninstall, force-stop or launch.')
 exit 0
}
if(-not(Test-Path -LiteralPath $adb)){throw 'Bundled platform-tools/adb.exe is missing.'}
if($Serial -and $Serial -notmatch '^[A-Za-z0-9._:-]+$'){throw 'Invalid device serial.'}
$devices=& $adb devices
if($LASTEXITCODE-ne0){throw 'ADB could not list devices.'}
$authorized=@($devices|ForEach-Object {if($_ -match '^([A-Za-z0-9._:-]+)\s+device\s*$'){$matches[1]}})
if(-not$Serial){
 if($authorized.Count-ne1){throw 'Connect one Quest by USB and approve USB debugging inside the headset, then run again. For multiple devices, use -Serial DEVICE_ID.'}
 $Serial=$authorized[0]
}
if($Serial -notin $authorized){throw 'The selected device is not authorized. Approve USB debugging inside the headset.'}
function Invoke-Adb([string[]]$CommandArgs){
 & $adb -s $Serial @CommandArgs
 if($LASTEXITCODE-ne0){throw ('ADB failed: '+($CommandArgs -join ' ')+'. No other app has been uninstalled or launched.')}
}
$deviceName=Invoke-Adb @('shell','getprop','ro.product.model')
Write-Host ('Selected device: '+($deviceName -join ' ')+' / '+$Serial)
$storage=Invoke-Adb @('shell','df','-k','/data')
$storageRows=@($storage|Where-Object{$_-match '^\S+\s+\d+\s+\d+\s+(\d+)\s+'})
if($storageRows.Count){
 $match=[regex]::Match($storageRows[-1],'^\S+\s+\d+\s+\d+\s+(\d+)\s+')
 if([long]$match.Groups[1].Value-lt12*1024*1024){throw 'Keep at least 12 GB free on the Quest for installation and map-copy staging. Free space yourself and rerun.'}
}
Write-Host 'Installing the separate ASHFALL R16 Quest app. Existing app data is retained on updates.'
Invoke-Adb @('install','-r',(Join-Path $bundleRoot $manifest.apk))
Invoke-Adb @('shell','run-as',$package,'mkdir','-p','files/map-packs')
foreach($file in @($manifest.files|Where-Object{$_.kind-eq'map-pack'})){
 $name=[IO.Path]::GetFileName($file.path)
 if($name -notmatch '^[A-Za-z0-9.-]+\.pck$'){throw 'Invalid map-pack filename.'}
 $target='files/map-packs/'+$name
 $probe=[Diagnostics.ProcessStartInfo]::new();$probe.FileName=$adb;$probe.UseShellExecute=$false;$probe.CreateNoWindow=$true;$probe.WindowStyle='Hidden';$probe.RedirectStandardOutput=$true;$probe.RedirectStandardError=$true
 $probe.Arguments=(@('-s',$Serial,'shell','run-as',$package,'sha256sum',$target)|ForEach-Object{'"'+$_+'"'})-join' '
 $query=[Diagnostics.Process]::Start($probe);$queryOut=$query.StandardOutput.ReadToEndAsync();$queryError=$query.StandardError.ReadToEndAsync();$query.WaitForExit()
 if($query.ExitCode-eq0-and$queryOut.Result-match('^'+[regex]::Escape($file.sha256)+'\s')){Write-Host ('Already installed: '+$name);continue}
 $stage='/data/local/tmp/ashfall-r16-'+$name+'.upload'
 Write-Host ('Copying '+$name+' - this can take several minutes. Keep USB connected.')
 Invoke-Adb @('push',(Join-Path $bundleRoot $file.path),$stage)
 Invoke-Adb @('shell','chmod','644',$stage)
 Invoke-Adb @('shell','run-as',$package,'cp',$stage,($target+'.part'))
 $digest=Invoke-Adb @('shell','run-as',$package,'sha256sum',($target+'.part'))
 if(($digest -join ' ')-notmatch ('^'+[regex]::Escape($file.sha256)+'\s')){throw ('Device checksum failed: '+$name+'. Rerun the installer; the previous complete file was not replaced.')}
 Invoke-Adb @('shell','run-as',$package,'mv',($target+'.part'),$target)
 Invoke-Adb @('shell','rm','-f',$stage)
 Write-Host ('Installed and verified '+$name)
}
Write-Host ''
Write-Host 'INSTALLATION_OK - APK and all map packs verified.'
Write-Host 'Unplug USB, open Unknown Sources, and choose ASHFALL R16 Quest.'
Write-Host 'The installer has not launched the app. Initialize remains a manual in-game action.'
