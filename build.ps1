param([switch]$Tests,[string]$ClassAtlas,[string]$FontDirectory)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework x64 C# compiler is required.' }
$destination = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$options = @('/nologo','/unsafe','/platform:x64','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
if (!$ClassAtlas) { $ClassAtlas = Join-Path $PSScriptRoot 'assets\ClassAtlas.png' }
if ($ClassAtlas) {
    $atlasPath = (Resolve-Path -LiteralPath $ClassAtlas).Path
    $options += '/resource:' + $atlasPath + ',ClassAtlas.png'
}
if ($FontDirectory) {
    foreach ($font in @('FRIZQT__.TTF','MORPHEUS.TTF')) {
        $fontPath = (Resolve-Path -LiteralPath (Join-Path $FontDirectory $font)).Path
        $options += '/resource:' + $fontPath + ',' + $font
    }
}
if ($Tests) {
    $sources += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object { $_.FullName })
    $options += @('/target:exe','/main:SmokeTests',('/out:' + (Join-Path $destination 'SmokeTests.exe')))
} else {
    $options += @('/target:winexe',('/out:' + (Join-Path $destination 'AzerothCoopBrowser.exe')))
}
& $compiler @options @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Host 'Build complete.'
