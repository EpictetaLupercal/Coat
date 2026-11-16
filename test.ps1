# Coat — Epicteta Lupercal
param([string]$OutputDirectory = 'app')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory
$executable = Join-Path (Join-Path $PSScriptRoot $OutputDirectory) 'Coat.exe'
$testOutput = Join-Path $PSScriptRoot 'artifacts\tests'
Remove-Item -LiteralPath (Join-Path (Join-Path $PSScriptRoot $OutputDirectory) 'test-error.txt') -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $executable -ArgumentList @('--self-test', ('"' + $testOutput + '"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Tests failed. See $OutputDirectory/test-error.txt." }
Get-Content -LiteralPath (Join-Path $testOutput 'results.txt') -Tail 2
