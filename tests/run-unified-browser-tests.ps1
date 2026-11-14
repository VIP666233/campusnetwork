$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
& (Join-Path $projectRoot 'build-portal.ps1')
$outputRoot = Join-Path $projectRoot '.build/unified-browser-tests'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$exe = Join-Path $outputRoot 'UnifiedBrowserTests.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File | ForEach-Object { $_.FullName })
$harness = Join-Path $PSScriptRoot 'UnifiedBrowserTests.cs'
& $compiler /nologo /utf8output /target:exe '/main:UnifiedBrowserTests' "/out:$exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Security.dll /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll "/resource:$projectRoot/src/Portal.js,Portal.js" $sources $harness
if ($LASTEXITCODE -ne 0) { throw 'Unified test compilation failed.' }
& $exe (Join-Path $outputRoot 'session') (Join-Path $PSScriptRoot 'fixtures/image-login-portal.html')
if ($LASTEXITCODE -ne 0) { throw 'Unified browser checks failed.' }
