<#
.SYNOPSIS
    Writes a release's version, file names, sizes, download addresses and SHA-256 checksums into
    the AstroCast website.

.DESCRIPTION
    The website in the AstroCast folder beside this script names one release: its version, and
    for each of the three downloads (the Windows MSI, the macOS DMG and the prompt library zip)
    the file name, the size, the download address and the SHA-256 checksum.

    Every one of those values is marked in the pages so that this script can find it exactly:

    - an element with a data-rel attribute holds one value as its text
      (data-rel="version", "msi-name", "msi-size", "msi-sha", and the same for dmg and zip);
    - a link with data-rel="msi-url" (or dmg-url, zip-url) holds a download address in its href;
    - the comment "rel:version-jsonld" stands before the JSON-LD script whose "softwareVersion"
      line carries the version;
    - llms.txt has one "Current version:" line and three download lines.

    The script finds the three release files in -ReleaseFolder by name, reads their sizes and
    computes their SHA-256, and rewrites every marked value in AstroCast\index.html, any marked
    value in the help pages, and llms.txt.

    It refuses, changing nothing, when a release file is missing or matched more than once, when
    a marker it expects is missing or occurs an unexpected number of times, when a page carries
    a data-rel name it does not know, or when a value of the release being replaced would be
    left somewhere without a marker.

    It makes no network call. It writes each file with the encoding (UTF-8, with or without a
    byte-order mark) and the line endings it found. It prints what changed.

.PARAMETER Version
    The release's version, as three numbers: 1.7.1. It names the MSI (AstroCast_1.7.1.msi), the
    DMG (AstroCast-v1.7.1-macOS-arm64.dmg) and the release tag in the download addresses (v1.7.1).

.PARAMETER ReleaseFolder
    A local folder that holds that release's three files: AstroCast_<Version>.msi,
    AstroCast-v<Version>-macOS-arm64.dmg and exactly one AstroCast_Prompt_Library_<date>.zip.
    The files are only read.

.PARAMETER WhatIf
    Shows every value that would change, and writes nothing.

.EXAMPLE
    .\Update-WebsiteRelease.ps1 -Version 1.7.2 -ReleaseFolder D:\Releases\1.7.2 -WhatIf

    Lists what would change for release 1.7.2 without writing a file.

.EXAMPLE
    .\Update-WebsiteRelease.ps1 -Version 1.7.2 -ReleaseFolder D:\Releases\1.7.2

    Rewrites the release values, then prints each change. Commit the result and upload the
    changed files.

.NOTES
    Sizes are printed in megabytes of 1,000,000 bytes to one decimal place, as the page shows
    them (73,884,348 bytes is 73.9 MB).
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseFolder
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$siteRoot = Join-Path $PSScriptRoot 'AstroCast'
$downloadBase = 'https://github.com/sthornhill008/AstroCast/releases/download'
$invariant = [System.Globalization.CultureInfo]::InvariantCulture

# The number of times each marker must occur in index.html.
$indexExpected = [ordered]@{
    'version'  = 1
    'msi-url'  = 1; 'msi-name' = 3; 'msi-size' = 1; 'msi-sha' = 1
    'dmg-url'  = 1; 'dmg-name' = 3; 'dmg-size' = 1; 'dmg-sha' = 1
    'zip-url'  = 1; 'zip-name' = 2; 'zip-size' = 1; 'zip-sha' = 1
}

function Stop-Refused {
    <#
    .SYNOPSIS
        Ends the run with a message, before anything has been written.
    .PARAMETER Message
        Why the run is refused.
    #>
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "REFUSED: $Message"
    Write-Host 'Nothing was changed.'
    exit 1
}

function Read-SiteFile {
    <#
    .SYNOPSIS
        Reads a text file as UTF-8 and reports whether it began with a byte-order mark.
    .PARAMETER Path
        The file to read.
    .OUTPUTS
        A hashtable: Path, Text (without the mark), HasBom.
    #>
    param([Parameter(Mandatory = $true)][string]$Path)
    [byte[]]$bytes = [System.IO.File]::ReadAllBytes($Path)
    [bool]$hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    [int]$offset = if ($hasBom) { 3 } else { 0 }
    [string]$text = (New-Object System.Text.UTF8Encoding($false, $true)).GetString($bytes, $offset, $bytes.Length - $offset)
    return @{ Path = $Path; Text = $text; HasBom = $hasBom }
}

function Get-MarkerPattern {
    <#
    .SYNOPSIS
        The regular expression that finds one marker's value. Group 2 is the value.
    .PARAMETER Key
        The data-rel name.
    #>
    param([Parameter(Mandatory = $true)][string]$Key)
    [string]$k = [regex]::Escape($Key)
    if ($Key.EndsWith('-url')) {
        return '(<a\b[^<>]*\bdata-rel="' + $k + '"[^<>]*\bhref=")([^"]*)(")'
    }
    return '(<(?<tag>[A-Za-z0-9]+)\b[^<>]*\bdata-rel="' + $k + '"[^<>]*>)([^<]*)(</\k<tag>>)'
}

function Update-Markers {
    <#
    .SYNOPSIS
        Replaces every marked value in a page's text and records each change.
    .PARAMETER Text
        The page's text.
    .PARAMETER Values
        The new value for each data-rel name.
    .PARAMETER Expected
        For index.html, the count each marker must have; $null for a help page, where every
        marker found is rewritten and none is required.
    .PARAMETER Label
        The file's name, for messages.
    .PARAMETER Changes
        The list that receives one line for each value that changes.
    .PARAMETER Olds
        The list that receives each value being replaced.
    .OUTPUTS
        The new text.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Values,
        [AllowNull()][System.Collections.IDictionary]$Expected,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Changes,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Olds
    )

    # Every data-rel name on the page must be one this script knows.
    foreach ($m in [regex]::Matches($Text, '\bdata-rel="([^"]*)"')) {
        [string]$markerName = $m.Groups[1].Value
        if (-not $Values.Contains($markerName)) { Stop-Refused "$Label carries an unknown marker data-rel=`"$markerName`"." }
    }

    [string]$result = $Text
    foreach ($key in $Values.Keys) {
        [string]$pattern = Get-MarkerPattern -Key $key
        [int]$attributeCount = [regex]::Matches($result, '\bdata-rel="' + [regex]::Escape($key) + '"').Count
        $found = [regex]::Matches($result, $pattern)
        if ($found.Count -ne $attributeCount) {
            Stop-Refused "$Label has $attributeCount data-rel=`"$key`" attributes but $($found.Count) in the form this script can rewrite (a value must be plain text inside its element, or an href after the attribute)."
        }
        if ($null -ne $Expected -and $found.Count -ne [int]$Expected[$key]) {
            Stop-Refused "$Label should hold marker `"$key`" $($Expected[$key]) time(s) and holds it $($found.Count) time(s)."
        }
        [string]$new = [string]$Values[$key]
        foreach ($m in $found) {
            [string]$old = $m.Groups[2].Value
            if ($old -cne $new) {
                $Changes.Add("${Label}: $key : '$old' -> '$new'")
                if (-not $Olds.Contains($old)) { $Olds.Add($old) }
            }
        }
        $evaluator = [System.Text.RegularExpressions.MatchEvaluator] { param($mm) $mm.Groups[1].Value + $new + $mm.Groups[3].Value }
        $result = [regex]::Replace($result, $pattern, $evaluator)
    }
    return $result
}

function Update-OneLine {
    <#
    .SYNOPSIS
        Replaces the value on a line that must occur exactly once. Group 2 of the pattern is the value.
    .PARAMETER Text
        The file's text.
    .PARAMETER Pattern
        The regular expression, with three groups: before, value, after.
    .PARAMETER New
        The new value.
    .PARAMETER What
        The marker's name, for messages.
    .PARAMETER Label
        The file's name, for messages.
    .PARAMETER Changes
        The list that receives one line if the value changes.
    .PARAMETER Olds
        The list that receives the value being replaced.
    .OUTPUTS
        The new text.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$New,
        [Parameter(Mandatory = $true)][string]$What,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Changes,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Olds
    )
    $found = [regex]::Matches($Text, $Pattern)
    if ($found.Count -ne 1) { Stop-Refused "$Label should hold $What once and holds it $($found.Count) time(s)." }
    [string]$old = $found[0].Groups[2].Value
    if ($old -cne $New) {
        $Changes.Add("${Label}: $What : '$old' -> '$New'")
        if (-not $Olds.Contains($old)) { $Olds.Add($old) }
    }
    $evaluator = [System.Text.RegularExpressions.MatchEvaluator] { param($mm) $mm.Groups[1].Value + $New + $mm.Groups[3].Value }
    return [regex]::Replace($Text, $Pattern, $evaluator)
}

# --- The release files -----------------------------------------------------------------------

if (-not (Test-Path -LiteralPath $ReleaseFolder -PathType Container)) { Stop-Refused "The release folder does not exist: $ReleaseFolder" }
if (-not (Test-Path -LiteralPath $siteRoot -PathType Container)) { Stop-Refused "The site folder does not exist: $siteRoot" }

$wanted = [ordered]@{
    'msi' = "AstroCast_$Version.msi"
    'dmg' = "AstroCast-v$Version-macOS-arm64.dmg"
    'zip' = 'AstroCast_Prompt_Library_????-??-??.zip'
}
$values = [ordered]@{ 'version' = $Version }
foreach ($kind in $wanted.Keys) {
    $matched = @(Get-ChildItem -LiteralPath $ReleaseFolder -File -Filter $wanted[$kind])
    if ($matched.Count -ne 1) { Stop-Refused "Expected one file named $($wanted[$kind]) in $ReleaseFolder and found $($matched.Count)." }
    $file = $matched[0]
    [string]$sha = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    [string]$size = ($file.Length / 1000000.0).ToString('0.0', $invariant) + ' MB'
    $values["$kind-url"] = "$downloadBase/v$Version/$($file.Name)"
    $values["$kind-name"] = $file.Name
    $values["$kind-size"] = $size
    $values["$kind-sha"] = $sha
    Write-Host ("{0}: {1}, {2} bytes ({3}), sha256 {4}" -f $kind, $file.Name, $file.Length, $size, $sha)
}

# --- Work out every file's new text before writing any ---------------------------------------

$changes = New-Object System.Collections.Generic.List[string]
$olds = New-Object System.Collections.Generic.List[string]
$pending = New-Object System.Collections.Generic.List[object]

# index.html: the markers, and the JSON-LD version line after its marker comment.
$indexPath = Join-Path $siteRoot 'index.html'
if (-not (Test-Path -LiteralPath $indexPath)) { Stop-Refused "Missing: $indexPath" }
$index = Read-SiteFile -Path $indexPath
[string]$indexText = Update-Markers -Text $index.Text -Values $values -Expected $indexExpected -Label 'index.html' -Changes $changes -Olds $olds
if ([regex]::Matches($indexText, '<!-- rel:version-jsonld\b').Count -ne 1) { Stop-Refused 'index.html should hold the comment "rel:version-jsonld" once.' }
$indexText = Update-OneLine -Text $indexText -Pattern '(<!-- rel:version-jsonld\b[^>]*-->\s*<script type="application/ld\+json">[^<]*?"softwareVersion": ")([^"]*)(")' `
    -New $Version -What 'the JSON-LD "softwareVersion" line' -Label 'index.html' -Changes $changes -Olds $olds
if ([regex]::Matches($indexText, '"softwareVersion":').Count -ne 1) { Stop-Refused 'index.html should hold one "softwareVersion": line.' }
$pending.Add(@{ File = $index; NewText = $indexText; Label = 'index.html' })

# The help pages: every marker found is rewritten; none is required.
$helpRoot = Join-Path $siteRoot 'help'
if (-not (Test-Path -LiteralPath $helpRoot -PathType Container)) { Stop-Refused "Missing: $helpRoot" }
foreach ($page in @(Get-ChildItem -LiteralPath $helpRoot -Recurse -File -Filter '*.html' | Sort-Object FullName)) {
    $read = Read-SiteFile -Path $page.FullName
    [string]$label = $page.FullName.Substring($siteRoot.Length + 1)
    [string]$newText = Update-Markers -Text $read.Text -Values $values -Expected $null -Label $label -Changes $changes -Olds $olds
    $pending.Add(@{ File = $read; NewText = $newText; Label = $label })
}

# llms.txt: the version line and the three download lines.
$llmsPath = Join-Path $siteRoot 'llms.txt'
if (-not (Test-Path -LiteralPath $llmsPath)) { Stop-Refused "Missing: $llmsPath" }
$llms = Read-SiteFile -Path $llmsPath
[string]$llmsText = Update-OneLine -Text $llms.Text -Pattern '(?m)^(Current version: )(\d+\.\d+\.\d+)(\.)' -New $Version -What 'the "Current version:" line' -Label 'llms.txt' -Changes $changes -Olds $olds
foreach ($row in @(@('msi', '- Windows installer: '), @('dmg', '- macOS disk image: '), @('zip', '- Prompt library: '))) {
    [string]$prefix = [regex]::Escape($row[1])
    $llmsText = Update-OneLine -Text $llmsText -Pattern ('(?m)^(' + $prefix + ')(\S+)( \()') -New $values["$($row[0])-name"] -What "the `"$($row[1].Trim())`" file name" -Label 'llms.txt' -Changes $changes -Olds $olds
    $llmsText = Update-OneLine -Text $llmsText -Pattern ('(?m)^(' + $prefix + '\S+ \()([^)]*)(\))') -New $values["$($row[0])-size"] -What "the `"$($row[1].Trim())`" size" -Label 'llms.txt' -Changes $changes -Olds $olds
}
$pending.Add(@{ File = $llms; NewText = $llmsText; Label = 'llms.txt' })

# No value of the release being replaced may be left anywhere without a marker. Sizes are left
# out of this test: "1.0 MB" is too short to tell from ordinary text.
foreach ($old in $olds) {
    if ($old -match ' MB$') { continue }
    foreach ($item in $pending) {
        if ($item.NewText.Contains($old)) {
            Stop-Refused "$($item.Label) would still hold '$old', a value of the release being replaced, outside any marker."
        }
    }
}

# --- Report and write ------------------------------------------------------------------------

if ($changes.Count -eq 0) {
    Write-Host "No change needed: the site already carries release $Version with these files."
    exit 0
}

Write-Host ''
Write-Host "$($changes.Count) value(s) to change:"
foreach ($line in $changes) { Write-Host "  $line" }
Write-Host ''

[int]$written = 0
foreach ($item in $pending) {
    if ($item.NewText -ceq $item.File.Text) { continue }
    if ($PSCmdlet.ShouldProcess($item.File.Path, 'Rewrite the release values')) {
        $encoding = New-Object System.Text.UTF8Encoding($item.File.HasBom)
        [System.IO.File]::WriteAllText($item.File.Path, $item.NewText, $encoding)
        $written++
        Write-Host "Wrote $($item.Label)"
    }
}
if ($WhatIfPreference) {
    Write-Host 'WhatIf: nothing was written.'
}
else {
    Write-Host "$written file(s) written. Commit them, then upload the changed files."
}
