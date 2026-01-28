#!/usr/bin/env pwsh
# Post-deploy hook to configure custom domains on Container Apps
# Custom domains get removed when container apps are updated, so we re-bind them after each deploy

$resourceGroup = "rg-$env:AZURE_ENV_NAME"

# Frontend app custom domain
if ($env:BREAKPOINT_CUSTOM_DOMAIN -and $env:BREAKPOINT_CERTIFICATE_NAME) {
    Write-Host "Binding custom domain '$env:BREAKPOINT_CUSTOM_DOMAIN' to frontend app..."
    az containerapp hostname bind `
        --hostname $env:BREAKPOINT_CUSTOM_DOMAIN `
        --resource-group $resourceGroup `
        --name "breakpoint" `
        --certificate $env:BREAKPOINT_CERTIFICATE_NAME `
        --validation-method CNAME
}

# API custom domain
if ($env:API_CUSTOM_DOMAIN -and $env:API_CERTIFICATE_NAME) {
    Write-Host "Binding custom domain '$env:API_CUSTOM_DOMAIN' to API..."
    az containerapp hostname bind `
        --hostname $env:API_CUSTOM_DOMAIN `
        --resource-group $resourceGroup `
        --name "breakpointapi" `
        --certificate $env:API_CERTIFICATE_NAME `
        --validation-method CNAME
}

Write-Host "Custom domain configuration complete."
