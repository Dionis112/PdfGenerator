param(
    [switch]$Docker,
    [string]$DependencyFeed
)

$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet test PdfGenerator.UnitTests -c Release
    if ($LASTEXITCODE) { throw 'Unit tests failed' }

    dotnet test PdfGenerator.IntegrationTests -c Release
    if ($LASTEXITCODE) { throw 'Integration tests failed' }

    foreach ($project in @('PdfGenerator.Domain', 'PdfGenerator.Application', 'PdfGenerator.Infrastructure')) {
        dotnet pack $project -c Release -o artifacts/packages
        if ($LASTEXITCODE) { throw "Pack failed: $project" }
    }

    $consumerPackages = Join-Path 'artifacts/consumer-packages' ([Guid]::NewGuid().ToString('N'))
    $restoreArguments = @('restore', 'PdfGenerator.Demo', '--configfile', 'eng/NuGet.local.Config', '--packages', $consumerPackages, '--force')
    if ($DependencyFeed) {
        $restoreArguments += @('--source', (Resolve-Path -LiteralPath 'artifacts/packages').Path)
        $restoreArguments += @('--source', (Resolve-Path -LiteralPath $DependencyFeed).Path)
    }
    & dotnet @restoreArguments
    if ($LASTEXITCODE) { throw 'Consumer restore failed' }

    dotnet run --project PdfGenerator.Demo -c Release --no-restore -- --self-test --pdf --data test-data --output artifacts/windows-smoke
    if ($LASTEXITCODE) { throw 'Consumer smoke test failed' }

    if ($Docker) {
        docker build -t pdfgenerator .
        if ($LASTEXITCODE) { throw 'Docker build failed' }

        docker run --rm --read-only --tmpfs /tmp --network none pdfgenerator
        if ($LASTEXITCODE) { throw 'Docker smoke test failed' }
    }
}
finally {
    Pop-Location
}
