$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$applicationProject = Join-Path $repositoryRoot 'VOIDOverlay\VOIDOverlay.csproj'
$testProject = Join-Path $repositoryRoot 'VOIDOverlay.Tests\VOIDOverlay.Tests.csproj'
$publishDirectory = Join-Path $repositoryRoot 'artifacts\publish\win-x64'

dotnet test $testProject --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Release tests failed with exit code $LASTEXITCODE."
}

dotnet publish $applicationProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Windows x64 publish failed with exit code $LASTEXITCODE."
}

Write-Output "Framework-dependent Windows x64 publish: $publishDirectory"
