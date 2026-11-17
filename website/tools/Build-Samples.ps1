<#
.SYNOPSIS
    Renders the website's sample pages from the Markdown sources in website\samples-src.

.DESCRIPTION
    Each sample on the AstroCast website is a report AstroCast wrote, or a reading an AI
    assistant made from one, kept as Markdown in website\samples-src exactly as it was written.
    This script turns each into a page of the site's own design:

        website\AstroCast\samples\<name>\index.html     one page for each source
        website\AstroCast\samples\index.html            the list of samples

    For each source it runs pandoc (GitHub-flavoured Markdown to an HTML fragment: no syntax
    highlighting, no embedded resources, no pandoc styles, bare web addresses not turned into
    links), then:

    - shifts the sample's headings down one level, so its own title is an h2 under the page's h1,
      and closes any level a source skips;
    - wraps every table in a region that scrolls sideways on its own, labelled by the heading
      above it, and marks header cells as column headers;
    - marks the cells of each column of running text (class "wrap") and, beside such a column,
      of each column of longer phrases (class "phrase"), so that the stylesheet can keep every
      other cell on one line and give a wrapping column a readable width;
    - puts one line above a sample's first table, shown on small screens only, that says a
      wide table scrolls sideways (class "table-hint": the one piece of text inside a
      sample's article that is not the sample's own);
    - adds the note that says what the reader is looking at, the download links with file type
      and size (read from website\AstroCast\samples\files), and an "On this page" list when the
      sample has four sections or more.

    A sample marked NoIndex is unlisted: its page asks search engines not to index it and
    carries no canonical address. It is still built and still linked from the samples index.

    A report page may be an excerpt: its source in website\samples-src is part of a report, cut
    at the byte level and otherwise as AstroCast wrote it, and its Markdown download is that
    same excerpt. The script renders an excerpt as it renders any source. The sample's entry
    says what was left out in a note of its own (Note) and words the download's link as an
    excerpt (MarkdownText).

    A reading's source may be a transcription: the assistant delivered a PDF, and the Markdown
    in website\samples-src gives that PDF's words. The script renders the transcription as it
    renders any source, and offers the PDF as delivered as the page's download. The sample's
    entry says how the reading is shown in a note of its own (Note). A line break inside a
    table cell (the source's <br>) is kept, and a right-aligned column stays right-aligned.

    Nothing in a sample's text is rewritten. The script makes no network call. The pages come
    out the same on every run from the same sources, files and pandoc version; they were made
    with pandoc 3.8.2.1. Text is written as ASCII (other characters as numeric references) with
    the line endings of the system the script runs on.

    A sample whose source file is absent is left out, with its card and its links.

    To change a sample: replace its source in website\samples-src, give any changed download a
    new versioned name in website\AstroCast\samples\files (and here, in $Samples), and run this
    script again.

.PARAMETER PandocPath
    The pandoc program. By default the one on the PATH, or the per-user Windows installation.

.EXAMPLE
    .\Build-Samples.ps1

    Rebuilds every sample page and prints each page's name, size and SHA-256.
#>
[CmdletBinding()]
param(
    [string]$PandocPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$websiteRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $websiteRoot 'samples-src'
$siteRoot = Join-Path $websiteRoot 'AstroCast'
$samplesRoot = Join-Path $siteRoot 'samples'
$filesRoot = Join-Path $samplesRoot 'files'
$siteAddress = 'https://www.sthornhill.com/AstroCast/'

# A table column whose longest cell holds more than $WrapOver characters is running text: its
# cells may wrap (class "wrap"). In a table that has such a column the rows are several lines
# tall anyway, so there a column whose longest cell holds more than $PhraseOver characters may
# break onto a second line too (class "phrase"). Every other cell stays on one line: a name, a
# position (up to 21 characters), an orb, a date.
[int]$WrapOver = 32
[int]$PhraseOver = 21

# The line above a sample's first table, shown on small screens only (samples-v1.css).
[string]$TableHint = 'On a small screen, a wide table scrolls sideways.'

# The last paragraph of a report page's note. The keywords are called traditional because the
# source does: AstroCast.Core's FixedStarCatalog ("keywords from traditional sources") and
# ProfectionAnalyzer ("the traditional house themes").
[string]$KeywordNote = 'Some tables give traditional keywords, for example for fixed stars and for the houses. Some are stark. They are the tradition''s words for a symbol, not predictions about a person.'

# One entry for each sample page. Kind is report, reading or horary. Pair is the page the note
# links to. Prompt and Assistant are as each reading's own closing lines give them. NoIndex
# marks a page that is on the site but not offered to search engines.
#
# Two optional fields serve a sample that is not a whole report beside its unedited reading:
# Note holds the note's paragraphs, as HTML, in place of the kind's standard note; in it {pair}
# stands for the link to the paired page, {assistant} for Assistant and {prompt} for Prompt.
# MarkdownText is the Markdown download's link text in place of the standard one (the file type
# and size are added). The rectification pair uses both: its report page is an excerpt, and its
# reading's source is a transcription of the PDF the assistant delivered.
$Samples = @(
    @{ Key = 'natal-report'; Kind = 'report'; Title = 'Sample report: a natal chart'
       Subtitle = 'Eleanor Hartwell, an invented person'; Pair = 'natal-reading'
       Made = 'In the program it comes from the <strong>Chart</strong> panel: <strong>Calculate &amp; Save</strong>, then <strong>Save Markdown</strong>.'
       Pdf = 'natal-report-v1.pdf'; Markdown = 'natal-report-v1.md'
       Description = 'A natal chart report exactly as AstroCast 1.7.1 wrote it, for an invented person.' },
    @{ Key = 'natal-reading'; Kind = 'reading'; Title = 'Sample reading: a natal chart'
       Subtitle = 'Eleanor Hartwell, an invented person'; Pair = 'natal-report'
       Assistant = 'Codex'; Prompt = 'P01, Enhanced Birth Interpretation v3.7'
       Pdf = 'natal-reading-v1.pdf'; Markdown = ''
       Description = 'A natal chart reading written by an AI assistant from an AstroCast report and a prompt of the AstroCast prompt library.' },
    @{ Key = 'timeline-report'; Kind = 'report'; Title = 'Sample report: a transit timeline'
       Subtitle = 'Eleanor Hartwell, January to March 2027'; Pair = 'timeline-reading'
       Made = 'In the program it comes from the <strong>Transits</strong> panel: <strong>Calculate</strong>, then <strong>Export Timeline MD</strong>.'
       Pdf = ''; Markdown = 'timeline-report-v1.md'
       Description = 'A three-month transit timeline exactly as AstroCast 1.7.1 wrote it, for an invented person.' },
    @{ Key = 'timeline-reading'; Kind = 'reading'; Title = 'Sample reading: a transit timeline'
       Subtitle = 'Eleanor Hartwell, January to March 2027'; Pair = 'timeline-report'
       Assistant = 'Codex'; Prompt = 'P04, Transit Timeline v2.12'
       Pdf = 'timeline-reading-v1.pdf'; Markdown = ''
       Description = 'A transit timeline reading written by an AI assistant from an AstroCast report and a prompt of the AstroCast prompt library.' },
    @{ Key = 'rectification-report'; Kind = 'report'; Title = 'Sample report: primary directions'
       Subtitle = 'Harry, Duke of Sussex, a public figure: birth to age 43'; Pair = 'rectification-reading'
       Pdf = ''; Markdown = 'rectification-report-v1.md'
       MarkdownText = 'Download this excerpt as a Markdown file'
       Description = 'The primary directions section of a report exactly as AstroCast 1.7.1 wrote it, for a public figure: the dates a birth-time rectification works from.'
       Note = @(
           '<strong>What this is.</strong> AstroCast 1.7.1 wrote this report: the primary directions of a public figure, Harry, Duke of Sussex, from birth to age 43, cast for his published birth time. This page is an excerpt, unedited: the report''s header and its Primary Directions section. The full report also holds the natal chart, which is left out here.',
           'In the program it comes from the <strong>Primary</strong> panel, here with the age limit set to 43 and the uncertainty to 6 minutes: <strong>Calculate Directions</strong>, then <strong>Export Directions MD</strong>.',
           'An AI assistant, given the full report and a prompt from the AstroCast prompt library, tested the birth time against ten dated events. {pair}.'
       ) },
    @{ Key = 'rectification-reading'; Kind = 'reading'; Title = 'Sample reading: a birth time put to the test'
       Subtitle = 'Harry, Duke of Sussex, a public figure: ten dated events'; Pair = 'rectification-report'
       Assistant = 'ChatGPT'; Prompt = 'P29, Primary Directions Interpretation v1.0'
       Pdf = 'rectification-reading-v1.pdf'; Markdown = ''
       Description = 'A birth-time rectification written by an AI assistant from an AstroCast primary directions report and a prompt of the AstroCast prompt library.'
       Note = @(
           '<strong>What this is.</strong> An AI assistant, {assistant}, wrote this reading from an AstroCast report, using prompt {prompt}, of the AstroCast prompt library. Its subject is a public figure, Harry, Duke of Sussex. It tests his published birth time against ten dated events of his life.',
           '<strong>What was asked.</strong> With the prompt and the report attached, the request was: &#8220;I want to rectify this chart with the uploaded prompt. This is a chart of a public figure whose biography is well known. Research this person to find significant biographical events that can be used for rectification. Include a table of events used for rectification in the PDF report.&#8221; The assistant found the events and their sources itself.',
           '<strong>How it is shown.</strong> The assistant delivered a PDF. This page gives its words unchanged. The PDF also draws the ten birth-time shifts as a chart, given here as a list, and its sources are links there.',
           'The prompts are an aid to an astrologer, not a replacement for one. The assistant gathers the dated events, sets them against the directions and shows where they agree and where they do not. Checking the events and weighing the matches stay with you. {pair}.'
       ) },
    @{ Key = 'synastry-report'; Kind = 'report'; Title = 'Sample report: a synastry'
       Subtitle = 'Eleanor Hartwell and Daniel Whitfield, two invented people'; Pair = 'synastry-reading'
       Made = 'In the program it comes from the <strong>Synastry</strong> panel: <strong>Calculate Synastry</strong>, then <strong>Export Synastry MD</strong>.'
       Pdf = ''; Markdown = 'synastry-report-v1.md'
       Description = 'A synastry report, two charts compared, exactly as AstroCast 1.7.1 wrote it, for two invented people.' },
    @{ Key = 'synastry-reading'; Kind = 'reading'; Title = 'Sample reading: a synastry'
       Subtitle = 'Eleanor Hartwell and Daniel Whitfield, two invented people'; Pair = 'synastry-report'
       Assistant = 'Codex'; Prompt = 'P12, Synastry Interpretation v1.13'
       Pdf = 'synastry-reading-v1.pdf'; Markdown = ''
       Description = 'A synastry reading written by an AI assistant from an AstroCast report and a prompt of the AstroCast prompt library.' },
    @{ Key = 'horary-reading'; Kind = 'horary'; Title = 'Sample reading: a horary question'
       Subtitle = 'A real question, shared by the developer'; Pair = ''
       Prompt = 'P15, Horary Chart Interpretation v2.10'; NoIndex = $true
       Pdf = 'horary-reading-v1.pdf'; Markdown = ''
       Description = 'A horary reading of a real question, shared by the developer, with a note of what happened afterwards.' }
)

# The cards of the samples index, in order. Each names the pages it links to.
$Cards = @(
    @{ Heading = 'Three months of transits'; Text = 'The transit timeline of Eleanor Hartwell, an invented person, for January to March 2027, and the dated reading made from it.'; Pages = @('timeline-report', 'timeline-reading') },
    @{ Heading = 'A birth time put to the test'; Text = 'Harry, Duke of Sussex, a public figure: his primary directions from birth to age 43, and a reading that tests his published birth time against ten dated events.'; Pages = @('rectification-report', 'rectification-reading') },
    @{ Heading = 'A natal chart'; Text = 'Eleanor Hartwell, an invented person: the chart report, and the full reading made from it.'; Pages = @('natal-report', 'natal-reading') },
    @{ Heading = 'Two charts compared'; Text = 'The synastry of the two invented charts, Eleanor Hartwell and Daniel Whitfield, and its reading.'; Pages = @('synastry-report', 'synastry-reading') },
    @{ Heading = 'A question asked of the sky'; Text = 'A horary chart answers a question from the moment it is asked. This one is a real question, with a note of what happened.'; Pages = @('horary-reading') }
)

function ConvertTo-Ascii {
    <#
    .SYNOPSIS
        Replaces every character outside ASCII with its numeric character reference.
    .PARAMETER Text
        HTML text.
    .OUTPUTS
        The same HTML in ASCII.
    #>
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text)
    $builder = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $Text.Length; $i++) {
        [int]$code = [int][char]$Text[$i]
        if ($code -lt 128) { $null = $builder.Append($Text[$i]) }
        elseif ([char]::IsHighSurrogate($Text[$i]) -and $i + 1 -lt $Text.Length) {
            $null = $builder.Append('&#' + [char]::ConvertToUtf32($Text[$i], $Text[$i + 1]) + ';')
            $i++
        }
        else { $null = $builder.Append('&#' + $code + ';') }
    }
    return $builder.ToString()
}

function Get-PlainText {
    <#
    .SYNOPSIS
        The text of an HTML fragment: tags removed, references decoded, whitespace collapsed.
    .PARAMETER Html
        The fragment.
    #>
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Html)
    [string]$stripped = [regex]::Replace($Html, '<[^>]+>', '')
    return ([regex]::Replace([System.Net.WebUtility]::HtmlDecode($stripped), '\s+', ' ')).Trim()
}

function ConvertTo-Attribute {
    <#
    .SYNOPSIS
        Encodes plain text for use inside a double-quoted HTML attribute, in ASCII.
    .PARAMETER Text
        The plain text.
    #>
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text)
    [string]$encoded = $Text.Replace('&', '&amp;').Replace('"', '&quot;').Replace('<', '&lt;').Replace('>', '&gt;')
    return ConvertTo-Ascii -Text $encoded
}

function Get-SizeText {
    <#
    .SYNOPSIS
        A file's size for a link's text: whole kilobytes of 1,000 bytes.
    .PARAMETER Path
        The file.
    #>
    param([Parameter(Mandatory = $true)][string]$Path)
    [long]$bytes = (Get-Item -LiteralPath $Path).Length
    return ([Math]::Round($bytes / 1000.0)).ToString([System.Globalization.CultureInfo]::InvariantCulture) + ' KB'
}

function Set-CellWrap {
    <#
    .SYNOPSIS
        Marks the cells of a table's running-text columns, so that the stylesheet lets them wrap.
    .DESCRIPTION
        A column whose longest body cell holds more than $WrapOver characters of text is a column
        of running text, and each of its body cells gets the class "wrap". In a table that has
        such a column, each body cell of a column whose longest cell holds more than $PhraseOver
        characters gets the class "phrase". Every other body cell is short (a name, a position,
        an orb, a date) and the stylesheet keeps it on one line. No cell's text is changed.
    .PARAMETER TableHtml
        One table element, as pandoc wrote it.
    .PARAMETER Label
        The sample's name and the table's number, for messages.
    .OUTPUTS
        The table, with the class added where it applies.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$TableHtml,
        [Parameter(Mandatory = $true)][string]$Label
    )
    if ($TableHtml -match '(col|row)span=') { throw "$Label has merged cells, which this script does not measure." }
    $bodyMatch = [regex]::Match($TableHtml, '(?s)<tbody>.*?</tbody>')
    if (-not $bodyMatch.Success) { return $TableHtml }
    [string]$cellPattern = '<tr>|<td( class="[^"]*")?>'
    if ([regex]::Matches($bodyMatch.Value, '<t[rd]\b').Count -ne [regex]::Matches($bodyMatch.Value, $cellPattern).Count) {
        throw "$Label has a row or cell with an attribute this script does not know."
    }
    [int]$columnCount = [regex]::Matches([regex]::Match($TableHtml, '(?s)<thead>.*?</thead>').Value, '<th[ >]').Count
    [int[]]$longest = New-Object int[] $columnCount
    foreach ($rowMatch in [regex]::Matches($bodyMatch.Value, '(?s)<tr>(.*?)</tr>')) {
        $cellMatches = [regex]::Matches($rowMatch.Groups[1].Value, '(?s)<td[^>]*>(.*?)</td>')
        if ($cellMatches.Count -ne $columnCount) { throw "$Label has a row of $($cellMatches.Count) cells under $columnCount headers." }
        for ([int]$i = 0; $i -lt $columnCount; $i++) {
            [int]$length = (Get-PlainText -Html $cellMatches[$i].Groups[1].Value).Length
            if ($length -gt $longest[$i]) { $longest[$i] = $length }
        }
    }
    [bool]$hasRunningText = @($longest | Where-Object { $_ -gt $script:WrapOver }).Count -gt 0
    $place = @{ Column = 0 }
    [string]$newBody = [regex]::Replace($bodyMatch.Value, $cellPattern, {
            param($c)
            if ($c.Value -eq '<tr>') { $place.Column = 0; return $c.Value }
            [int]$index = $place.Column
            $place.Column++
            [string]$mark = if ($longest[$index] -gt $script:WrapOver) { 'wrap' } elseif ($hasRunningText -and $longest[$index] -gt $script:PhraseOver) { 'phrase' } else { '' }
            if (-not $mark) { return $c.Value }
            if ($c.Groups[1].Success) { return $c.Value.Replace('">', ' ' + $mark + '">') }
            return '<td class="' + $mark + '">'
        })
    return $TableHtml.Substring(0, $bodyMatch.Index) + $newBody + $TableHtml.Substring($bodyMatch.Index + $bodyMatch.Length)
}

function Convert-Sample {
    <#
    .SYNOPSIS
        Renders one Markdown source to the fragment placed in its page.
    .PARAMETER SourcePath
        The Markdown file.
    .PARAMETER Label
        The sample's name, for messages.
    .OUTPUTS
        A hashtable: Html (the fragment), Sections (the list for "On this page": Id and Text),
        Tables (the number of tables), Notes (messages about adjusted headings).
    #>
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$Label
    )
    [string]$outFile = Join-Path ([System.IO.Path]::GetTempPath()) ('astrocast-sample-' + [System.Guid]::NewGuid().ToString('N') + '.html')
    try {
        & $script:pandoc --from=gfm-autolink_bare_uris --to=html5 --wrap=none --columns=100000 `
            --syntax-highlighting=none --shift-heading-level-by=1 --ascii --sandbox `
            --output=$outFile $SourcePath
        if ($LASTEXITCODE -ne 0) { throw "pandoc failed on $Label (exit $LASTEXITCODE)." }
        [string]$html = [System.IO.File]::ReadAllText($outFile)
    }
    finally {
        if (Test-Path -LiteralPath $outFile) { Remove-Item -LiteralPath $outFile }
    }
    $html = $html.Replace("`r`n", "`n").TrimEnd("`n")

    # Alignment as a class, not an inline style; header cells as column headers.
    $html = [regex]::Replace($html, ' style="text-align: (left|right|center);"', { param($m) if ($m.Groups[1].Value -eq 'left') { '' } else { ' class="ta-' + $m.Groups[1].Value + '"' } })
    if ($html -match ' style="') { throw "$Label holds an inline style this script does not know." }
    $html = [regex]::Replace($html, '<th(?=[ >])', '<th scope="col"')

    # Headings: keep the levels in step (no level skipped under the page's h1), and give every
    # id a prefix so that none can meet an id of the page itself.
    $notes = New-Object System.Collections.Generic.List[string]
    $sections = New-Object System.Collections.Generic.List[object]
    $state = @{ Previous = 1 }
    $html = [regex]::Replace($html, '(?s)<h([1-6])( id="([^"]*)")?>(.*?)</h\1>', {
            param($m)
            [int]$level = [int]$m.Groups[1].Value
            [string]$inner = $m.Groups[4].Value
            [int]$use = $level
            if ($level -gt $state.Previous + 1) {
                $use = $state.Previous + 1
                $notes.Add("${Label}: heading '$(Get-PlainText -Html $inner)' skipped from h$($state.Previous) to h$level; shown as h$use")
            }
            $state.Previous = $use
            [string]$id = if ($m.Groups[3].Success -and $m.Groups[3].Value) { 's-' + $m.Groups[3].Value } else { '' }
            if ($use -eq 3 -and $id) { $sections.Add(@{ Id = $id; Html = $inner }) }
            [string]$idAttribute = if ($id) { ' id="' + $id + '"' } else { '' }
            return "<h$use$idAttribute>$inner</h$use>"
        })
    if ($html -match '<h1[ >]') { throw "$Label still holds an h1 after the heading shift." }

    # Tables: each in its own scrolling region, labelled by the heading above it, with the cells
    # of its running-text columns marked. The line about sideways scrolling goes above the first.
    $labelCounts = @{}
    $tables = @{ Count = 0 }
    $heading = @{ Text = 'the start of the sample' }
    $html = [regex]::Replace($html, '(?s)<h[2-6][^>]*>(.*?)</h[2-6]>|<table>.*?</table>', {
            param($m)
            if ($m.Value.StartsWith('<h')) {
                $heading.Text = Get-PlainText -Html $m.Groups[1].Value
                return $m.Value
            }
            if ($m.Value -notmatch '<th[ >]') { throw "$Label has a table without header cells under '$($heading.Text)'." }
            $tables.Count++
            [string]$name = 'Table: ' + $heading.Text
            if ($labelCounts.ContainsKey($name)) { $labelCounts[$name]++; $name = $name + ' (' + $labelCounts[$name] + ')' }
            else { $labelCounts[$name] = 1 }
            [string]$hint = if ($tables.Count -eq 1) { '<p class="table-hint">' + $script:TableHint + '</p>' + "`n" } else { '' }
            [string]$marked = Set-CellWrap -TableHtml $m.Value -Label "$Label, table $($tables.Count)"
            return $hint + '<div class="table-wrap" tabindex="0" role="region" aria-label="' + (ConvertTo-Attribute -Text $name) + '">' + "`n" + $marked + "`n" + '</div>'
        })
    if ($html -match '<a\s[^>]*href="(https?:|//)') { throw "$Label would link to another host." }
    if ($html -match '<(script|img|iframe|link|style|object|embed|video|audio)\b') { throw "$Label holds an element the sample pages do not allow." }

    return @{ Html = $html; Sections = $sections; Tables = $tables.Count; Notes = $notes }
}

function Get-DownloadLinks {
    <#
    .SYNOPSIS
        The paragraph of download links for a sample page, with file type and size in each link.
    .PARAMETER Sample
        The sample's entry.
    .PARAMETER Prefix
        The relative path from the page to the samples folder.
    #>
    param([Parameter(Mandatory = $true)][hashtable]$Sample, [Parameter(Mandatory = $true)][string]$Prefix)
    $links = New-Object System.Collections.Generic.List[string]
    [string]$what = if ($Sample.Kind -eq 'report') { 'report' } else { 'reading' }
    if ($Sample.Pdf) {
        $links.Add('<a href="' + $Prefix + 'files/' + $Sample.Pdf + '">Download this ' + $what + ' as a PDF (PDF, ' + (Get-SizeText -Path (Join-Path $filesRoot $Sample.Pdf)) + ')</a>')
    }
    if ($Sample.Markdown) {
        [string]$markdownText = if ($Sample.ContainsKey('MarkdownText')) { $Sample.MarkdownText } else { 'Download the Markdown file as AstroCast wrote it' }
        $links.Add('<a href="' + $Prefix + 'files/' + $Sample.Markdown + '" download>' + $markdownText + ' (Markdown, ' + (Get-SizeText -Path (Join-Path $filesRoot $Sample.Markdown)) + ')</a>')
    }
    return '            <p class="downloads">' + ($links -join ' ') + '</p>'
}

function Get-PageHead {
    <#
    .SYNOPSIS
        The page from the doctype to the opening of main: head, skip link, band header, breadcrumb.
    .PARAMETER Title
        The page's h1 and the start of its title element.
    .PARAMETER Subtitle
        The line under the h1 in the band.
    .PARAMETER Description
        The meta description.
    .PARAMETER Canonical
        The page's address under the site.
    .PARAMETER Root
        The relative path from the page to the site's root folder ('../' or '../../').
    .PARAMETER Crumbs
        The breadcrumb's list items, as HTML.
    .PARAMETER NoIndex
        The page is unlisted: it gets a robots meta that says noindex, and no canonical link
        (a canonical address names the address to index, and this page asks not to be indexed).
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$Subtitle,
        [Parameter(Mandatory = $true)][string]$Description,
        [Parameter(Mandatory = $true)][string]$Canonical,
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Crumbs,
        [switch]$NoIndex
    )
    [string]$robotsLine = if ($NoIndex) { '    <meta name="robots" content="noindex">' + "`n" } else { '' }
    [string]$canonicalLine = if ($NoIndex) { '' } else { '    <link rel="canonical" href="' + $siteAddress + $Canonical + '">' + "`n" }
    return @"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta name="theme-color" media="(prefers-color-scheme: light)" content="#1B2838">
    <meta name="theme-color" media="(prefers-color-scheme: dark)" content="#1B2838">
    <meta name="description" content="$Description">
    <meta name="author" content="Steve Thornhill">
$robotsLine    <title>$Title - AstroCast</title>
$canonicalLine    <link rel="icon" href="${Root}favicon.ico" sizes="any">
    <link rel="apple-touch-icon" href="${Root}images/apple-touch-icon-v1.png">
    <link rel="stylesheet" href="${Root}samples-v1.css">
</head>
<body>
    <a class="skip" href="#main">Skip to content</a>

    <header class="band">
        <div class="container">
            <h1>$Title</h1>
            <p>$Subtitle</p>
        </div>
    </header>

    <nav class="breadcrumb" aria-label="Breadcrumb">
        <div class="container">
            <ol>
$Crumbs
            </ol>
        </div>
    </nav>

    <main id="main">
        <div class="container">

"@
}

function Get-PageFoot {
    <#
    .SYNOPSIS
        The page from the close of main to the end: the band footer.
    .PARAMETER Root
        The relative path from the page to the site's root folder.
    .PARAMETER SamplesIndex
        The relative path from the page to the samples index.
    #>
    param([Parameter(Mandatory = $true)][string]$Root, [Parameter(Mandatory = $true)][string]$SamplesIndex)
    return @"

        </div>
    </main>

    <footer class="band">
        <div class="container">
            <p><strong>AstroCast</strong> by Steve Thornhill</p>
            <ul>
                <li><a href="${Root}index.html">AstroCast home and downloads</a></li>
                <li><a href="$SamplesIndex">Samples</a></li>
                <li><a href="${Root}help/index.html">Help</a></li>
                <li><a href="${Root}docs/AstroCast_User_Guide-v1.pdf">User Guide (PDF)</a></li>
                <li><a href="${Root}docs/AstroCast_Prompt_Library_Guide-v1.pdf">Prompt Library Guide (PDF)</a></li>
            </ul>
            <p>Copyright &copy; 2026 Thornhill Computer Software</p>
        </div>
    </footer>
</body>
</html>
"@
}

function Write-Page {
    <#
    .SYNOPSIS
        Writes a page as ASCII with the system's line endings, and prints its size and SHA-256.
    .PARAMETER Path
        The file to write.
    .PARAMETER Html
        The page, with LF line endings.
    #>
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string]$Html)
    [string]$text = (ConvertTo-Ascii -Text $Html.Replace("`r`n", "`n")).TrimEnd("`n") + "`n"
    $text = $text.Replace("`n", [Environment]::NewLine)
    [string]$folder = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $folder)) { $null = New-Item -ItemType Directory -Path $folder -Force }
    [System.IO.File]::WriteAllText($Path, $text, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("{0} | {1} bytes | sha256 {2}" -f $Path.Substring($siteRoot.Length + 1), (Get-Item -LiteralPath $Path).Length, (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant())
}

# --- Find pandoc -----------------------------------------------------------------------------

if ($PandocPath) { $script:pandoc = $PandocPath }
else {
    $found = Get-Command pandoc -ErrorAction SilentlyContinue
    if ($found) { $script:pandoc = $found.Source }
    elseif ($env:LOCALAPPDATA -and (Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'Pandoc\pandoc.exe'))) { $script:pandoc = Join-Path $env:LOCALAPPDATA 'Pandoc\pandoc.exe' }
    else { throw 'pandoc was not found. Install it, or pass -PandocPath.' }
}
[string]$pandocVersion = (& $script:pandoc --version | Select-Object -First 1)
Write-Host "Rendering with $pandocVersion"

# --- The sample pages ------------------------------------------------------------------------

$present = @{}
foreach ($sample in $Samples) {
    if (Test-Path -LiteralPath (Join-Path $sourceRoot ($sample.Key + '.md'))) { $present[$sample.Key] = $sample }
    else { Write-Host "Left out (no source): $($sample.Key)" }
}

[int]$tableTotal = 0
foreach ($sample in $Samples) {
    if (-not $present.ContainsKey($sample.Key)) { continue }
    foreach ($file in @($sample.Pdf, $sample.Markdown)) {
        if ($file -and -not (Test-Path -LiteralPath (Join-Path $filesRoot $file))) { throw "Missing download for $($sample.Key): $file" }
    }
    $rendered = Convert-Sample -SourcePath (Join-Path $sourceRoot ($sample.Key + '.md')) -Label $sample.Key
    foreach ($note in $rendered.Notes) { Write-Host "NOTE: $note" }
    $tableTotal += $rendered.Tables

    [string]$pairLink = ''
    if ($sample.Pair -and $present.ContainsKey($sample.Pair)) {
        [string]$pairWords = if ($sample.Kind -eq 'report') { 'Read the reading made from this report' } else { 'See the report this reading was made from' }
        $pairLink = '<a href="../' + $sample.Pair + '/index.html">' + $pairWords + '</a>'
    }

    [string]$kindOfNote = if ($sample.ContainsKey('Note')) { 'own' } else { $sample.Kind }
    [string]$note = switch ($kindOfNote) {
        'own' {
            [string]$assistant = if ($sample.ContainsKey('Assistant')) { $sample.Assistant } else { '' }
            [string]$promptName = if ($sample.ContainsKey('Prompt')) { $sample.Prompt } else { '' }
            if (-not $pairLink -and (@($sample.Note) -join ' ').Contains('{pair}')) { throw "The note of $($sample.Key) names its pair, and the pair's source is absent." }
            (@($sample.Note) | ForEach-Object { '                <p>' + $_.Replace('{pair}', $pairLink).Replace('{assistant}', $assistant).Replace('{prompt}', $promptName) + '</p>' }) -join "`n"
        }
        'report' {
            '                <p><strong>What this is.</strong> AstroCast 1.7.1 wrote this report. It is shown here unedited, and the person it describes is invented. ' + $sample.Made + '</p>' + "`n" +
            '                <p>An AI assistant, given this report and a prompt from the AstroCast prompt library, wrote a reading from it. ' + $pairLink + '.</p>' + "`n" +
            '                <p>' + $KeywordNote + '</p>'
        }
        'reading' {
            '                <p><strong>What this is.</strong> An AI assistant, ' + $sample.Assistant + ', wrote this reading from an AstroCast report, using prompt ' + $sample.Prompt + ', of the AstroCast prompt library. It is shown here unedited, and the person it describes is invented.</p>' + "`n" +
            '                <p>A reading is an interpretation, not fact. ' + $pairLink + '.</p>'
        }
        'horary' {
            '                <p><strong>What this is.</strong> A horary chart answers a question from the moment it is asked. This is a real question, shared by the developer, with a note of what happened afterwards.</p>' + "`n" +
            '                <p>The reading was made with prompt ' + $sample.Prompt + ', of the AstroCast prompt library, from an AstroCast 1.7.0 report. The report itself is not shown.</p>'
        }
    }

    [string]$toc = ''
    if ($rendered.Sections.Count -ge 4) {
        $items = foreach ($section in $rendered.Sections) { '                    <li><a href="#' + $section.Id + '">' + [regex]::Replace($section.Html, '<[^>]+>', '') + '</a></li>' }
        $toc = "            <nav class=`"toc`" aria-label=`"On this page`">`n                <h2>On this page</h2>`n                <ol>`n" + ($items -join "`n") + "`n                </ol>`n            </nav>`n`n"
    }

    [string]$downloads = Get-DownloadLinks -Sample $sample -Prefix '../'
    [string]$crumbs = "                <li><a href=`"../../index.html`">AstroCast</a></li>`n                <li><a href=`"../index.html`">Samples</a></li>`n                <li aria-current=`"page`">$($sample.Title)</li>"
    [string]$more = '            <p class="more-links">' + $(if ($pairLink) { $pairLink + ' ' } else { '' }) + '<a href="../index.html">All samples</a> <a href="../../index.html#download">Download AstroCast</a></p>'

    [bool]$unlisted = $sample.ContainsKey('NoIndex') -and $sample.NoIndex
    [string]$page = (Get-PageHead -Title $sample.Title -Subtitle $sample.Subtitle -Description $sample.Description -Canonical ('samples/' + $sample.Key + '/') -Root '../../' -Crumbs $crumbs -NoIndex:$unlisted) +
        "            <div class=`"note-box`">`n$note`n            </div>`n`n$downloads`n`n$toc" +
        "            <article class=`"sample`">`n" + $rendered.Html + "`n            </article>`n`n$downloads`n$more" +
        (Get-PageFoot -Root '../../' -SamplesIndex '../index.html')
    Write-Page -Path (Join-Path $samplesRoot ($sample.Key + '\index.html')) -Html $page
}

# --- The samples index -----------------------------------------------------------------------

$cardHtml = New-Object System.Collections.Generic.List[string]
foreach ($card in $Cards) {
    $pages = @($card.Pages | Where-Object { $present.ContainsKey($_) })
    if ($pages.Count -eq 0) { continue }
    $links = foreach ($key in $pages) {
        [string]$words = if ($present[$key].Kind -eq 'report') { 'The report' } else { 'The reading' }
        '<a href="' + $key + '/index.html" aria-label="' + $words + ': ' + $card.Heading + '">' + $words + '</a>'
    }
    $cardHtml.Add("                <li>`n                    <h2>$($card.Heading)</h2>`n                    <p>$($card.Text)</p>`n                    <p>" + ($links -join ' ') + "</p>`n                </li>")
}
[string]$indexCrumbs = "                <li><a href=`"../index.html`">AstroCast</a></li>`n                <li aria-current=`"page`">Samples</li>"
[string]$indexPage = (Get-PageHead -Title 'Sample reports and readings' -Subtitle 'What AstroCast writes, and what an AI assistant makes of it' -Description 'Sample AstroCast reports, each beside the reading an AI assistant wrote from it: a transit timeline, a birth time put to the test, a natal chart and a synastry.' -Canonical 'samples/index.html' -Root '../' -Crumbs $indexCrumbs) +
    "            <div class=`"note-box`">`n" +
    "                <p><strong>How to read these.</strong> AstroCast writes the report: the positions, aspects and timings, as a plain file. An AI assistant, given that report and a prompt from the AstroCast prompt library, writes the reading: it presents the report, organises it and highlights the patterns in it. Each page says what it shows and how it was made.</p>`n" +
    "                <p>The prompts are an aid to an astrologer, not a replacement for one. A reading is an interpretation, not fact.</p>`n" +
    "            </div>`n`n" +
    "            <ul class=`"sample-cards`">`n" + ($cardHtml -join "`n") + "`n            </ul>`n`n" +
    "            <p class=`"more-links`"><a href=`"../index.html#download`">Download AstroCast</a> <a href=`"../help/first-reading/index.html`">Your first chart and reading, step by step</a></p>" +
    (Get-PageFoot -Root '../' -SamplesIndex 'index.html')
Write-Page -Path (Join-Path $samplesRoot 'index.html') -Html $indexPage

Write-Host "$($present.Count) sample page(s) and the index written; $tableTotal table(s) wrapped."
