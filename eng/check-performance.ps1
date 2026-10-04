param([string]$Reports = 'docs/benchmarks/linux-v2', [double]$AllowedRegression = 0.25, [string]$Baseline)
$ErrorActionPreference = 'Stop'
$files = @(Get-ChildItem -LiteralPath $Reports -Recurse -Filter 'report-*.json')
if (!$files.Count) { throw "No benchmark reports in $Reports" }
foreach ($file in $files) {
    $report = Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json
    $warm = @($report.measurements | Where-Object round -GT 0 | ForEach-Object totalMs | Sort-Object)
    if (!$warm.Count) { throw 'At least two rounds are required' }
    $p95 = $warm[[int][math]::Ceiling($warm.Count * .95) - 1]
    $jobs = [int]$report.concurrency
    if ($report.sizeMiB -le 10) { $latency = 6000 * [math]::Sqrt($jobs); $rss = (384 + 128 * ($jobs - 1)) * 1MB }
    elseif ($report.sizeMiB -le 50) { $latency = 12000 * [math]::Sqrt($jobs); $rss = (512 + 256 * ($jobs - 1)) * 1MB }
    else { $latency = 15000 * [math]::Sqrt($jobs); $rss = (640 + 320 * ($jobs - 1)) * 1MB }
    if ($report.mode -eq 'worker') {
        $rss = (128 + 1024 * $jobs) * 1MB
        # The boundary worker case includes parent staging and another bounded input copy.
        if ($report.sizeMiB -gt 50) { $latency = 20000 * [math]::Sqrt($jobs) }
    }
    if ($p95 -gt $latency -or $report.peakWorkingSet -gt $rss) { throw "Budget exceeded in $($file.Name): warm p95=$p95 ms, RSS=$($report.peakWorkingSet) bytes" }
    if (@($report.measurements | Where-Object { $_.PageCount -lt 1 -or $_.PdfBytes -lt 5 }).Count) { throw 'Invalid completed PDF metrics' }
    if ($report.mode -eq 'inProcess' -and $report.scenario -eq 'repeated' -and $report.templateCache.Compilations -ne 1) { throw 'Repeated template compiled more than once' }
    if ($Baseline) {
        $reference = @(Get-ChildItem -LiteralPath $Baseline -Recurse -Filter "report-$($report.sizeMiB)-$jobs-*.json" | Sort-Object LastWriteTime -Descending)[0]
        if (!$reference) { throw 'Missing comparison baseline' }
        $old = Get-Content -Raw -LiteralPath $reference.FullName | ConvertFrom-Json
        if ($old.os -ne $report.os -or $old.processors -ne $report.processors -or $old.mode -ne $report.mode -or $old.corpus.version -ne $report.corpus.version) { throw 'Incompatible baseline environment/corpus' }
        $previous = @($old.measurements | Where-Object round -GT 0 | ForEach-Object totalMs | Sort-Object)
        $oldP95 = $previous[[int][math]::Ceiling($previous.Count * .95) - 1]
        if ($p95 -gt $oldP95 * (1 + $AllowedRegression)) { throw "Latency regression exceeds $AllowedRegression in $($file.Name)" }
    }
    Write-Output "PASS $($report.sizeMiB) MiB / $jobs jobs: warm p95=$([math]::Round($p95)) ms, RSS=$([math]::Round($report.peakWorkingSet/1MB)) MiB"
}
