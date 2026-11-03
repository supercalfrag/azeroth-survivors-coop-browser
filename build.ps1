param([switch]$Tests)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework x64 C# compiler is required.' }
$destination = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$options = @('/nologo','/unsafe','/platform:x64','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
if ($Tests) {
    $sources += Join-Path $PSScriptRoot 'tests\SmokeTests.cs'
    $options += @('/target:exe','/main:SmokeTests',('/out:' + (Join-Path $destination 'SmokeTests.exe')))
} else {
    $options += @('/target:winexe',('/out:' + (Join-Path $destination 'AzerothCoopBrowser.exe')))
}
& $compiler @options @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Host 'Build complete.'
