# Deploy Parley plugins to user's plugin folder
# Run from Parley/Scripts: .\deploy-plugins.ps1

$ErrorActionPreference = "Stop"

# Source paths - Scripts is inside Parley/
$ParleyRoot = Split-Path -Parent $PSScriptRoot
$PluginSource = Join-Path $ParleyRoot "Parley\Plugins\Official"
$PythonSource = Join-Path $ParleyRoot "Python\parley_plugin"
$ProtoSource = Join-Path $ParleyRoot "Parley\Plugins\Protos\plugin.proto"

# Target paths (AppData)
$ParleyData = Join-Path $env:USERPROFILE "Parley"
$PluginTarget = Join-Path $ParleyData "Plugins"
$PythonTarget = Join-Path $ParleyData "Python\parley_plugin"

Write-Host "=== Parley Plugin Deployment ===" -ForegroundColor Cyan
Write-Host ""

# Create directories if needed
if (-not (Test-Path $PluginTarget)) {
    Write-Host "Creating: $PluginTarget"
    New-Item -ItemType Directory -Path $PluginTarget -Force | Out-Null
}

if (-not (Test-Path $PythonTarget)) {
    Write-Host "Creating: $PythonTarget"
    New-Item -ItemType Directory -Path $PythonTarget -Force | Out-Null
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

# Copy Official plugins
Write-Host ""
Write-Host "Copying Official plugins..." -ForegroundColor Yellow
if (Test-Path $PluginSource) {
    Copy-Item -Path "$PluginSource\*" -Destination $PluginTarget -Recurse -Force
    Write-Host "  -> $PluginTarget" -ForegroundColor Green
}
else {
    Write-Host "  Source not found: $PluginSource" -ForegroundColor Red
}

# Copy Python client library
Write-Host ""
Write-Host "Copying Python client library..." -ForegroundColor Yellow
if (Test-Path $PythonSource) {
    Copy-Item -Path "$PythonSource\*" -Destination $PythonTarget -Recurse -Force
    Write-Host "  -> $PythonTarget" -ForegroundColor Green
}
else {
    Write-Host "  Source not found: $PythonSource" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Plugin folder: $PluginTarget"
Write-Host "Python folder: $PythonTarget"
Write-Host ""
Write-Host "Restart Parley to load updated plugins."
