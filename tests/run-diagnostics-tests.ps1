$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
$output=Join-Path $projectRoot '.build/diagnostics-check'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
# Render actual UI using defaults; never load the user's real account settings.
$main=[IO.File]::ReadAllText((Join-Path $projectRoot 'src/MainForm.cs')).Replace('ReadSettings();','changes.Accept(EditValues());').Replace('Visible = true','Visible = false')
[IO.File]::WriteAllText((Join-Path $output 'MainForm.cs'),$main,[Text.UTF8Encoding]::new($true))
$sources=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | Where-Object {$_.Name -ne 'MainForm.cs'} | ForEach-Object {$_.FullName}) + @((Join-Path $output 'MainForm.cs'))
$references=@('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Security.dll','/reference:System.Web.Extensions.dll','/reference:System.Net.Http.dll')
$resources=@("/resource:$projectRoot/src/Portal.js,Portal.js","/resource:$projectRoot/src/Inspect.js,Inspect.js","/resource:$projectRoot/src/assets/App.ico,App.ico","/resource:$projectRoot/src/assets/Logo.png,Logo.png")
& $compiler /nologo /utf8output /target:exe /main:DiagnosticsUiCheck "/out:$output/UiCheck.exe" $references $resources $sources (Join-Path $PSScriptRoot 'DiagnosticsUiCheck.cs')
if($LASTEXITCODE -ne 0){throw 'UI check compilation failed'}
& (Join-Path $output 'UiCheck.exe') (Join-Path $output 'advanced-settings.png')
if($LASTEXITCODE -ne 0){throw 'UI checks failed'}
& $compiler /nologo /utf8output /target:exe /main:DiagnosticTests "/out:$output/DiagnosticTests.exe" $references $resources $sources (Join-Path $PSScriptRoot 'DiagnosticTests.cs')
if($LASTEXITCODE -ne 0){throw 'Browser check compilation failed'}
& (Join-Path $output 'DiagnosticTests.exe') $output
if($LASTEXITCODE -ne 0){throw 'Browser checks failed'}
