# Coat — Epicteta Lupercal
param([switch]$Package, [string]$OutputDirectory = 'app')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$project = Join-Path $PSScriptRoot 'src\Coat.csproj'
$output = Join-Path $PSScriptRoot $OutputDirectory
dotnet publish $project -c Release --self-contained false -o $output -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Coat build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\Guide-Francais.md') -Destination (Join-Path $output 'LISEZ-MOI.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\CHANGELOG.md') -Destination (Join-Path $output 'NOUVEAUTES.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $output 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\CHANGELOG-English.md') -Destination (Join-Path $output 'CHANGELOG.md') -Force
$readmeAssets = @('docs/README-English.md', 'docs/Guide-Francais.md', 'docs/CHANGELOG-English.md', 'docs/images/coat-interface.png', 'docs/images/coat-logo.png', 'docs/LUA-PLUGINS.md', 'docs/MOONSHARP-LICENSE.txt', 'plugins/Plan summary.lua')
foreach ($asset in $readmeAssets) {
    $destination = Join-Path $output $asset
    New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $asset) -Destination $destination -Force
}
if ($Package) {
    [xml]$projectXml = Get-Content -LiteralPath $project
    $version = $projectXml.Project.PropertyGroup.Version
    $distribution = Join-Path $PSScriptRoot 'dist'
    New-Item -ItemType Directory -Path $distribution -Force | Out-Null
    # Explicit manifest: only the app, its dependencies, documentation and bundled plugin.
    $files = @('Coat.exe', 'Coat.dll', 'MoonSharp.Interpreter.dll', 'Coat.deps.json', 'Coat.runtimeconfig.json', 'README.md', 'LISEZ-MOI.md', 'CHANGELOG.md', 'NOUVEAUTES.md') + $readmeAssets
    $zip = Join-Path $distribution "Coat-$version-Windows-x64.zip"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stream = [IO.File]::Create($zip)
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($entry in $files) {
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $output $entry), $entry) | Out-Null
            }
        } finally { $archive.Dispose() }
    } finally { $stream.Dispose() }
    Write-Host "Package: $zip"
}
Write-Host "Coat is ready: $output\Coat.exe"
