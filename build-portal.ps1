$ErrorActionPreference = 'Stop'
$template = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/PortalRouter.js'))
foreach ($name in @('Classic','Huadian','Enhanced')) {
    $adapter = [IO.File]::ReadAllText((Join-Path $PSScriptRoot ('src/adapters/' + $name + '.js'))).Trim().TrimEnd(';')
    $adapter = $adapter.Replace('__CONFIG__','configuration')
    $template = $template.Replace(('__' + $name.ToUpperInvariant() + '_ADAPTER__'),$adapter)
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'src/Portal.js'),$template,(New-Object Text.UTF8Encoding($false)))
