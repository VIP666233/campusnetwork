$ErrorActionPreference = 'Stop'
$testRoot = $PSScriptRoot
$projectRoot = Split-Path $testRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$library = Join-Path $testRoot 'NavigationProtocolTests.dll'
& $compiler /nologo /target:library /utf8output "/out:$library" /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Security.dll /reference:System.Web.Extensions.dll (Join-Path $projectRoot 'src\BrowserProtocol.cs') (Join-Path $projectRoot 'src\LoopbackWebSocket.cs') (Join-Path $projectRoot 'src\Settings.cs') (Join-Path $projectRoot 'src\PortalRedirect.cs') (Join-Path $testRoot 'NavigationProtocolTests.cs') (Join-Path $testRoot 'LoopbackTransportTests.cs') (Join-Path $testRoot 'PortalRedirectTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Protocol test compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($library)
$results = [CampusNet.NavigationProtocolTests]::RunAll()
$results += [CampusNet.LoopbackTransportTests]::RunAll()
$results += [CampusNet.PortalRedirectTests]::RunAll()
$results | Set-Content -LiteralPath (Join-Path $testRoot 'protocol-test-results.txt') -Encoding UTF8
$results
