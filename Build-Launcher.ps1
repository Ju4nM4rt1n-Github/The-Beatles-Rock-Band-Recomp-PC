param([string]$OutputDirectory,[string]$GameBuildDirectory,[string]$VsInstallDirectory,[string]$CrtDirectory,[string]$VcpkgInstalledDirectory)
$ErrorActionPreference='Stop'
if(-not $OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot 'out/BeatlesRockBand-PC'}
if(-not $GameBuildDirectory){$GameBuildDirectory=Join-Path $PSScriptRoot 'out/build/game'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$GameBuildDirectory=[IO.Path]::GetFullPath($GameBuildDirectory)
$VcpkgInstalledDirectory=if($VcpkgInstalledDirectory){[IO.Path]::GetFullPath($VcpkgInstalledDirectory)}else{Join-Path $env:TEMP 'BeatlesRockBand-vcpkg'}
$VcpkgInstalledDirectory=Join-Path $VcpkgInstalledDirectory 'x64-windows'
$binaries=@('beatlesrockband-guitar.exe','rexruntime.dll','rexgpu-xenos.dll','beatles-input.dll','beatles-content.exe')
foreach($name in $binaries){if(-not (Test-Path -LiteralPath (Join-Path $GameBuildDirectory $name))){throw ('Missing native build file: '+$name+'. Run Build-Game.ps1 or pass -GameBuildDirectory.')}}
foreach($name in @('avcodec-63.dll','avutil-61.dll')){if(-not (Test-Path -LiteralPath (Join-Path $VcpkgInstalledDirectory ('bin/'+$name)))){throw ('Missing FFmpeg runtime '+$name+'. Build with the pinned vcpkg manifest first.')}}
if(-not $CrtDirectory){
 . (Join-Path $PSScriptRoot 'scripts/DevTools.ps1')
 $tools=Get-TbrbDevTools -VsInstallDirectory $VsInstallDirectory
 $redist=Join-Path $tools.Vs 'VC/Redist/MSVC'
 $versions=@(Get-ChildItem -LiteralPath $redist -Directory | Where-Object {$_.Name -match '^\d+\.\d+\.\d+$'} | Sort-Object {[version]$_.Name} -Descending)
 foreach($version in $versions){
  $candidate=Get-ChildItem -LiteralPath (Join-Path $version.FullName 'x64') -Directory -Filter 'Microsoft.VC*.CRT' -ErrorAction SilentlyContinue | Select-Object -First 1
  if($candidate -and (Test-Path -LiteralPath (Join-Path $candidate.FullName 'vcruntime140.dll'))){$CrtDirectory=$candidate.FullName;break}
 }
 if(-not $CrtDirectory){throw 'Visual C++ x64 redistributable files not found; pass -CrtDirectory.'}
}
$runtime=Join-Path $OutputDirectory 'runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$src=Join-Path $PSScriptRoot 'launcher-src'
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(-not (Test-Path -LiteralPath $csc)){throw 'The .NET Framework 4.x C# compiler was not found.'}
$compilerArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',('/out:'+(Join-Path $OutputDirectory 'BeatlesRockBandLauncher.exe')),('/win32manifest:'+(Join-Path $src 'launcher.manifest')),('/win32icon:'+(Join-Path $src 'launcher.ico')),'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Runtime.Serialization.dll')
foreach($name in @('LauncherCore.cs','LauncherApp.cs','Controls.cs','ControlsForm.cs','AeroTheme.cs','UiKit.cs','ControllerDevices.cs','Responsive.cs','DlcInstaller.cs','DlcForm.cs')){$compilerArgs+=Join-Path $src $name}
$compilerArgs+='/resource:'+(Join-Path $src 'assets/DMSerifDisplay-Regular.ttf')+',BeatlesLauncher.Display.ttf'
$compilerArgs+='/resource:'+(Join-Path $src 'assets/cover.jpg')+',BeatlesLauncher.Cover.jpg'
foreach($name in @('guitar','bass','drums','vocals')){$compilerArgs+='/resource:'+(Join-Path $src ('assets/instrument-'+$name+'.png'))+(',BeatlesLauncher.Instrument.'+$name+'.png')}
& $csc @compilerArgs
if($LASTEXITCODE){throw 'Launcher compilation failed.'}
$playArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',('/out:'+(Join-Path $OutputDirectory 'BeatlesRockBand-Play.exe')),('/win32manifest:'+(Join-Path $src 'launcher.manifest')),('/win32icon:'+(Join-Path $src 'launcher.ico')),'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Runtime.Serialization.dll')
foreach($name in @('LauncherCore.cs','Controls.cs','ControllerDevices.cs','PlayDirect.cs')){$playArgs+=Join-Path $src $name}
& $csc @playArgs
if($LASTEXITCODE){throw 'Direct play executable compilation failed.'}
Copy-Item -LiteralPath (Join-Path $src 'BeatlesRockBandLauncher.exe.config') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $src 'BeatlesRockBandLauncher.exe.config') -Destination (Join-Path $OutputDirectory 'BeatlesRockBand-Play.exe.config') -Force
foreach($name in $binaries){Copy-Item -LiteralPath (Join-Path $GameBuildDirectory $name) -Destination $runtime -Force}
foreach($name in @('avcodec-63.dll','avutil-61.dll')){Copy-Item -LiteralPath (Join-Path $VcpkgInstalledDirectory ('bin/'+$name)) -Destination $runtime -Force}
foreach($dll in Get-ChildItem -LiteralPath $CrtDirectory -File -Filter '*.dll'){Copy-Item -LiteralPath $dll.FullName -Destination $runtime -Force}
foreach($name in @('launcher.ico','game-files.json')){Copy-Item -LiteralPath (Join-Path $src $name) -Destination $OutputDirectory -Force}
$lines=@('# Portable defaults. Data paths are supplied by the launcher.')+@(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'beatlesrockband.toml') -Encoding UTF8 | Where-Object {$_ -notmatch '^\s*(game_data_root|user_data_root|cache_root|log_file)\s*=' -and $_ -notmatch '^# Portable defaults'})
[IO.File]::WriteAllText((Join-Path $runtime 'beatlesrockband.toml'),(($lines -join [Environment]::NewLine)+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
$licenses=Join-Path $OutputDirectory 'licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $VcpkgInstalledDirectory 'share/ffmpeg/copyright') -Destination (Join-Path $licenses 'FFmpeg-LICENSE.txt') -Force
foreach($name in @('ReXGlue-LICENSE.txt','SDL3-LICENSE.txt','DMSerifDisplay-OFL.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('licenses/'+$name)) -Destination $licenses -Force}
Write-Output ('Portable application: '+$OutputDirectory)
$standardOutput=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'out/BeatlesRockBand-PC'))
if([String]::Equals($OutputDirectory,$standardOutput,[StringComparison]::OrdinalIgnoreCase)){
 $shell=New-Object -ComObject WScript.Shell
 foreach($shortcut in @(
  @{Name='The Beatles Rock Band Launcher.lnk';Target='BeatlesRockBandLauncher.exe';Description='Start The Beatles: Rock Band launcher'},
  @{Name='The Beatles Rock Band Play.lnk';Target='BeatlesRockBand-Play.exe';Description='Start the game with the latest saved launcher settings'}
 )){
  $link=$shell.CreateShortcut((Join-Path $PSScriptRoot $shortcut.Name))
  $link.TargetPath=Join-Path $OutputDirectory $shortcut.Target
  $link.WorkingDirectory=$OutputDirectory
  $link.IconLocation=(Join-Path $OutputDirectory 'launcher.ico')+',0'
  $link.Description=$shortcut.Description
  $link.Save()
 }
}
