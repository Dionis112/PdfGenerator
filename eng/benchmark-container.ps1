param(
    [int[]]$Sizes = @(10,25,50,100),
    [int[]]$Concurrency = @(1,2,4),
    [int]$Iterations = 4,
    [ValidateSet('inProcess','worker')][string]$Mode = 'inProcess',
    [ValidateSet('repeated','mixed','new','stream','baseline','queued','oneshot')][string]$Scenario = 'repeated',
    [string]$Image = 'pdfgenerator',
    [string]$Memory = '2g',
    [int]$Cpus = 4,
    [string]$Output = 'artifacts/benchmarks/linux'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$resultRoot = [System.IO.Path]::GetFullPath((Join-Path $repo $Output))
New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null
foreach ($size in $Sizes) {
    foreach ($jobs in $Concurrency) {
        $name = "$Mode-$Scenario-$size-$jobs"
        $runArgs = @('run','--rm','--read-only','--tmpfs','/tmp:rw,noexec,nosuid,size=512m','--network','none',
            '--memory',$Memory,'--cpus',$Cpus,'--mount',"type=bind,source=$resultRoot,target=/results",'--entrypoint','dotnet',
            $Image,'/benchmark/PdfGenerator.Benchmarks.dll',$size,$jobs,$Iterations,"/results/$name",$Mode,$Scenario)
        & docker @runArgs | Out-File -LiteralPath (Join-Path $resultRoot "$name.log") -Encoding utf8
        if ($LASTEXITCODE) { throw "Container benchmark failed: $name" }
    }
}
