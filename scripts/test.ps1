param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'VOIDOverlay.Termo.sln'
$artifacts = Join-Path $repositoryRoot 'artifacts\build'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is required. Install a compatible .NET SDK and retry.'
}

Push-Location $repositoryRoot
try {
    dotnet test $solution --configuration $Configuration --artifacts-path $artifacts
    if ($LASTEXITCODE -ne 0) {
        throw "Termo $Configuration tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
