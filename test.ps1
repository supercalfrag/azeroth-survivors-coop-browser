$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -Tests
& (Join-Path $PSScriptRoot 'dist\SmokeTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Offline smoke tests failed.' }
