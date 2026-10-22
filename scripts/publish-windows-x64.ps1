$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$applicationProject = Join-Path $repositoryRoot 'VOIDOverlay.Termo\VOIDOverlay.Termo.csproj'
$testProject = Join-Path $repositoryRoot 'VOIDOverlay.Termo.Tests\VOIDOverlay.Termo.Tests.csproj'
$publishRoot = Join-Path $repositoryRoot 'publish'
$publishParent = Join-Path $publishRoot 'VOIDOverlay-Termo'
$publishDirectory = Join-Path $publishParent 'win-x64'
$runId = [Guid]::NewGuid().ToString('N')
$stagingDirectory = Join-Path $publishParent "win-x64.staging-$runId"
$backupDirectory = Join-Path $publishParent "win-x64.backup-$runId"

dotnet test $testProject --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Release tests failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $publishParent -Force | Out-Null
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

try {
    dotnet publish $applicationProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained false `
        --output $stagingDirectory `
        -p:DebugSymbols=false `
        -p:DebugType=None

    if ($LASTEXITCODE -ne 0) {
        throw "Windows x64 publish failed with exit code $LASTEXITCODE."
    }

    $requiredFiles = @(
        'VOIDOverlay.Termo.exe',
        'VOIDOverlay.Termo.dll',
        'VOIDOverlay.Termo.deps.json',
        'VOIDOverlay.Termo.runtimeconfig.json'
    )
    foreach ($fileName in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $stagingDirectory $fileName) -PathType Leaf)) {
            throw "The publish output is missing required file '$fileName'."
        }
    }

    if (Get-ChildItem -LiteralPath $stagingDirectory -Recurse -File -Filter '*.pdb') {
        throw 'The publish output contains PDB files; refusing to publish development symbols.'
    }

    $hadPreviousPublish = Test-Path -LiteralPath $publishDirectory
    $previousPublishMoved = $false
    try {
        if ($hadPreviousPublish) {
            Move-Item -LiteralPath $publishDirectory -Destination $backupDirectory
            $previousPublishMoved = $true
        }

        Move-Item -LiteralPath $stagingDirectory -Destination $publishDirectory
    }
    catch {
        if ((-not $hadPreviousPublish -or $previousPublishMoved) -and
            (Test-Path -LiteralPath $publishDirectory)) {
            Remove-Item -LiteralPath $publishDirectory -Recurse -Force
        }
        if ($previousPublishMoved -and (Test-Path -LiteralPath $backupDirectory)) {
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

Write-Output "Framework-dependent Windows x64 publish: $publishDirectory"
