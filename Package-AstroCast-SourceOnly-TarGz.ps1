# Package-AstroCast-SourceOnly-TarGz.ps1
# Creates a source code archive of AstroCast containing only source files
# Outputs as .tar.gz format with version number from .csproj file
# Created: 2026-02-26
# Based on: Package-PhotoDB-SourceOnly-TarGz.ps1

param(
    [string]$OutputPath = "",  # If empty, will be auto-generated with version
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "AstroCast Source Code Packager" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Determine the project directory
# Priority: 1) Known location, 2) Current directory, 3) Other common locations
$projectDir = $null

# First, try the known AstroCast location
$knownLocation = "D:\OneDrive\Code\AstroCast"
if (Test-Path (Join-Path $knownLocation "AstroCast.slnx")) {
    # Verify it has the expected structure (src\AstroCast.Core\AstroCast.Core.csproj)
    if (Test-Path (Join-Path $knownLocation "src\AstroCast.Core\AstroCast.Core.csproj")) {
        $projectDir = $knownLocation
        Write-Host "Found project at: $knownLocation" -ForegroundColor Gray
    }
}

# If not found, try current directory
if ($null -eq $projectDir) {
    $currentDir = Get-Location
    $testPath = Join-Path $currentDir "AstroCast.slnx"
    $testCsproj = Join-Path $currentDir "src\AstroCast.Core\AstroCast.Core.csproj"
    if ((Test-Path $testPath) -and (Test-Path $testCsproj)) {
        $projectDir = $currentDir
        Write-Host "Found project at current directory" -ForegroundColor Gray
    }
}

# If still not found, try other common locations
if ($null -eq $projectDir) {
    $commonLocations = @(
        "$env:USERPROFILE\OneDrive\Code\AstroCast",
        "$env:USERPROFILE\Source\Repos\AstroCast",
        "$env:USERPROFILE\Documents\AstroCast"
    )
    
    foreach ($loc in $commonLocations) {
        $testCsproj = Join-Path $loc "src\AstroCast.Core\AstroCast.Core.csproj"
        if ((Test-Path (Join-Path $loc "AstroCast.slnx")) -and (Test-Path $testCsproj)) {
            $projectDir = $loc
            Write-Host "Found project at: $loc" -ForegroundColor Gray
            break
        }
    }
}

# Final check
if ($null -eq $projectDir) {
    Write-Host "ERROR: Could not find AstroCast project!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please either:" -ForegroundColor Yellow
    Write-Host "  1. Run this script from the AstroCast solution directory" -ForegroundColor White
    Write-Host "  2. Place this script in the AstroCast solution directory" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Change to project directory
$originalLocation = Get-Location
Set-Location $projectDir
Write-Host "Working directory: $projectDir" -ForegroundColor Green

# Verify we found the solution
$slnFiles = Get-ChildItem -Path $projectDir -Filter "*.slnx" -File
Write-Host "Solution file: $($slnFiles[0].Name)" -ForegroundColor Green
Write-Host ""

# Extract version number from AstroCast.Core.csproj
Write-Host "Reading version information..." -ForegroundColor Cyan
$csprojPath = Join-Path $projectDir "src\AstroCast.Core\AstroCast.Core.csproj"

if (-not (Test-Path $csprojPath)) {
    Write-Host "ERROR: Could not find AstroCast.Core.csproj at expected location!" -ForegroundColor Red
    Write-Host "Expected path: $csprojPath" -ForegroundColor Yellow
    Set-Location $originalLocation
    exit 1
}

Write-Host "  Reading from: $csprojPath" -ForegroundColor Gray

try {
    # Read the .csproj file as XML
    [xml]$csprojXml = Get-Content $csprojPath
    
    # Extract AssemblyVersion - handle multiple PropertyGroup elements
    $assemblyVersion = $null
    
    # Check if PropertyGroup is an array (multiple elements) or single element
    $propertyGroups = $csprojXml.Project.PropertyGroup
    if ($propertyGroups -is [System.Array]) {
        # Multiple PropertyGroup elements - search through all of them
        foreach ($pg in $propertyGroups) {
            if ($pg.AssemblyVersion) {
                $assemblyVersion = $pg.AssemblyVersion
                break
            }
        }
    } else {
        # Single PropertyGroup element
        $assemblyVersion = $propertyGroups.AssemblyVersion
    }
    
    if ([string]::IsNullOrEmpty($assemblyVersion)) {
        throw "AssemblyVersion not found in any PropertyGroup in .csproj file"
    }
    
    # Parse version (format: Major.Minor.Build.Revision)
    $versionParts = $assemblyVersion.Split('.')
    if ($versionParts.Length -lt 3) {
        throw "Invalid AssemblyVersion format: $assemblyVersion (expected at least Major.Minor.Build)"
    }
    
    $major = $versionParts[0]
    $minor = $versionParts[1]
    $build = $versionParts[2]
    
    $versionString = "$major.$minor.$build"
    $versionStringUnderscore = "${major}_${minor}_${build}"
    
    Write-Host "  AssemblyVersion: $assemblyVersion" -ForegroundColor White
    Write-Host "  Using version: $versionString" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "ERROR: Failed to read version from .csproj file" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Falling back to default filename without version" -ForegroundColor Yellow
    $versionStringUnderscore = ""
}

# Set output filename with version if not specified
if ([string]::IsNullOrEmpty($OutputPath)) {
    if ([string]::IsNullOrEmpty($versionStringUnderscore)) {
        $OutputPath = "Package-AstroCast-SourceOnly.tar.gz"
    } else {
        $OutputPath = "Package-AstroCast-SourceOnly-${versionStringUnderscore}.tar.gz"
    }
    Write-Host "Output filename: $OutputPath" -ForegroundColor Green
    Write-Host ""
}

# Define source code file extensions to include
$includeExtensions = @(
    # Source code files
    "*.cs",           # C# source files
    "*.xaml",         # WPF UI files
    "*.csproj",       # Project files
    "*.slnx",          # Solution file
    "*.md",           # Documentation files
    "*.ps1",          # PowerShell scripts
    
    # WiX installer files
    "*.wxs",          # WiX source files
    "*.wixproj",      # WiX project files
    
    # Configuration and build files
    "*.json",         # JSON config files (e.g., appsettings, launchSettings)
    "*.props",        # MSBuild props files (e.g., Directory.Build.props)
    "*.targets",      # MSBuild targets files
    "*.editorconfig", # Editor configuration
    "*.gitignore",    # Git ignore rules
    "*.gitattributes" # Git attributes
)

# Also include specific files that might not match extensions
$includeSpecificFiles = @(
    "CLAUDE.md",
    "LICENSE",
    "LICENSE.txt",
    "README.md",
    "Directory.Build.props"
)

# Define directories to exclude
$excludeDirs = @(
    "bin",
    "obj",
    ".vs",
    ".git",
    "packages",
    "TestResults",
    ".vscode",
    ".idea",
    "node_modules",
    "release",
    "publish",
    "BenchmarkDotNet.Artifacts"
)

Write-Host "Including file types:" -ForegroundColor Cyan
foreach ($ext in $includeExtensions) {
    Write-Host "  * $ext" -ForegroundColor White
}
Write-Host ""

Write-Host "Collecting source files..." -ForegroundColor Cyan

# Get all files matching our include patterns
$allFiles = @()
foreach ($ext in $includeExtensions) {
    $files = Get-ChildItem -Path $projectDir -Filter $ext -Recurse -File -ErrorAction SilentlyContinue
    $allFiles += $files
}

# Add specific files that might not match extensions
foreach ($specificFile in $includeSpecificFiles) {
    $fullPath = Join-Path $projectDir $specificFile
    if (Test-Path $fullPath) {
        $fileInfo = Get-Item $fullPath
        # Check if not already in list
        if ($allFiles.FullName -notcontains $fileInfo.FullName) {
            $allFiles += $fileInfo
        }
    }
}

Write-Host "Found $($allFiles.Count) files matching include patterns" -ForegroundColor Gray

# Filter out excluded directories
$filesToInclude = $allFiles | Where-Object {
    $file = $_
    $relativePath = $file.FullName.Substring($projectDir.Length + 1)
    
    # Check if file is in an excluded directory
    $inExcludedDir = $false
    foreach ($excludeDir in $excludeDirs) {
        if ($relativePath -match "^$excludeDir\\" -or $relativePath -match "\\$excludeDir\\") {
            $inExcludedDir = $true
            break
        }
    }
    
    # Include file if it's not excluded
    -not $inExcludedDir
}

Write-Host "After filtering: $($filesToInclude.Count) files to include" -ForegroundColor Green
Write-Host ""

# Show breakdown by file type
Write-Host "File breakdown:" -ForegroundColor Cyan
$fileGroups = $filesToInclude | Group-Object Extension | Sort-Object Name
foreach ($group in $fileGroups) {
    Write-Host "  $($group.Name): $($group.Count) files" -ForegroundColor White
}
Write-Host ""

# Create a temporary directory for organizing files
$tempDir = Join-Path $env:TEMP "AstroCast_Package_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

Write-Host "Copying files to temporary directory..." -ForegroundColor Cyan

# Copy files maintaining directory structure
$fileCount = 0
foreach ($file in $filesToInclude) {
    $relativePath = $file.FullName.Substring($projectDir.Length + 1)
    $destPath = Join-Path $tempDir $relativePath
    $destDir = Split-Path $destPath -Parent
    
    # Create destination directory if it doesn't exist
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    
    # Copy file
    Copy-Item -Path $file.FullName -Destination $destPath -Force
    $fileCount++
    
    if ($Verbose) {
        Write-Host "  Copied: $relativePath" -ForegroundColor Gray
    }
}

Write-Host "Copied $fileCount files" -ForegroundColor Green
Write-Host ""

# Create the TAR.GZ archive
Write-Host "Creating TAR.GZ archive..." -ForegroundColor Cyan

# Output to Archive folder
$archiveDir = "D:\OneDrive\Code\Archive_AstroCast\Source"
if (-not (Test-Path $archiveDir)) {
    New-Item -ItemType Directory -Path $archiveDir -Force | Out-Null
    Write-Host "  Created archive directory: $archiveDir" -ForegroundColor Gray
}
$outputFullPath = Join-Path $archiveDir $OutputPath

# Remove existing archive if it exists
if (Test-Path $outputFullPath) {
    Remove-Item $outputFullPath -Force
    Write-Host "  Removed existing archive" -ForegroundColor Gray
}

# Use tar command (available in Windows 10/11)
try {
    # Check if tar is available
    $tarPath = Get-Command tar -ErrorAction SilentlyContinue
    if ($null -eq $tarPath) {
        throw "tar command not found. Please ensure you're running Windows 10+ or have tar installed."
    }
    
    Write-Host "  Using tar command at: $($tarPath.Source)" -ForegroundColor Gray
    
    # Change to temp directory and create tar.gz
    Push-Location $tempDir
    try {
        # Create tar.gz with all files in temp directory
        $tarArgs = @(
            "-czf",
            $outputFullPath,
            "*"
        )
        
        & tar $tarArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "tar command failed with exit code $LASTEXITCODE"
        }
        
        Write-Host "  Archive created successfully" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-Host "ERROR: Failed to create TAR.GZ archive" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternative: Use 7-Zip to create .tar.gz manually" -ForegroundColor Yellow
    Write-Host "  1. Install 7-Zip from https://www.7-zip.org/" -ForegroundColor White
    Write-Host "  2. Right-click the temp folder: $tempDir" -ForegroundColor White
    Write-Host "  3. 7-Zip > Add to archive..." -ForegroundColor White
    Write-Host "  4. Set Archive format to 'tar' and check 'gzip'" -ForegroundColor White
    
    # Don't clean up temp directory if there's an error
    Write-Host ""
    Write-Host "Temp directory preserved at: $tempDir" -ForegroundColor Yellow
    Set-Location $originalLocation
    exit 1
}

# Clean up temp directory
Remove-Item $tempDir -Recurse -Force

# Restore original location
Set-Location $originalLocation

# Get file size
$archiveFile = Get-Item $outputFullPath
$sizeKB = [math]::Round($archiveFile.Length / 1KB, 2)
$sizeMB = [math]::Round($archiveFile.Length / 1MB, 2)

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "SUCCESS!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Archive created: $OutputPath" -ForegroundColor White
Write-Host "Location: $outputFullPath" -ForegroundColor Gray
Write-Host "Size: $sizeKB KB ($sizeMB MB)" -ForegroundColor White
Write-Host "Files included: $fileCount" -ForegroundColor White
if (-not [string]::IsNullOrEmpty($versionString)) {
    Write-Host "Version: $versionString" -ForegroundColor White
}
Write-Host ""

# Show file type breakdown in final summary
Write-Host "File type summary:" -ForegroundColor Cyan
foreach ($group in $fileGroups) {
    Write-Host "  $($group.Name): $($group.Count) files" -ForegroundColor White
}
Write-Host ""

# Check for WiX files
$wixCount = ($filesToInclude | Where-Object { $_.Extension -eq ".wxs" -or $_.Extension -eq ".wixproj" }).Count
if ($wixCount -gt 0) {
    Write-Host "WiX files included: $wixCount" -ForegroundColor Green
} else {
    Write-Host "Note: No WiX installer files found" -ForegroundColor Gray
}

# Check for XAML files (WPF UI)
$xamlCount = ($filesToInclude | Where-Object { $_.Extension -eq ".xaml" }).Count
if ($xamlCount -gt 0) {
    Write-Host "XAML files included: $xamlCount" -ForegroundColor Green
} else {
    Write-Host "Note: No XAML files found" -ForegroundColor Gray
}

# Check for PowerShell scripts
$ps1Count = ($filesToInclude | Where-Object { $_.Extension -eq ".ps1" }).Count
if ($ps1Count -gt 0) {
    Write-Host "PowerShell scripts included: $ps1Count" -ForegroundColor Green
}

# Check for test files
$testFiles = $filesToInclude | Where-Object { $_.FullName -match "\\Tests\\" -or $_.FullName -match "\\tests\\" }
$testCount = $testFiles.Count
if ($testCount -gt 0) {
    Write-Host "Test files included: $testCount" -ForegroundColor Green
}
Write-Host ""

Write-Host "Archive ready!" -ForegroundColor Green
Write-Host ""
