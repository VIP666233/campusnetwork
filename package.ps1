param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'releases'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$tutorialName = '校园网自动登录_从零开始使用教程.txt'
$tutorial = Join-Path $PSScriptRoot $tutorialName
if (!(Test-Path -LiteralPath $tutorial)) { throw 'Required tutorial is missing.' }
$main = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/MainForm.cs'))
$title = [regex]::Match($main, 'Text = "校园网自动登录 v(?<version>\d+\.\d+\.\d+)";')
if (!$title.Success) { throw 'Window title must contain only the app name and version number.' }
$version = $title.Groups['version'].Value
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$appZip = Join-Path $OutputDirectory ('校园网自动登录_v' + $version + '.zip')
$sourceZip = Join-Path $OutputDirectory ('CampusNetLoginHelper-source-v' + $version + '.zip')
if ((Test-Path -LiteralPath $appZip) -or (Test-Path -LiteralPath $sourceZip)) { throw 'Release archive already exists. Choose a different output directory.' }
& (Join-Path $PSScriptRoot 'build.ps1')
$stage = Join-Path $PSScriptRoot ('.build/package-' + [Guid]::NewGuid().ToString('N'))
$appStage = Join-Path $stage 'app'
$sourceStage = Join-Path $stage 'source'
New-Item -ItemType Directory -Path $appStage,$sourceStage,$OutputDirectory -Force | Out-Null
foreach ($name in @('校园网自动登录.exe','校园网自动登录.exe.config')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('dist/' + $name)) -Destination $appStage
}
Copy-Item -LiteralPath $tutorial,(Join-Path $PSScriptRoot 'LICENSE') -Destination $appStage
foreach ($folder in @('src','tests','.github','docs')) {
    $base = Join-Path $PSScriptRoot $folder
    if (!(Test-Path -LiteralPath $base)) { continue }
    Get-ChildItem -LiteralPath $base -Recurse -File -Force | Where-Object { $_.Extension -in @('.cs','.js','.cjs','.ps1','.html','.config','.ico','.png','.yml') } | ForEach-Object {
        $target = Join-Path $sourceStage $_.FullName.Substring($PSScriptRoot.Length + 1)
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $target
    }
}
foreach ($name in @('README.md','LICENSE','SOURCE_PROVENANCE.md','RELEASING.md','.gitignore','.gitattributes','build.ps1','build-portal.ps1','package.ps1',$tutorialName)) {
    if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot $name))) { continue }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $sourceStage
}
$utf8 = New-Object Text.UTF8Encoding($true)
Get-ChildItem -LiteralPath $sourceStage -Recurse -File -Filter '*.ps1' | ForEach-Object { [IO.File]::WriteAllText($_.FullName,[IO.File]::ReadAllText($_.FullName),$utf8) }
$manifest = Get-ChildItem -LiteralPath $sourceStage -Recurse -File -Force | Sort-Object FullName | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash + '  ' + $_.FullName.Substring($sourceStage.Length + 1).Replace('\','/') }
[IO.File]::WriteAllLines((Join-Path $sourceStage 'SOURCE_FILES.sha256'),$manifest,$utf8)
[IO.Compression.ZipFile]::CreateFromDirectory($appStage,$appZip)
[IO.Compression.ZipFile]::CreateFromDirectory($sourceStage,$sourceZip)
$expected = (Get-FileHash -LiteralPath $tutorial -Algorithm SHA256).Hash
foreach ($path in @($appZip,$sourceZip)) {
    $archive = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entry = $archive.GetEntry($tutorialName)
        if (!$entry) { throw 'Tutorial not found in archive.' }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($actual -ne $expected) { throw 'Tutorial changed while packaging.' }
    } finally { $archive.Dispose() }
    Write-Output $path
}
$hashes = @($appZip,$sourceZip) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash + '  ' + (Split-Path $_ -Leaf) }
[IO.File]::WriteAllLines((Join-Path $OutputDirectory ('v' + $version + '-SHA256SUMS.txt')),$hashes,$utf8)
