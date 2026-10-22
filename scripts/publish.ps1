param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src\VOIDOverlay.Termo\VOIDOverlay.Termo.csproj'
$publishRoot = Join-Path $repositoryRoot 'artifacts\publish'
$publishDirectory = Join-Path $publishRoot 'win-x64'
$runId = [Guid]::NewGuid().ToString('N')
$stagingDirectory = Join-Path $publishRoot "win-x64.staging-$runId"
$backupDirectory = Join-Path $publishRoot "win-x64.backup-$runId"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is required. Install a compatible .NET SDK and retry.'
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
try {
    dotnet publish $project --configuration $Configuration --runtime win-x64 --self-contained false `
        --output $stagingDirectory -p:PublishSingleFile=false -p:DebugSymbols=false -p:DebugType=None
    if ($LASTEXITCODE -ne 0) {
        throw "Termo Windows x64 publish failed with exit code $LASTEXITCODE."
    }

    foreach ($file in @(
        'VOIDOverlay.Termo.exe',
        'VOIDOverlay.Termo.dll',
        'VOIDOverlay.Termo.deps.json',
        'VOIDOverlay.Termo.runtimeconfig.json'
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $stagingDirectory $file) -PathType Leaf)) {
            throw "Termo publish output is missing required file '$file'."
        }
    }

    if (Get-ChildItem -LiteralPath $stagingDirectory -Recurse -File -Filter '*.pdb') {
        throw 'Termo publish output contains development symbols.'
    }

    $hadPreviousOutput = Test-Path -LiteralPath $publishDirectory
    $previousOutputMoved = $false
    try {
        if ($hadPreviousOutput) {
            Move-Item -LiteralPath $publishDirectory -Destination $backupDirectory
            $previousOutputMoved = $true
        }
        Move-Item -LiteralPath $stagingDirectory -Destination $publishDirectory
    }
    catch {
        if ((-not $hadPreviousOutput -or $previousOutputMoved) -and
            (Test-Path -LiteralPath $publishDirectory)) {
            Remove-Item -LiteralPath $publishDirectory -Recurse -Force
        }
        if ($previousOutputMoved -and (Test-Path -LiteralPath $backupDirectory)) {
            Move-Item -LiteralPath $backupDirectory -Destination $publishDirectory
        }
        throw
    }

    if (Test-Path -LiteralPath $backupDirectory) {
        Remove-Item -LiteralPath $backupDirectory -Recurse -Force
    }
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

Write-Output "Framework-dependent Termo Windows x64 publish: $publishDirectory"
