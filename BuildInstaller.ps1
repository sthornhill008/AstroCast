# BuildInstaller.ps1
# AstroCast Installer Build Script
#
# Usage: .\BuildInstaller.ps1
#
# Prerequisites:
#   - WiX Toolset 6 (CLI): dotnet tool install --global wix
#   - WiX UI Extension: wix extension add -g WixToolset.UI.wixext
#   - .NET 10.0 SDK
#
# Output: dist\AstroCast_{version}.msi (gitignored dist\ at the repo root)
#
# Copyright (c) 2026 Thornhill Computer Software

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  AstroCast Installer Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$projectRoot = $PSScriptRoot
$publishDir = "$projectRoot\src\AstroCast\bin\x64\Release\net10.0-windows\win-x64\publish"
$installerDir = "$projectRoot\src\AstroCast.Installer"
$ephePath = "$projectRoot\ephe"
$nativeDll = "$projectRoot\lib\swisseph\win-x64\swedll64.dll"

# A path that must not exist. AstroCast.Presentation embeds the file its
# CartoKeyFile property names as the built-in CARTO key, by default the
# profile's .astrocast\carto-key.txt; publishing with CartoKeyFile naming this
# path embeds nothing, so the MSI carries no key and every installed copy reads
# its key from carto-key.txt (public builds ship no key, CLAUDE.md, Base Map).
# Checked after the publish below.
$noKeyFile = "$projectRoot\build\no-carto-key-here"
if (Test-Path $noKeyFile) {
    Write-Host "ERROR: $noKeyFile exists; it must not, or the publish would embed it as the built-in CARTO key" -ForegroundColor Red
    exit 1
}

# Output goes to a gitignored dist\ beside the repo root, mirroring the macOS
# convention (build/macos/dist/). It used to go to the Desktop, which on this
# machine resolves to D:\OneDrive\Desktop -- so every build pushed a ~200 MB
# artifact into OneDrive sync.
$distDir = "$projectRoot\dist"

Write-Host "Project Root: $projectRoot" -ForegroundColor Gray
Write-Host "Publish Dir:  $publishDir" -ForegroundColor Gray
Write-Host ""

# Step 1: Clean previous publish
Write-Host "[1/5] Cleaning previous publish..." -ForegroundColor Yellow
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
    Write-Host "  Cleaned: $publishDir" -ForegroundColor Gray
}

# Step 2: Publish
#
# SELF-CONTAINED, deliberately. A framework-dependent MSI installs happily onto a
# machine with no .NET 10 Desktop Runtime and then fails at launch with the
# generic host error. A WiX launch condition would report that better, but it
# still leaves the user to go and install a runtime. Carrying the runtime costs
# ~139 MB (197.9 MB against 58.8 MB) and removes the prerequisite outright, which
# is the same trade the macOS bundle makes.
#
# Because the runtime ships in the package, NO LAUNCH CONDITION IS NEEDED. Its
# absence from Package.wxs is a consequence of this line, not an oversight.
Write-Host "[2/5] Publishing AstroCast (Release x64, self-contained)..." -ForegroundColor Yellow
dotnet publish "$projectRoot\src\AstroCast\AstroCast.csproj" -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:CartoKeyFile="$noKeyFile"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed!" -ForegroundColor Red
    exit 1
}

# Verify publish directory exists
if (-not (Test-Path $publishDir)) {
    Write-Host "ERROR: Publish directory not found at $publishDir" -ForegroundColor Red
    exit 1
}

# Verify the third-party notices survived publish (AstroCast.Core copies them)
if (-not (Test-Path "$publishDir\THIRD-PARTY-NOTICES.md")) {
    Write-Host "ERROR: THIRD-PARTY-NOTICES.md not found in $publishDir" -ForegroundColor Red
    exit 1
}

# Verify the licence survived publish (AstroCast.Core copies it)
if (-not (Test-Path "$publishDir\LICENSE")) {
    Write-Host "ERROR: LICENSE not found in $publishDir" -ForegroundColor Red
    exit 1
}

# Verify no CARTO key was embedded. The embedded resource's name,
# AstroCast.Presentation.CartoKey, is ASCII in the assembly's metadata, so it
# appears in the bytes read as ASCII only when a key was embedded (the C#
# constant of the same name is UTF-16 and does not match).
$presentationDll = "$publishDir\AstroCast.Presentation.dll"
if (-not (Test-Path $presentationDll)) {
    Write-Host "ERROR: AstroCast.Presentation.dll not found in $publishDir" -ForegroundColor Red
    exit 1
}
$presentationText = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($presentationDll))
if ($presentationText.Contains("AstroCast.Presentation.CartoKey")) {
    Write-Host "ERROR: AstroCast.Presentation.dll carries the AstroCast.Presentation.CartoKey resource; the MSI must ship no CARTO key" -ForegroundColor Red
    exit 1
}
Write-Host "  No built-in CARTO key (the user supplies carto-key.txt)" -ForegroundColor Gray
Write-Host "  Published to: $publishDir" -ForegroundColor Gray

# Read version from published assembly
$exePath = "$publishDir\AstroCast.exe"
if (Test-Path $exePath) {
    $fileVersion = (Get-Item $exePath).VersionInfo.FileVersion
    # Strip any trailing .0 to get 3-part version for WiX
    $msiVersion = ($fileVersion -split '\.')[0..2] -join '.'
    Write-Host "  Assembly version: $fileVersion" -ForegroundColor Gray
    Write-Host "  MSI version:      $msiVersion" -ForegroundColor Gray
} else {
    $msiVersion = "1.0.0"
    Write-Host "  Warning: Could not read version, defaulting to $msiVersion" -ForegroundColor Yellow
}

if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }
$outputMsi = "$distDir\AstroCast_${msiVersion}.msi"
Write-Host "  Output MSI:       $outputMsi" -ForegroundColor Gray

# Step 3: Copy resources to publish folder
Write-Host "[3/5] Copying resources..." -ForegroundColor Yellow

# Copy ephemeris data files to publish\ephe\
$publishEpheDir = "$publishDir\ephe"
if (-not (Test-Path $publishEpheDir)) {
    New-Item -ItemType Directory -Path $publishEpheDir | Out-Null
}
$epheFiles = Get-ChildItem -Path "$ephePath\*" -File -Include "*.se1", "sefstars.txt" -ErrorAction SilentlyContinue
if ($epheFiles.Count -gt 0) {
    foreach ($file in $epheFiles) {
        Copy-Item $file.FullName "$publishEpheDir\" -Force
    }
    Write-Host "  Copied $($epheFiles.Count) ephemeris file(s) to publish\ephe\" -ForegroundColor Gray
} else {
    Write-Host "  Warning: No ephemeris files found in $ephePath" -ForegroundColor Yellow
}

# Copy swedll64.dll if not already in publish output
if (-not (Test-Path "$publishDir\swedll64.dll")) {
    if (Test-Path $nativeDll) {
        Copy-Item $nativeDll "$publishDir\" -Force
        Write-Host "  Copied: swedll64.dll" -ForegroundColor Gray
    } else {
        Write-Host "  Warning: swedll64.dll not found at $nativeDll" -ForegroundColor Yellow
    }
} else {
    Write-Host "  swedll64.dll already in publish output" -ForegroundColor Gray
}

# The icon needs no copy step. AstroCast.ico lives at the repository root and is
# already compiled into the executable twice over -- <ApplicationIcon> gives the
# shell icon, <Resource> makes it available to WPF. An earlier version of this
# script looked for it at src\AstroCast\AstroCast.ico, where it has never been,
# and printed a misleading "not found" on every build.

# Step 4: Generate Components.wxs from actual publish contents
Write-Host "[4/5] Generating Components.wxs..." -ForegroundColor Yellow
$componentsFile = "$installerDir\Components.wxs"

# ---------------------------------------------------------------------------
# THE HARVEST IS RECURSIVE AND NAMES NOTHING.
#
# An earlier version enumerated the publish root plus a hard-coded ephe\ folder.
# That silently omitted 175 files (19.6 MB) from the self-contained payload: the
# 19 Lato fonts QuestPDF renders PDFs with, and 156 satellite resource assemblies
# across 13 culture folders. Adding "LatoFont" by name would have closed that one
# hole and left the identical hole for the next package that ships assets in a
# folder, so the walk below discovers whatever publish produced, at any depth.
# ---------------------------------------------------------------------------

# Ids derive from the path RELATIVE TO THE PUBLISH ROOT, never from the bare file
# name. The payload ships 12 resource assembly names 13 times over (once per
# culture folder), so a filename-derived id collides 13 ways and the MSI would
# carry one copy and silently drop the rest.
#
# MSI Identifier rules: 72 characters maximum, first character a letter or an
# underscore. The prefix guarantees the first character. An over-length id keeps
# the TAIL of the path -- the part a human recognises -- and takes 8 hex digits
# of SHA-256 over the full relative path to stay unique.
function New-WixId {
    param([Parameter(Mandatory)][string]$Prefix, [Parameter(Mandatory)][string]$RelativePath)

    $safe = $RelativePath -replace '[^A-Za-z0-9_]', '_'
    $id = "${Prefix}_${safe}"
    if ($id.Length -le 72) { return $id }

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($RelativePath)
        $hash = ([BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-', '').Substring(0, 8)
    }
    finally { $sha.Dispose() }

    $room = 72 - $Prefix.Length - 10
    if ($safe.Length -gt $room) { $safe = $safe.Substring($safe.Length - $room) }
    return "${Prefix}_${safe}_${hash}"
}

# Emits the <Directory> tree beneath INSTALLFOLDER, one element per discovered
# subdirectory, nested to match the publish layout.
function New-DirectoryXml {
    param([string]$AbsDir, [string]$RelDir, [int]$Indent)

    $xml = ''
    foreach ($d in (Get-ChildItem -LiteralPath $AbsDir -Directory | Sort-Object Name)) {
        $rel = if ($RelDir) { "$RelDir\$($d.Name)" } else { $d.Name }
        $dirId = New-WixId 'dir' $rel
        $pad = ' ' * $Indent
        $xml += "$pad<Directory Id=`"$dirId`" Name=`"$($d.Name)`">`r`n"
        $xml += New-DirectoryXml -AbsDir $d.FullName -RelDir $rel -Indent ($Indent + 4)
        $xml += "$pad</Directory>`r`n"
    }
    return $xml
}

# Emits one ComponentGroup per directory, then recurses. Group ids accumulate so
# the aggregate group below can reference every one of them.
$script:HarvestGroupIds = New-Object System.Collections.Generic.List[string]
$script:HarvestFileCount = 0

function New-ComponentGroupXml {
    param([string]$AbsDir, [string]$RelDir, [string]$DirectoryId)

    $groupId = if ($RelDir) { New-WixId 'grp' $RelDir } else { 'grp_root' }
    $script:HarvestGroupIds.Add($groupId)

    $xml = "        <ComponentGroup Id=`"$groupId`" Directory=`"$DirectoryId`">`r`n"
    foreach ($f in (Get-ChildItem -LiteralPath $AbsDir -File | Sort-Object Name)) {
        $rel = if ($RelDir) { "$RelDir\$($f.Name)" } else { $f.Name }
        $id = New-WixId 'f' $rel
        $xml += "            <Component Id=`"$id`" Guid=`"*`">`r`n"
        $xml += "                <File Id=`"$id`" Source=`"`$(var.PublishDir)$rel`" KeyPath=`"yes`" />`r`n"
        $xml += "            </Component>`r`n"
        $script:HarvestFileCount++
    }
    $xml += "        </ComponentGroup>`r`n`r`n"

    foreach ($d in (Get-ChildItem -LiteralPath $AbsDir -Directory | Sort-Object Name)) {
        $childRel = if ($RelDir) { "$RelDir\$($d.Name)" } else { $d.Name }
        $xml += New-ComponentGroupXml -AbsDir $d.FullName -RelDir $childRel -DirectoryId (New-WixId 'dir' $childRel)
    }
    return $xml
}

$dirTreeXml = New-DirectoryXml -AbsDir $publishDir -RelDir '' -Indent 12
$groupsXml = New-ComponentGroupXml -AbsDir $publishDir -RelDir '' -DirectoryId 'INSTALLFOLDER'

$subDirCount = (Get-ChildItem -LiteralPath $publishDir -Directory -Recurse).Count
Write-Host "  Harvested $($script:HarvestFileCount) file(s) across $($script:HarvestGroupIds.Count) directory group(s) ($subDirCount subdirectory/ies)" -ForegroundColor Gray

# Build Components.wxs
$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<!--
    Components.wxs - Auto-generated from publish folder
    Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    DO NOT EDIT - Regenerated by BuildInstaller.ps1 on each build
    Copyright (c) 2026 Thornhill Computer Software

    Harvest is recursive: every file under the publish root is included, and each
    directory gets its own Directory element and ComponentGroup. The feature in
    Package.wxs references the single aggregate group AllComponents, so a new
    payload directory needs no edit anywhere outside this generated file.
-->
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
    <Fragment>
        <DirectoryRef Id="INSTALLFOLDER">
$dirTreeXml        </DirectoryRef>

$groupsXml        <ComponentGroup Id="AllComponents">

"@

foreach ($g in $script:HarvestGroupIds) {
    $xml += "            <ComponentGroupRef Id=`"$g`" />`r`n"
}

$xml += @"
        </ComponentGroup>

        <Component Id="ProgramMenuShortcut" Directory="ApplicationProgramsFolder" Guid="*">
            <Shortcut Id="AstroCastShortcut" Name="AstroCast" Description="Astrological Chart Calculator"
                      Target="[INSTALLFOLDER]AstroCast.exe" WorkingDirectory="INSTALLFOLDER" />
            <Shortcut Id="UninstallShortcut" Name="Uninstall AstroCast"
                      Target="[SystemFolder]msiexec.exe" Arguments="/x [ProductCode]" />
            <RemoveFolder Id="RemoveApplicationProgramsFolder" On="uninstall" />
            <RegistryValue Root="HKCU" Key="Software\Thornhill Computer Software\AstroCast"
                           Name="StartMenuShortcut" Type="integer" Value="1" KeyPath="yes" />
        </Component>

        <Component Id="DesktopShortcut" Directory="DesktopFolder" Guid="*">
            <Shortcut Id="DesktopShortcut" Name="AstroCast" Description="Astrological Chart Calculator"
                      Target="[INSTALLFOLDER]AstroCast.exe" WorkingDirectory="INSTALLFOLDER" />
            <RegistryValue Root="HKCU" Key="Software\Thornhill Computer Software\AstroCast"
                           Name="DesktopShortcut" Type="integer" Value="1" KeyPath="yes" />
        </Component>
    </Fragment>
</Wix>
"@

$xml | Out-File -FilePath $componentsFile -Encoding UTF8
Write-Host "  Generated: $componentsFile" -ForegroundColor Gray

# Step 5: Build MSI
Write-Host "[5/5] Building MSI..." -ForegroundColor Yellow
Push-Location $installerDir

if (Test-Path $outputMsi) {
    Remove-Item $outputMsi -Force
}

wix build Package.wxs Directories.wxs Components.wxs -ext WixToolset.UI.wixext -d "PublishDir=$publishDir\" -d "ProductVersion=$msiVersion" -arch x64 -o "$outputMsi"
$wixResult = $LASTEXITCODE
Pop-Location

if ($wixResult -ne 0) {
    Write-Host "ERROR: WiX build failed!" -ForegroundColor Red
    exit 1
}

# Verify MSI
if (Test-Path $outputMsi) {
    $msiSize = (Get-Item $outputMsi).Length / 1MB
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Build Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "MSI Location: $outputMsi" -ForegroundColor White
    Write-Host "MSI Size:     $([math]::Round($msiSize, 2)) MB" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "ERROR: MSI file was not created!" -ForegroundColor Red
    exit 1
}
