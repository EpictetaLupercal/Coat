# Coat — Epicteta Lupercal
param([switch]$Package)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$project = Join-Path $PSScriptRoot 'src\Coat.csproj'
$output = Join-Path $PSScriptRoot 'app'
dotnet publish $project -c Release --self-contained false -o $output -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Coat build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\Guide-Francais.md') -Destination (Join-Path $output 'LISEZ-MOI.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\CHANGELOG.md') -Destination (Join-Path $output 'NOUVEAUTES.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\README-English.md') -Destination (Join-Path $output 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\CHANGELOG-English.md') -Destination (Join-Path $output 'CHANGELOG.md') -Force
if ($Package) {
    [xml]$projectXml = Get-Content -LiteralPath $project
    $version = $projectXml.Project.PropertyGroup.Version
    $distribution = Join-Path $PSScriptRoot 'dist'
    New-Item -ItemType Directory -Path $distribution -Force | Out-Null
    # Explicit manifest: no examples, test output, preferences, or user plans in the delivery.
    $files = @('Coat.exe', 'Coat.dll', 'Coat.deps.json', 'Coat.runtimeconfig.json', 'README.md', 'LISEZ-MOI.md', 'CHANGELOG.md', 'NOUVEAUTES.md') | ForEach-Object { Join-Path $output $_ }
    $zip = Join-Path $distribution "Coat-$version-Windows-x64.zip"
    Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
    Write-Host "Package: $zip"
}
Write-Host "Coat is ready: $output\Coat.exe"
