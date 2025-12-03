<#
.SYNOPSIS
    Builds a distributable ZIP package for a Parley plugin.

.DESCRIPTION
    Creates a self-contained ZIP file that users can extract directly to ~/Parley/
    to install the plugin. Includes the plugin files and the parley_plugin client library.

.PARAMETER PluginPath
    Path to the plugin directory (must contain plugin.json)

.PARAMETER OutputPath
    Path where the ZIP file will be created (default: current directory)

.PARAMETER IncludeLibrary
    Include the parley_plugin client library in the ZIP (default: true)

.EXAMPLE
    .\build-plugin-zip.ps1 -PluginPath ".\Parley\Plugins\Official\flowchart-view"

.EXAMPLE
    .\build-plugin-zip.ps1 -PluginPath ".\Parley\Plugins\Official\flowchart-view" -OutputPath ".\dist"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$PluginPath,

    [Parameter(Mandatory=$false)]
    [string]$OutputPath = ".",

    [Parameter(Mandatory=$false)]
    [bool]$IncludeLibrary = $true
)

$ErrorActionPreference = "Stop"

# Resolve paths
$PluginPath = Resolve-Path $PluginPath
$OutputPath = Resolve-Path $OutputPath -ErrorAction SilentlyContinue
if (-not $OutputPath) {
    $OutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
}

# Validate plugin.json exists
$pluginJsonPath = Join-Path $PluginPath "plugin.json"
if (-not (Test-Path $pluginJsonPath)) {
    Write-Error "plugin.json not found in $PluginPath"
    exit 1
}

# Read plugin metadata
$pluginJson = Get-Content $pluginJsonPath -Raw | ConvertFrom-Json
$pluginId = $pluginJson.plugin.id
$pluginName = $pluginJson.plugin.name
$pluginVersion = $pluginJson.plugin.version

# Derive folder name from plugin path
$pluginFolderName = Split-Path $PluginPath -Leaf

# Create ZIP filename
$zipFileName = "$pluginFolderName-$pluginVersion.zip"
$zipFilePath = Join-Path $OutputPath $zipFileName

Write-Host "Building plugin package:" -ForegroundColor Cyan
Write-Host "  Name: $pluginName"
Write-Host "  Version: $pluginVersion"
Write-Host "  ID: $pluginId"
Write-Host "  Output: $zipFilePath"
Write-Host ""

# Create temp staging directory
$stagingDir = Join-Path $env:TEMP "parley-plugin-staging-$(Get-Random)"
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

try {
    # Create directory structure
    $pluginsDir = Join-Path $stagingDir "Plugins"
    $targetPluginDir = Join-Path $pluginsDir $pluginFolderName
    New-Item -ItemType Directory -Path $targetPluginDir -Force | Out-Null

    # Copy plugin files (exclude __pycache__)
    Write-Host "Copying plugin files..." -ForegroundColor Yellow
    Get-ChildItem -Path $PluginPath -Recurse |
        Where-Object { $_.FullName -notmatch '__pycache__' } |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($PluginPath.Length).TrimStart('\', '/')
            $targetPath = Join-Path $targetPluginDir $relativePath

            if ($_.PSIsContainer) {
                New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
            } else {
                $targetDir = Split-Path $targetPath -Parent
                if (-not (Test-Path $targetDir)) {
                    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
                }
                Copy-Item $_.FullName $targetPath
            }
        }

    # Copy parley_plugin library if requested
    if ($IncludeLibrary) {
        Write-Host "Copying parley_plugin library..." -ForegroundColor Yellow

        # Find parley_plugin relative to script location
        $scriptDir = Split-Path $PSScriptRoot -Parent
        $parleyPluginSource = Join-Path $scriptDir "Python\parley_plugin"

        if (-not (Test-Path $parleyPluginSource)) {
            Write-Warning "parley_plugin not found at $parleyPluginSource - skipping library"
        } else {
            $pythonDir = Join-Path $stagingDir "Python"
            $parleyPluginTarget = Join-Path $pythonDir "parley_plugin"
            New-Item -ItemType Directory -Path $parleyPluginTarget -Force | Out-Null

            # Copy library files (exclude __pycache__, .md files)
            Get-ChildItem -Path $parleyPluginSource -Recurse |
                Where-Object {
                    $_.FullName -notmatch '__pycache__' -and
                    $_.Extension -ne '.md'
                } |
                ForEach-Object {
                    $relativePath = $_.FullName.Substring($parleyPluginSource.Length).TrimStart('\', '/')
                    $targetPath = Join-Path $parleyPluginTarget $relativePath

                    if ($_.PSIsContainer) {
                        New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
                    } else {
                        Copy-Item $_.FullName $targetPath
                    }
                }
        }
    }

    # Create README.txt
    Write-Host "Creating README.txt..." -ForegroundColor Yellow
    $readmeContent = @"
$pluginName v$pluginVersion
Plugin ID: $pluginId
========================================

INSTALLATION
------------
Extract this ZIP to your Parley data folder:

  Windows:  %USERPROFILE%\Parley\
  Linux:    ~/Parley/
  macOS:    ~/Parley/

After extraction, your folder structure should look like:

  ~/Parley/
    Plugins/
      $pluginFolderName/
        plugin.json
        ...
    Python/
      parley_plugin/
        ...

REQUIREMENTS
------------
- Parley $($pluginJson.plugin.parleyVersion)
- Python 3.8+

After installing, restart Parley to load the plugin.

For more information, visit: https://github.com/LordOfMyatar/Radoub
"@
    $readmeContent | Out-File (Join-Path $stagingDir "README.txt") -Encoding UTF8

    # Create ZIP
    Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
    if (Test-Path $zipFilePath) {
        Remove-Item $zipFilePath -Force
    }

    Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipFilePath -CompressionLevel Optimal

    Write-Host ""
    Write-Host "SUCCESS: Created $zipFilePath" -ForegroundColor Green

    # Show contents
    Write-Host ""
    Write-Host "ZIP Contents:" -ForegroundColor Cyan

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipFilePath)
    try {
        $zip.Entries | ForEach-Object { Write-Host "  $($_.FullName)" }
    } finally {
        $zip.Dispose()
    }

} finally {
    # Cleanup staging directory
    if (Test-Path $stagingDir) {
        Remove-Item $stagingDir -Recurse -Force
    }
}
