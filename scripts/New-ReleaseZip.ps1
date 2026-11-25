param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Directory')]
    [string] $SourceDirectory,
    [Parameter(Mandatory = $true, ParameterSetName = 'Zip')]
    [string] $SourceZip,
    [Parameter(Mandatory = $true)]
    [string] $OutputZip,
    [string] $FolderName = 'Valley-Keepsake'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

$output = [System.IO.Path]::GetFullPath($OutputZip)
if ($PSCmdlet.ParameterSetName -eq 'Directory') {
    $source = (Resolve-Path -LiteralPath $SourceDirectory).Path
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "Source directory not found: $source"
    }
    if ($output.StartsWith($source.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The output ZIP must be outside the source directory.'
    }
    $files = @(Get-ChildItem -LiteralPath $source -File -Recurse |
        Where-Object { $_.Extension -ne '.pdb' })
    if ($files.Count -eq 0) { throw "Source directory is empty: $source" }
} else {
    $source = (Resolve-Path -LiteralPath $SourceZip).Path
    if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or $source -eq $output) {
        throw 'Source ZIP must exist and differ from OutputZip.'
    }
}
if ($FolderName -notmatch '^[^/\\]+$' -or $FolderName -eq '.' -or $FolderName -eq '..') {
    throw 'FolderName must be one directory name.'
}

$outputDirectory = [System.IO.Path]::GetDirectoryName($output)
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$temporary = "$output.tmp"
try {
    $stream = [System.IO.File]::Open($temporary, [System.IO.FileMode]::Create)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            if ($PSCmdlet.ParameterSetName -eq 'Directory') {
                foreach ($file in $files) {
                    $relative = [System.IO.Path]::GetRelativePath($source, $file.FullName).Replace('\', '/')
                    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                        $archive, $file.FullName, "$FolderName/$relative",
                        [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
                }
                $count = $files.Count
            } else {
                $inputArchive = [System.IO.Compression.ZipFile]::OpenRead($source)
                try {
                    $entries = @($inputArchive.Entries | Where-Object { $_.Name })
                    if ($entries.Count -eq 0) { throw 'Source ZIP has no files.' }
                    foreach ($entry in $entries) {
                        $relative = $entry.FullName.Replace('\', '/')
                        if ($relative.StartsWith('/') -or $relative -split '/' -contains '..') {
                            throw "Invalid path in source ZIP: $relative"
                        }
                        $newEntry = $archive.CreateEntry("$FolderName/$relative",
                            [System.IO.Compression.CompressionLevel]::Optimal)
                        $newEntry.LastWriteTime = $entry.LastWriteTime
                        $inputStream = $entry.Open()
                        $outputStream = $newEntry.Open()
                        try { $inputStream.CopyTo($outputStream) }
                        finally { $outputStream.Dispose(); $inputStream.Dispose() }
                    }
                    $count = $entries.Count
                } finally { $inputArchive.Dispose() }
            }
        } finally { $archive.Dispose() }
    } finally { $stream.Dispose() }
    Move-Item -LiteralPath $temporary -Destination $output -Force
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}

Write-Output "Created $output with $count files under $FolderName/"
