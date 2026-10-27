$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$library = Join-Path $PSScriptRoot 'LoginFlowTests.dll'
$template = Join-Path $PSScriptRoot 'FlowTemplate.js'
Set-Content -LiteralPath $template -Value '__CONFIG__' -NoNewline -Encoding ASCII
& $compiler /nologo /target:library /utf8output "/out:$library" /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll "/resource:$template,Portal.js" (Join-Path $projectRoot 'src\PortalRedirect.cs') (Join-Path $projectRoot 'src\LoginResult.cs') (Join-Path $projectRoot 'src\LoginService.cs') (Join-Path $PSScriptRoot 'LoginFlowTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Login flow test compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($library)
$results = [CampusNet.LoginFlowTests]::RunAll()
$results | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'flow-test-results.txt') -Encoding UTF8
$results
