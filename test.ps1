# Coat — Epicteta Lupercal
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$executable = Join-Path $PSScriptRoot 'app\Coat.exe'
$testOutput = Join-Path $PSScriptRoot 'artifacts\tests'
$process = Start-Process -FilePath $executable -ArgumentList @('--self-test', ('"' + $testOutput + '"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Tests failed. See app\test-error.txt." }
Get-Content -LiteralPath (Join-Path $testOutput 'results.txt') -Tail 2
