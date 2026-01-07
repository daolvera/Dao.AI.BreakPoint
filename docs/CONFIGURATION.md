# BreakPoint Configuration Guide

This document outlines all configuration needed to run BreakPoint locally and in Azure.

---

## 🔐 Secrets Overview

| Secret | Local Dev | GitHub Secrets | Azure Key Vault | Required |
|--------|-----------|----------------|-----------------|----------|
| JWT Key | appsettings.Development.json | N/A | ✅ `Jwt--Key` | Yes |
| Google OAuth ClientId | appsettings.Development.json | N/A | ✅ `Authentication--Google--ClientId` | Yes |
| Google OAuth ClientSecret | appsettings.Development.json | N/A | ✅ `Authentication--Google--ClientSecret` | Yes |
| Azure OpenAI Endpoint | appsettings.json | N/A | ✅ `AzureOpenAI--Endpoint` | Optional |
| Azure OpenAI ApiKey | appsettings.json | N/A | ✅ `AzureOpenAI--ApiKey` | Optional |
| PostgreSQL Connection | Aspire auto-configures | N/A | Aspire auto-configures | Yes |
| Blob Storage Connection | Aspire auto-configures | N/A | Aspire auto-configures | Yes |
| ACR Login Server | N/A | ✅ `ACR_LOGIN_SERVER` | N/A | For CI/CD |
| ACR Username | N/A | ✅ `ACR_USERNAME` | N/A | For CI/CD |
| ACR Password | N/A | ✅ `ACR_PASSWORD` | N/A | For CI/CD |

---

## 🖥️ Local Development Setup

### Prerequisites
- .NET 10 SDK
- Docker Desktop (for Aspire containers)
- Node.js 20+ (for Angular frontend)
- Azure CLI (optional, for cloud resources)

### Step 1: Configure Local Secrets

Create/update `appsettings.Development.json` files:

**Dao.AI.BreakPoint.ApiService/appsettings.Development.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Jwt": {
    "Key": "dev-jwt-secret-key-at-least-32-characters-long-for-development-only",
    "Issuer": "BreakPointAPI",
    "Audience": "BreakPointApp",
    "RefreshTokenExpiryDays": 30,
    "AccessTokenExpiryMinutes": 15
  },
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    }
  },
  "AzureOpenAI": {
    "Endpoint": "https://YOUR_RESOURCE.openai.azure.com/",
    "ApiKey": "YOUR_AZURE_OPENAI_KEY",
    "DeploymentName": "gpt-4",
    "MaxTokens": 1000,
    "Temperature": 0.7
  }
}
```

> **Note:** Azure OpenAI is optional. If not configured, the app falls back to static coaching tips.

### Step 2: Run with Aspire

```bash
cd Dao.AI.BreakPoint.AppHost
dotnet run
```

Aspire automatically:
- ✅ Starts PostgreSQL container (Azurite for blob storage)
- ✅ Runs database migrations
- ✅ Configures connection strings
- ✅ Starts API, Functions, and Frontend

### Step 3: Access the Application

- **Aspire Dashboard:** https://localhost:15078 (or port shown in terminal)
- **API Service:** https://localhost:7xxx (port varies)
- **Frontend:** http://localhost:3000

---

## ☁️ Azure Deployment Setup

### Step 1: GitHub Secrets (for CI/CD)

Add these secrets in your GitHub repository (Settings → Secrets and variables → Actions):

| Secret Name | Value | How to Get |
|-------------|-------|------------|
| `ACR_LOGIN_SERVER` | `yourregistry.azurecr.io` | Azure Portal → Container Registry → Overview |
| `ACR_USERNAME` | Admin username | Azure Portal → Container Registry → Access keys |
| `ACR_PASSWORD` | Admin password | Azure Portal → Container Registry → Access keys |

### Step 2: Azure Key Vault Secrets

Create these secrets in Azure Key Vault (use `--` for nested config):

```bash
# Required secrets
az keyvault secret set --vault-name YOUR_VAULT --name "Jwt--Key" --value "YOUR-PRODUCTION-JWT-KEY-MIN-32-CHARS"
az keyvault secret set --vault-name YOUR_VAULT --name "Authentication--Google--ClientId" --value "YOUR_GOOGLE_CLIENT_ID"
az keyvault secret set --vault-name YOUR_VAULT --name "Authentication--Google--ClientSecret" --value "YOUR_GOOGLE_CLIENT_SECRET"

# Optional - Azure OpenAI (for AI coaching)
az keyvault secret set --vault-name YOUR_VAULT --name "AzureOpenAI--Endpoint" --value "https://YOUR_RESOURCE.openai.azure.com/"
az keyvault secret set --vault-name YOUR_VAULT --name "AzureOpenAI--ApiKey" --value "YOUR_AZURE_OPENAI_KEY"
az keyvault secret set --vault-name YOUR_VAULT --name "AzureOpenAI--DeploymentName" --value "gpt-4"
```

### Step 3: Deploy with Azure Developer CLI

```bash
# Authenticate
azd auth login

# Provision infrastructure and deploy
azd up
```

This creates:
- Azure Container Apps Environment
- Azure Container Registry
- Azure Database for PostgreSQL Flexible Server
- Azure Storage Account (for blob storage)
- Azure Key Vault (for secrets)

---

## 🔑 Getting External Service Credentials

### Google OAuth Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Navigate to **APIs & Services → Credentials**
4. Click **Create Credentials → OAuth 2.0 Client IDs**
5. Configure:
   - Application type: **Web application**
   - Authorized redirect URIs:
     - Local: `https://localhost:7xxx/Auth/google/callback`
     - Production: `https://YOUR_APP.azurecontainerapps.io/Auth/google/callback`
6. Copy **Client ID** and **Client Secret**

### Azure OpenAI Setup (Optional)

1. Go to [Azure Portal](https://portal.azure.com/)
2. Create **Azure OpenAI** resource
3. Navigate to **Keys and Endpoint**
4. Copy **Endpoint** and **Key 1**
5. Deploy a model (e.g., gpt-4) in Azure OpenAI Studio
6. Copy the **Deployment Name**

### JWT Key Generation

Generate a secure key (minimum 32 characters):

```bash
# PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])

# Or use any password generator to create a 256-bit key
```

---

## 📁 Configuration Files Reference

| File | Purpose | Committed to Git |
|------|---------|------------------|
| `appsettings.json` | Default settings, no secrets | ✅ Yes |
| `appsettings.Development.json` | Local dev settings with secrets | ❌ No (in .gitignore) |
| `local.settings.json` | Azure Functions local config | ❌ No (in .gitignore) |
| `azure.yaml` | Azure Developer CLI config | ✅ Yes |
| `.azure/` | Azure environment config | ❌ No (in .gitignore) |

---

## 🧪 Testing Configuration

### Verify Local Setup

```bash
# 1. Run all tests
dotnet test

# 2. Start Aspire and verify services
cd Dao.AI.BreakPoint.AppHost
dotnet run

# 3. Test API health
curl https://localhost:7xxx/health
```

### Verify Azure Setup

```bash
# Check deployed services
azd show

# View logs
azd monitor --logs

# Test deployed API
curl https://YOUR_APP.azurecontainerapps.io/health
```

---

## 🚨 Troubleshooting

### "Key not valid for use in specified state" (azd)
```powershell
Remove-Item -Path "$env:USERPROFILE\.azd\cache\*" -Recurse -Force
Remove-Item -Path "$env:USERPROFILE\.azd\auth\*" -Recurse -Force
azd auth login
```

### Google OAuth redirect fails
- Ensure redirect URI matches exactly in Google Console
- For local dev, use the exact port shown in Aspire dashboard

### Database connection fails
- Aspire should auto-configure this
- Check Aspire dashboard for PostgreSQL container status

### Blob storage errors
- Local: Ensure Azurite emulator is running (Aspire handles this)
- Azure: Check storage account connection in Key Vault
