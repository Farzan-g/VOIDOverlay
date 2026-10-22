$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$applicationProject = Join-Path $repositoryRoot 'VOIDOverlay\VOIDOverlay.csproj'
$testProject = Join-Path $repositoryRoot 'VOIDOverlay.Tests\VOIDOverlay.Tests.csproj'

dotnet restore $applicationProject
if ($LASTEXITCODE -ne 0) { throw "Application restore failed with exit code $LASTEXITCODE." }

dotnet test $testProject --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Release tests failed with exit code $LASTEXITCODE." }

dotnet build $applicationProject --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE." }
