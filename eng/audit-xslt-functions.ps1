param(
    [Parameter(Mandatory = $true)]
    [string]$TemplateDirectory,

    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $TemplateDirectory).Path
$files = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.Extension -in '.xsl', '.xslt' })
$xsltNamespace = 'http://www.w3.org/1999/XSL/Transform'
$altovaNamespace = 'http://www.altova.com/xslt-extensions'
$standardNamespaces = @(
    'http://www.w3.org/2005/xpath-functions',
    'http://www.w3.org/2001/XMLSchema',
    $xsltNamespace
)
$callPattern = [regex]'(?<![\w.-])(?<prefix>[A-Za-z_][\w.-]*):(?<name>[A-Za-z_][\w.-]*)\s*\('
$documents = [System.Collections.Generic.List[System.Xml.XmlDocument]]::new()
$parseErrors = 0

foreach ($file in $files) {
    try {
        $settings = [System.Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $reader = [System.Xml.XmlReader]::Create($file.FullName, $settings)
        try {
            $document = [System.Xml.XmlDocument]::new()
            $document.XmlResolver = $null
            $document.Load($reader)
            $documents.Add($document)
        }
        finally {
            $reader.Dispose()
        }
    }
    catch {
        $parseErrors++
    }
}

$declaredFunctions = [System.Collections.Generic.HashSet[string]]::new()
foreach ($document in $documents) {
    foreach ($element in $document.SelectNodes('//*')) {
        if ($element.NamespaceURI -eq $xsltNamespace -and $element.LocalName -eq 'function') {
            $name = $element.GetAttribute('name')
            if ($name -match '^([^:]+):(.+)$') {
                $uri = $element.GetNamespaceOfPrefix($Matches[1])
                if ($uri) {
                    [void]$declaredFunctions.Add("{$uri}$($Matches[2])")
                }
            }
        }
    }
}

$calls = @{}
foreach ($document in $documents) {
    foreach ($element in $document.SelectNodes('//*')) {
        foreach ($attribute in $element.Attributes) {
            if ($attribute.Prefix -eq 'xmlns' -or $attribute.Name -eq 'xmlns') {
                continue
            }
            foreach ($match in $callPattern.Matches($attribute.Value)) {
                $prefix = $match.Groups['prefix'].Value
                $name = $match.Groups['name'].Value
                $uri = $element.GetNamespaceOfPrefix($prefix)
                if (-not $uri) {
                    $uri = '(unresolved prefix)'
                }
                $key = "{$uri}$name"
                if (-not $calls.ContainsKey($key)) {
                    $kind = if ($uri -eq $altovaNamespace) {
                        'Altova'
                    }
                    elseif ($declaredFunctions.Contains($key)) {
                        'XSLT-defined'
                    }
                    elseif ($uri -in $standardNamespaces) {
                        'Standard'
                    }
                    else {
                        'Other extension'
                    }
                    $calls[$key] = [pscustomobject]@{ Kind = $kind; NamespaceUri = $uri; Name = $name; Count = 0 }
                }
                $calls[$key].Count++
            }
        }
    }
}

$report = [pscustomobject]@{
    TemplateCount = $files.Count
    ParsedCount = $documents.Count
    ParseErrorCount = $parseErrors
    Calls = @($calls.Values | Sort-Object Kind, NamespaceUri, Name)
    Note = 'Static candidate scan of attribute values; dynamic calls, aliases in XPath strings and some function calls can be missed. Counts may include false positives. No template content or file paths are included.'
}

$json = $report | ConvertTo-Json -Depth 5
if ($ReportPath) {
    $target = [System.IO.Path]::GetFullPath($ReportPath)
    [System.IO.File]::WriteAllText($target, $json, [System.Text.UTF8Encoding]::new($false))
    Write-Output "Report written to $target"
}
else {
    Write-Output $json
}
