param(
    [int[]]$Sizes = @(10,25,50,100),
    [int[]]$Concurrency = @(1,2,4),
    [int]$Iterations = 4,
    [ValidateSet('inProcess','worker')][string]$Mode = 'inProcess',
    [ValidateSet('repeated','mixed','new','stream','baseline','queued','oneshot')][string]$Scenario = 'repeated',
    [string]$Output = 'artifacts/benchmarks/matrix'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    dotnet build eng/PdfGenerator.Benchmarks -c Release
    if ($LASTEXITCODE) { throw 'Benchmark build failed' }
    foreach ($size in $Sizes) {
        foreach ($jobs in $Concurrency) {
            $run = Join-Path $Output "$Mode-$Scenario-$size-$jobs"
            New-Item -ItemType Directory -Force -Path $run | Out-Null
            dotnet eng/PdfGenerator.Benchmarks/bin/Release/net8.0/PdfGenerator.Benchmarks.dll $size $jobs $Iterations $run $Mode $Scenario |
                Out-File -LiteralPath (Join-Path $run 'run.log') -Encoding utf8
            if ($LASTEXITCODE) { throw "Benchmark failed: $size MiB, $jobs jobs" }
        }
    }
}
finally { Pop-Location }
