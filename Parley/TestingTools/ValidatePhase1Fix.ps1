# Phase 1 Fix Validation Script
# Verifies that struct types are preserved during round-trip editing

Write-Host ""
Write-Host "Running Phase 1 validation..." -ForegroundColor Cyan
Write-Host ""

dotnet run --project TestingTools/Phase1Validator/Phase1Validator.csproj
