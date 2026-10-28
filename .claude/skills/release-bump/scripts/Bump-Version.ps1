<#
.SYNOPSIS
    Sets Version, AssemblyVersion, FileVersion and InformationalVersion in every
    versioned .csproj of the AstroCast tree, all or nothing.

.DESCRIPTION
    The projects come from the tree, never from a list: every *.csproj outside
    bin/, obj/, .git/ and .claude/ that holds a <Version> element. (The table in
    CLAUDE.md has been wrong twice.)

    Everything is audited before anything is written. Each project must hold each
    of the four elements exactly once, with no uncommitted change, and all of them
    must carry one current version: X.Y.Z in Version and InformationalVersion,
    X.Y.Z.0 in AssemblyVersion and FileVersion. The new version must be the next
    patch, minor or major after it. If any check fails, no file is touched.

    Only the four elements' text is replaced; every other byte, the line endings
    and the encoding stay as they were. After writing, every file is read back
    from disk and the two forms are counted separately, because X.Y.Z is a prefix
    of X.Y.Z.0 and a bump that flattened all four fields to one form would still
    count right in total and still build.

    Runs under Windows PowerShell 5.1 and PowerShell 7.

.PARAMETER NewVersion
    The version to set, as three components: 1.3.4. Omitted, the script only
    audits: it reports the one version every project agrees on, or why they do
    not, and writes nothing.

.PARAMETER RepoRoot
    The repository root. Defaults to git's top level for the current directory.

.PARAMETER DryRun
    Audit and report what would change; write nothing.

.EXAMPLE
    pwsh -NoProfile -File .claude/skills/release-bump/scripts/Bump-Version.ps1 -NewVersion 1.3.4

.EXAMPLE
    pwsh -NoProfile -File .claude/skills/release-bump/scripts/Bump-Version.ps1
    Audit only: do all the projects agree, and on what?

.NOTES
    Exit codes: 0 done (or dry run passed); 1 aborted before writing, nothing
    changed; 2 written, but a check of the written files failed: restore them with
    git checkout -- <files> before doing anything else.
#>
[CmdletBinding()]
param(
    [string] $NewVersion,
    [string] $RepoRoot,
    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Stop-Bump([string] $Message, [int] $Code) {
    [Console]::Error.WriteLine("ABORT: $Message")
    if ($Code -eq 1) { [Console]::Error.WriteLine('Nothing was written.') }
    exit $Code
}

function Get-ElementCount([string] $Text, [string] $Pattern) {
    return ([regex]::Matches($Text, $Pattern)).Count
}

# ---------------------------------------------------------------------------
# Inputs
# ---------------------------------------------------------------------------

if ($NewVersion -and $NewVersion -notmatch '^\d+\.\d+\.\d+$') {
    Stop-Bump "NewVersion '$NewVersion' is not three numeric components (X.Y.Z)." 1
}

if (-not $RepoRoot) {
    $RepoRoot = & git rev-parse --show-toplevel 2>$null
    if (-not $RepoRoot) { Stop-Bump 'Not inside a git repository; pass -RepoRoot.' 1 }
}
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path.TrimEnd('\', '/')

# Version and InformationalVersion are three-component; the other two four.
$fields = @(
    @{ Name = 'Version';              Four = $false },
    @{ Name = 'AssemblyVersion';      Four = $true  },
    @{ Name = 'FileVersion';          Four = $true  },
    @{ Name = 'InformationalVersion'; Four = $false }
)

# Strict UTF-8 without a preamble: invalid bytes throw rather than decode to
# U+FFFD, and a BOM, if a file has one, survives as the character U+FEFF.
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)

# ---------------------------------------------------------------------------
# Discover the versioned projects from the tree
# ---------------------------------------------------------------------------

$projects = @()
foreach ($file in Get-ChildItem -LiteralPath $RepoRoot -Recurse -Filter '*.csproj' -File) {
    $relative = $file.FullName.Substring($RepoRoot.Length + 1).Replace('\', '/')
    if ($relative -match '(^|/)(bin|obj|\.git|\.claude)/') { continue }
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    try { $text = $utf8.GetString($bytes) }
    catch { Stop-Bump "$relative is not valid UTF-8." 1 }
    if ($text -notmatch '<Version[\s>]') { continue }
    $projects += [pscustomobject]@{
        Relative = $relative
        Path     = $file.FullName
        Bytes    = $bytes
        Text     = $text
    }
}

if ($projects.Count -eq 0) { Stop-Bump "No .csproj under $RepoRoot holds a <Version> element." 1 }

$ordered = [string[]]($projects | ForEach-Object { $_.Relative })
[Array]::Sort($ordered, [StringComparer]::Ordinal)
$projects = @(foreach ($r in $ordered) { $projects | Where-Object { $_.Relative -eq $r } })

Write-Output "Versioned projects, from the tree: $($projects.Count)"
foreach ($p in $projects) { Write-Output "  $($p.Relative)" }

# ---------------------------------------------------------------------------
# Audit: shape, agreement, cleanliness, successor
# ---------------------------------------------------------------------------

$problems = New-Object System.Collections.Generic.List[string]
$threeValues = @{}
$fourValues = @{}

foreach ($p in $projects) {
    foreach ($f in $fields) {
        $n = $f.Name
        # Any form of the element, attributes included, must occur exactly once,
        # so a conditional <Version Condition="..."> cannot hide beside the plain one.
        $any = Get-ElementCount $p.Text "<$n[\s>]"
        $plain = [regex]::Matches($p.Text, "<$n>([^<]*)</$n>")
        if ($any -ne 1 -or $plain.Count -ne 1) {
            $problems.Add("$($p.Relative): <$n> occurs $any time(s), $($plain.Count) in plain form; expected exactly 1.")
            continue
        }
        $value = $plain[0].Groups[1].Value
        $table = if ($f.Four) { $fourValues } else { $threeValues }
        if (-not $table.ContainsKey($value)) { $table[$value] = New-Object System.Collections.Generic.List[string] }
        $table[$value].Add("$($p.Relative) <$n>")
    }
}

if ($problems.Count -gt 0) { Stop-Bump ("Malformed version elements:`n  " + ($problems -join "`n  ")) 1 }

if ($threeValues.Count -ne 1 -or $fourValues.Count -ne 1) {
    # Name every field except those holding each form's most common value, which is
    # only counted: the odd ones out are what needs looking at.
    $lines = @()
    foreach ($form in @(@{ Label = 'three-component'; Table = $threeValues }, @{ Label = 'four-component'; Table = $fourValues })) {
        $byCount = @($form.Table.Keys | Sort-Object { $form.Table[$_].Count } -Descending)
        for ($i = 0; $i -lt $byCount.Count; $i++) {
            $v = $byCount[$i]
            $where = if ($i -eq 0) { '' } else { ': ' + ($form.Table[$v] -join '; ') }
            $lines += "  '$v' ($($form.Label)) in $($form.Table[$v].Count) field(s)$where"
        }
    }
    Stop-Bump ("The projects do not agree on one version:`n" + ($lines -join "`n")) 1
}

$current = @($threeValues.Keys)[0]
$currentFour = @($fourValues.Keys)[0]
if ($current -notmatch '^\d+\.\d+\.\d+$') { Stop-Bump "Current version '$current' is not X.Y.Z." 1 }
if ($currentFour -ne "$current.0") {
    Stop-Bump "Version/InformationalVersion read '$current' but AssemblyVersion/FileVersion read '$currentFour'; expected '$current.0'." 1
}

if (-not $NewVersion) {
    Write-Output "Current $current (all $($projects.Count) agree; $currentFour in the four-component fields). Audit only; nothing written."
    exit 0
}

$old = [version]$current
$new = [version]$NewVersion
if ($new -eq [version]::new($old.Major, $old.Minor, $old.Build + 1)) { $kind = 'patch' }
elseif ($new -eq [version]::new($old.Major, $old.Minor + 1, 0)) { $kind = 'minor' }
elseif ($new -eq [version]::new($old.Major + 1, 0, 0)) { $kind = 'major' }
else {
    $next = "$($old.Major).$($old.Minor).$($old.Build + 1), $($old.Major).$($old.Minor + 1).0 or $($old.Major + 1).0.0"
    Stop-Bump "$NewVersion does not follow $current; the next version is $next." 1
}

$gitPaths = [string[]]($projects | ForEach-Object { $_.Relative })
$dirty = & git -C $RepoRoot status --porcelain -- $gitPaths
if ($LASTEXITCODE -ne 0) { Stop-Bump 'git status failed.' 1 }
if ($dirty) {
    Stop-Bump ("Versioned projects have uncommitted changes; commit or stash them first, so the bump's diff is only the bump:`n  " + (@($dirty) -join "`n  ")) 1
}

Write-Output "Current $current (all $($projects.Count) agree; $currentFour in the four-component fields) -> $NewVersion ($kind)"

# ---------------------------------------------------------------------------
# Splice in memory and check every file before writing any
# ---------------------------------------------------------------------------

$lengthDelta = 4 * ($NewVersion.Length - $current.Length)

foreach ($p in $projects) {
    # The decode must round-trip, or re-encoding would change bytes we never meant to touch.
    if ([Convert]::ToBase64String($utf8.GetBytes($p.Text)) -ne [Convert]::ToBase64String($p.Bytes)) {
        Stop-Bump "$($p.Relative) does not round-trip through UTF-8 unchanged." 1
    }

    $text = $p.Text
    foreach ($f in $fields) {
        $n = $f.Name
        $from = if ($f.Four) { "$current.0" } else { $current }
        $to = if ($f.Four) { "$NewVersion.0" } else { $NewVersion }
        $text = $text.Replace("<$n>$from</$n>", "<$n>$to</$n>")
    }

    $oldLines = $p.Text -split "`n"
    $newLines = $text -split "`n"
    if ($oldLines.Count -ne $newLines.Count) { Stop-Bump "$($p.Relative): the line count would change." 1 }
    $changed = 0
    for ($i = 0; $i -lt $oldLines.Count; $i++) { if ($oldLines[$i] -ne $newLines[$i]) { $changed++ } }
    if ($changed -ne 4) { Stop-Bump "$($p.Relative): $changed line(s) would change; expected 4." 1 }

    $newBytes = $utf8.GetBytes($text)
    if ($newBytes.Length - $p.Bytes.Length -ne $lengthDelta) {
        Stop-Bump "$($p.Relative): the length would change by $($newBytes.Length - $p.Bytes.Length) bytes; expected $lengthDelta." 1
    }

    $p | Add-Member -NotePropertyName NewBytes -NotePropertyValue $newBytes
}

if ($DryRun) {
    Write-Output "Dry run: all $($projects.Count) files would change 4 lines each ($(4 * $projects.Count) fields), $lengthDelta byte(s) per file. Nothing written."
    exit 0
}

# ---------------------------------------------------------------------------
# Write, then check the files as they now stand on disk
# ---------------------------------------------------------------------------

foreach ($p in $projects) { [IO.File]::WriteAllBytes($p.Path, $p.NewBytes) }

$afterProblems = New-Object System.Collections.Generic.List[string]
$threeCount = 0
$fourCount = 0
$leftover = 0

foreach ($p in $projects) {
    $disk = [IO.File]::ReadAllBytes($p.Path)
    $text = $utf8.GetString($disk)
    foreach ($f in $fields) {
        $n = $f.Name
        $want = if ($f.Four) { "$NewVersion.0" } else { $NewVersion }
        $m = [regex]::Matches($text, "<$n>([^<]*)</$n>")
        if ($m.Count -ne 1 -or $m[0].Groups[1].Value -ne $want) {
            $afterProblems.Add("$($p.Relative) <$n> reads '$(($m | ForEach-Object { $_.Groups[1].Value }) -join ', ')'; expected '$want'.")
        }
    }
    $threeCount += (Get-ElementCount $text "<Version>$([regex]::Escape($NewVersion))</Version>") +
                   (Get-ElementCount $text "<InformationalVersion>$([regex]::Escape($NewVersion))</InformationalVersion>")
    $fourCount += (Get-ElementCount $text "<AssemblyVersion>$([regex]::Escape("$NewVersion.0"))</AssemblyVersion>") +
                  (Get-ElementCount $text "<FileVersion>$([regex]::Escape("$NewVersion.0"))</FileVersion>")
    foreach ($f in $fields) {
        $from = if ($f.Four) { "$current.0" } else { $current }
        $leftover += Get-ElementCount $text "<$($f.Name)>$([regex]::Escape($from))</$($f.Name)>"
    }
    if ($disk.Length - $p.Bytes.Length -ne $lengthDelta) {
        $afterProblems.Add("$($p.Relative) changed length by $($disk.Length - $p.Bytes.Length) bytes; expected $lengthDelta.")
    }
}

$expected = 2 * $projects.Count
if ($threeCount -ne $expected) { $afterProblems.Add("Three-component $NewVersion (Version, InformationalVersion): $threeCount; expected $expected.") }
if ($fourCount -ne $expected) { $afterProblems.Add("Four-component $NewVersion.0 (AssemblyVersion, FileVersion): $fourCount; expected $expected.") }
if ($leftover -ne 0) { $afterProblems.Add("$leftover field(s) still hold $current or $current.0.") }

$numstat = & git -C $RepoRoot diff --numstat -- $gitPaths
$insertions = 0
$deletions = 0
foreach ($line in @($numstat)) {
    $parts = $line -split "`t"
    $insertions += [int]$parts[0]
    $deletions += [int]$parts[1]
    if ($parts[0] -ne '4' -or $parts[1] -ne '4') { $afterProblems.Add("git diff --numstat: $line; expected 4 and 4.") }
}
if (@($numstat).Count -ne $projects.Count) { $afterProblems.Add("git diff lists $(@($numstat).Count) file(s); expected $($projects.Count).") }

if ($afterProblems.Count -gt 0) {
    Stop-Bump ("Written, but the files on disk fail their checks:`n  " + ($afterProblems -join "`n  ") +
               "`nRestore them: git checkout -- " + ($gitPaths -join ' ')) 2
}

Write-Output "Written and read back from disk:"
Write-Output "  three-component $NewVersion (Version, InformationalVersion): $threeCount of $expected"
Write-Output "  four-component $NewVersion.0 (AssemblyVersion, FileVersion): $fourCount of $expected"
Write-Output "  fields still holding $current or $current.0: $leftover"
Write-Output "  length change per file: $lengthDelta byte(s), as expected; line endings and encoding untouched"
Write-Output "  git diff --numstat: $(@($numstat).Count) files, $insertions insertions, $deletions deletions, 4/4 in each"
exit 0
