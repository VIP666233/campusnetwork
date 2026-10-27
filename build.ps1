param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
if (!(Test-Path -LiteralPath $compiler)) { throw '需要 Windows 自带的 .NET Framework 4.x 编译器。' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$exe = Join-Path $OutputDirectory '校园网自动登录.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | ForEach-Object { $_.FullName })
& $compiler /nologo /utf8output /target:winexe /platform:anycpu /optimize+ "/out:$exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Security.dll /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll "/resource:$PSScriptRoot/src/Portal.js,Portal.js" "/resource:$PSScriptRoot/src/assets/App.ico,App.ico" "/resource:$PSScriptRoot/src/assets/Logo.png,Logo.png" "/win32icon:$PSScriptRoot/src/assets/App.ico" $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/app.config') -Destination ($exe + '.config') -Force
Write-Output $exe
