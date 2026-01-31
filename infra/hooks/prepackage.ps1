#!/usr/bin/env pwsh
# This hook modifies the Aspire manifest to set the correct Docker build context
# for the analyzer function project

param(
    [string]$manifestPath = "$env:AZURE_DEV_OUTPUTS_PATH/aspire-manifest.json"
)

Write-Host "Prepackage hook: Adjusting Docker build context for analyzer function..."

if (Test-Path $manifestPath) {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json -AsHashtable
    
    if ($manifest.resources.breakpointanalyzerfunction.build) {
        # Change context from project folder to solution root
        $manifest.resources.breakpointanalyzerfunction.build.context = "."
        Write-Host "Updated breakpointanalyzerfunction build context to solution root"
        
        $manifest | ConvertTo-Json -Depth 100 | Set-Content $manifestPath
        Write-Host "Manifest updated successfully"
    }
    else {
        Write-Host "No build configuration found for breakpointanalyzerfunction"
    }
}
else {
    Write-Host "Manifest not found at: $manifestPath"
}
