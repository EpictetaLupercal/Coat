# Coat — Epicteta Lupercal
param([string]$OutputDirectory = 'app', [string]$TestOutputDirectory = 'artifacts\tests', [switch]$View3D, [switch]$EditorSounds, [switch]$GeometryTools, [switch]$SelectionFocus, [switch]$EditorLayout, [switch]$ContextOptions, [switch]$InteractionFeedback, [switch]$RtsHud)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory
$executable = Join-Path (Join-Path $PSScriptRoot $OutputDirectory) 'Coat.exe'
$testOutput = Join-Path $PSScriptRoot $TestOutputDirectory
Remove-Item -LiteralPath (Join-Path (Join-Path $PSScriptRoot $OutputDirectory) 'test-error.txt') -ErrorAction SilentlyContinue
$testMode = if ($RtsHud) { '--test-rts' } elseif ($InteractionFeedback) { '--test-interaction-feedback' } elseif ($ContextOptions) { '--test-context-options' } elseif ($EditorLayout) { '--test-editor-layout' } elseif ($SelectionFocus) { '--test-selection-focus' } elseif ($GeometryTools) { '--test-geometry-tools' } elseif ($EditorSounds) { '--test-editor-sounds' } elseif ($View3D) { '--test-3d-view' } else { '--self-test' }
$process = Start-Process -FilePath $executable -ArgumentList @($testMode, ('"' + $testOutput + '"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Tests failed. See $OutputDirectory/test-error.txt." }
Get-Content -LiteralPath (Join-Path $testOutput 'results.txt') -Tail 2
