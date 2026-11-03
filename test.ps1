param([string]$FontDirectory)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -Tests -FontDirectory $FontDirectory
& (Join-Path $PSScriptRoot 'dist\SmokeTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Offline smoke tests failed.' }
