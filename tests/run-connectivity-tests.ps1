$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$output = Join-Path $PSScriptRoot 'ConnectivityTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /utf8output /target:exe /main:ConnectivityTests "/out:$output" /reference:System.dll /reference:System.Core.dll /reference:System.Net.Http.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Security.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'ConnectivityTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Connectivity tests did not compile.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Connectivity tests failed.' }
