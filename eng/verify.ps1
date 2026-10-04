param(
    [switch]$Docker,
    [string]$DependencyFeed
)

$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet run --project eng/PdfGenerator.Documentation -c Release -- --check .
    if ($LASTEXITCODE) { throw 'Bilingual XML documentation check failed' }
    dotnet test PdfGenerator.UnitTests -c Release
    if ($LASTEXITCODE) { throw 'Unit tests failed' }

    dotnet test PdfGenerator.IntegrationTests -c Release
    if ($LASTEXITCODE) { throw 'Integration tests failed' }

    foreach ($project in @('PdfGenerator.Domain', 'PdfGenerator.Application', 'PdfGenerator.Infrastructure')) {
        dotnet pack $project -c Release -o artifacts/packages
        if ($LASTEXITCODE) { throw "Pack failed: $project" }
    }

    $consumerPackages = Join-Path 'artifacts/consumer-packages' ([Guid]::NewGuid().ToString('N'))
    $consumerConfig = 'eng/NuGet.local.Config'
    if ($DependencyFeed) {
        [xml]$configuration = Get-Content -Raw -LiteralPath $consumerConfig
        $configuration.configuration.packageSources.add | Where-Object key -EQ 'mvp-local' | ForEach-Object { $_.value = (Resolve-Path -LiteralPath 'artifacts/packages').Path }
        $feed = $configuration.CreateElement('add')
        $feed.SetAttribute('key','dependencies')
        $feed.SetAttribute('value',(Resolve-Path -LiteralPath $DependencyFeed).Path)
        $null = $configuration.configuration.packageSources.AppendChild($feed)
        $mapping = $configuration.CreateElement('packageSource')
        $mapping.SetAttribute('key','dependencies')
        $pattern = $configuration.CreateElement('package')
        $pattern.SetAttribute('pattern','*')
        $null = $mapping.AppendChild($pattern)
        $null = $configuration.configuration.packageSourceMapping.AppendChild($mapping)
        New-Item -ItemType Directory -Force artifacts/nuget-config | Out-Null
        $consumerConfig = Join-Path 'artifacts/nuget-config' (([Guid]::NewGuid().ToString('N')) + '.config')
        $configuration.Save((Join-Path $PWD $consumerConfig))
    }
    $restoreArguments = @('restore', 'PdfGenerator.Demo', '--configfile', $consumerConfig, '--packages', $consumerPackages, '--force')
    & dotnet @restoreArguments
    if ($LASTEXITCODE) { throw 'Consumer restore failed' }

    dotnet run --project PdfGenerator.Demo -c Release --no-restore -- --self-test --pdf --data test-data --output artifacts/windows-smoke
    if ($LASTEXITCODE) { throw 'Consumer smoke test failed' }

    dotnet publish PdfGenerator.Demo -c Release --no-restore --self-contained false -o artifacts/windows-publish
    if ($LASTEXITCODE) { throw 'Consumer publish failed' }
    dotnet artifacts/windows-publish/PdfGenerator.Demo.dll --self-test --pdf --data test-data --output artifacts/windows-publish-smoke
    if ($LASTEXITCODE) { throw 'Published consumer smoke test failed' }

    if ($Docker) {
        docker build -t pdfgenerator .
        if ($LASTEXITCODE) { throw 'Docker build failed' }

        docker run --rm --read-only --tmpfs /tmp:rw,noexec,nosuid,size=512m --network none --memory 2g --cpus 4 pdfgenerator
        if ($LASTEXITCODE) { throw 'Docker smoke test failed' }
    }
}
finally {
    Pop-Location
}
