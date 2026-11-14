$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$outputRoot = Join-Path $projectRoot '.build/success-timing'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$exe = Join-Path $outputRoot 'SuccessTimingChecks.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File | ForEach-Object { $_.FullName })
$harness = Join-Path $PSScriptRoot 'SuccessTimingChecks.cs'
& $compiler /nologo /utf8output /target:exe '/main:SuccessTimingChecks' "/out:$exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Security.dll /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll "/resource:$projectRoot/src/Portal.js,Portal.js" $sources $harness
if ($LASTEXITCODE -ne 0) { throw 'Timing test compilation failed.' }
& $exe (Join-Path $outputRoot 'session')
if ($LASTEXITCODE -ne 0) { throw 'Timing test failed.' }
