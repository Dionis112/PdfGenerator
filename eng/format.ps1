param([switch]$Check)

$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $arguments = @('PdfGenerator.sln', '--no-restore')
    if ($Check) { $arguments += '--verify-no-changes' }

    dotnet format whitespace @arguments
    if ($LASTEXITCODE) { throw 'Whitespace formatting failed' }

    # The .NET 8 workspace cannot apply some document-metadata fixes to linked files.
    # Restrict style fixes to code rules; public namespaces remain stable.
    dotnet format style @arguments --severity info --diagnostics IDE0005 IDE0007 IDE0008 IDE0011 IDE0040 IDE0062 IDE0065 IDE0090 IDE0161 IDE0300 IDE0305
    if ($LASTEXITCODE) { throw 'C# style formatting failed' }
}
finally {
    Pop-Location
}
