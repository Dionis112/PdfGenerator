[CmdletBinding(SupportsShouldProcess)]
param([switch]$VerificationCache)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$repositoryPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

# Project directories only: never enumerate generated feeds or user data recursively.
$projects = @(Get-ChildItem -LiteralPath $repositoryRoot -Directory -Filter 'PdfGenerator.*')
$projects += @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'eng') -Directory -Filter 'PdfGenerator.*')
$targets = @(
    foreach ($project in $projects) {
        foreach ($name in @('bin', 'obj', 'TestResults')) {
            Join-Path $project.FullName $name
        }
    }
    Join-Path $repositoryRoot 'TestResults'
)
if ($VerificationCache) {
    foreach ($name in @('consumer-packages', 'nuget-config', 'windows-publish')) {
        $targets += Join-Path $repositoryRoot "artifacts/$name"
    }
}

foreach ($target in $targets) {
    $absoluteTarget = [IO.Path]::GetFullPath($target)
    if (!$absoluteTarget.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cleanup target is outside the repository: $absoluteTarget"
    }
    if (!(Test-Path -LiteralPath $absoluteTarget)) { continue }

    # A junction on any ancestor could redirect an otherwise valid repository path.
    $currentPath = $absoluteTarget
    while ($currentPath -ne $repositoryRoot) {
        if ((Get-Item -LiteralPath $currentPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Cleanup refuses symbolic links or junctions: $currentPath"
        }
        $currentPath = Split-Path $currentPath -Parent
    }
    if (!(Get-Item -LiteralPath $absoluteTarget -Force).PSIsContainer) {
        throw "Expected a generated directory: $absoluteTarget"
    }
    # Refuse linked descendants as well, before any recursive deletion.
    $linkedItem = Get-ChildItem -LiteralPath $absoluteTarget -Recurse -Force |
        Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } |
        Select-Object -First 1
    if ($linkedItem) { throw "Cleanup refuses a linked descendant: $($linkedItem.FullName)" }

    if ($PSCmdlet.ShouldProcess($absoluteTarget, 'Remove generated directory')) {
        Remove-Item -LiteralPath $absoluteTarget -Recurse -Force
        Write-Output "Removed: $absoluteTarget"
    }
}
