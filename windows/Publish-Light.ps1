$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$artifactDir = Join-Path $PSScriptRoot 'artifacts'
$zipPath = Join-Path $artifactDir 'Shelf-Windows11-v0.2-Light.zip'
if (Test-Path $zipPath) {
    throw 'The ZIP already exists. Move or rename it before publishing again.'
}
# Use a new directory so old build outputs cannot enter the package.
$publishDir = Join-Path $artifactDir ('Shelf-v0.2-Light-' + [Guid]::NewGuid().ToString('N'))
dotnet publish ./src/Shelf.Windows/Shelf.Windows.csproj -c Release -r win-x64 --self-contained false -p:DebugType=None -p:DebugSymbols=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
Copy-Item ./docs/Light-README.txt (Join-Path $publishDir 'README-JA.txt')
Copy-Item ./docs/Windows-Test-Checklist.md (Join-Path $publishDir 'Windows-Test-Checklist.md')
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath
Write-Output "Created: $zipPath"
