# Deploy Parley plugins for development testing
# Run from Parley/Scripts: .\deploy-plugins.ps1
#
# This script:
# 1. Regenerates Python gRPC stubs from plugin.proto
# 2. Copies Official plugins to build output directory
# 3. Copies Python SDK to build output directory
# 4. Creates Community plugins folder structure for user plugins
#
# After running, start Parley from bin/Debug or bin/Release to test plugins.

$ErrorActionPreference = "Stop"

# Source paths - Scripts is inside Parley/
$ParleyRoot = Split-Path -Parent $PSScriptRoot
$PluginSource = Join-Path $ParleyRoot "Parley\Plugins\Official"
$PythonSource = Join-Path $ParleyRoot "Python\parley_plugin"
$ProtoSource = Join-Path $ParleyRoot "Parley\Plugins\Protos\plugin.proto"

# Build output paths (where Parley.exe runs from during development)
$DebugOutput = Join-Path $ParleyRoot "Parley\bin\Debug\net9.0"
$ReleaseOutput = Join-Path $ParleyRoot "Parley\bin\Release\net9.0"

# User data path (for Community plugins)
$ParleyData = Join-Path $env:USERPROFILE "Parley"
$CommunityPluginTarget = Join-Path $ParleyData "Plugins\Community"

Write-Host "=== Parley Plugin Deployment ===" -ForegroundColor Cyan
Write-Host ""

# Create Community plugins directory for user plugins
if (-not (Test-Path $CommunityPluginTarget)) {
    Write-Host "Creating Community plugins folder: $CommunityPluginTarget"
    New-Item -ItemType Directory -Path $CommunityPluginTarget -Force | Out-Null
}

# Regenerate Python gRPC stubs from proto file
Write-Host ""
Write-Host "Regenerating Python gRPC stubs..." -ForegroundColor Yellow
$ProtoDir = Join-Path $ParleyRoot "Parley\Plugins\Protos"
$PythonOutputDir = $PythonSource

Push-Location $PythonOutputDir
try {
    python -m grpc_tools.protoc `
        "-I$ProtoDir" `
        --python_out=. `
        --grpc_python_out=. `
        "$ProtoSource" 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Proto stubs generated successfully" -ForegroundColor Green

        # Fix the import in plugin_pb2_grpc.py (relative import required for package)
        $GrpcFile = Join-Path $PythonOutputDir "plugin_pb2_grpc.py"
        if (Test-Path $GrpcFile) {
            $content = Get-Content $GrpcFile -Raw
            $content = $content -replace "import plugin_pb2 as plugin__pb2", "from . import plugin_pb2 as plugin__pb2"
            Set-Content -Path $GrpcFile -Value $content -NoNewline
            Write-Host "  Fixed relative import in plugin_pb2_grpc.py" -ForegroundColor Green
        }
    }
    else {
        Write-Host "  Failed to generate proto stubs (is grpcio-tools installed?)" -ForegroundColor Red
        Write-Host "  Run: pip install grpcio-tools" -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}

# Copy to build output directories (both Debug and Release)
foreach ($OutputDir in @($DebugOutput, $ReleaseOutput)) {
    if (-not (Test-Path $OutputDir)) {
        Write-Host "Skipping $OutputDir (not built yet)" -ForegroundColor Gray
        continue
    }

    $ConfigName = if ($OutputDir -like "*Debug*") { "Debug" } else { "Release" }
    Write-Host ""
    Write-Host "Deploying to $ConfigName build..." -ForegroundColor Yellow

    # Copy Official plugins to Plugins/Official/
    $PluginTarget = Join-Path $OutputDir "Plugins\Official"
    if (-not (Test-Path $PluginTarget)) {
        New-Item -ItemType Directory -Path $PluginTarget -Force | Out-Null
    }
    if (Test-Path $PluginSource) {
        Copy-Item -Path "$PluginSource\*" -Destination $PluginTarget -Recurse -Force
        Write-Host "  Plugins -> $PluginTarget" -ForegroundColor Green
    }
    else {
        Write-Host "  Plugin source not found: $PluginSource" -ForegroundColor Red
    }

    # Copy Python SDK to Python/parley_plugin/
    $PythonTarget = Join-Path $OutputDir "Python\parley_plugin"
    if (-not (Test-Path $PythonTarget)) {
        New-Item -ItemType Directory -Path $PythonTarget -Force | Out-Null
    }
    if (Test-Path $PythonSource) {
        Copy-Item -Path "$PythonSource\*" -Destination $PythonTarget -Recurse -Force
        Write-Host "  Python SDK -> $PythonTarget" -ForegroundColor Green
    }
    else {
        Write-Host "  Python SDK source not found: $PythonSource" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Build outputs updated with latest plugins and SDK."
Write-Host "Community plugins go in: $CommunityPluginTarget"
Write-Host ""
Write-Host "Run Parley from bin/Debug or bin/Release to test."
