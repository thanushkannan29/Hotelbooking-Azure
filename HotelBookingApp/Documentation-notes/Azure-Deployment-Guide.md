# Deployment Guide — Thanush StayHub

## Architecture

```
Users
  │
  ├──▶ Vercel (Angular Frontend — free, public)
  │         │ HTTPS API calls
  └──▶ Railway (Backend API — free, public, Docker)
              │
              ├── Key Vault (Azure) ──▶ reads secrets
              └── Azure SQL Database ──▶ data
```

## What's Where

| Component | Platform | URL |
|---|---|---|
| Frontend (Angular) | Vercel | `https://stayhub.vercel.app` |
| Backend (.NET 10) | Railway | `https://stayhub-api.up.railway.app` |
| Database | Azure SQL | `stayhub-sql-server.database.windows.net` |
| Secrets | Azure Key Vault | `https://kv-stayhub-prod1.vault.azure.net/` |

## CI/CD

| Trigger | Pipeline | Action |
|---|---|---|
| Push to `main` (backend files) | GitHub Actions | Build → Test → Migrate Azure SQL → Deploy Railway |
| Push to `main` (frontend files) | GitHub Actions | Build → Deploy Vercel |
| Push to `main` (backend files) | Azure DevOps | Build → Test → Migrate Azure SQL → Deploy Railway |
| Push to `main` (frontend files) | Azure DevOps | Build → Deploy Vercel |

---

## ⚠️ IMPORTANT — Rotate Your Secrets

Your secrets were shared in chat. Go to Key Vault and create new versions of:
- `Keys--Jwt` → create new version with a new 64-char random string
- `GroqApiKey` → create new version if compromised

---

## PART 1 — Azure Resources (Already Done ✅)

- ✅ SQL Server: `stayhub-sql-server.database.windows.net`
- ✅ Database: `dbHotelBookingApp`
- ✅ Key Vault: `https://kv-stayhub-prod1.vault.azure.net/`
- ✅ Key Vault secrets: `ConnectionStrings--Production`, `Keys--Jwt`, `GroqApiKey`

---

## PART 2 — Run Database Migrations (One-time)

Since Azure SQL has no public firewall access, use **Azure Cloud Shell**.

1. Click **Cloud Shell** (`>_`) in Azure Portal top bar → **Bash**
2. Run:

```bash
# Install .NET 10
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
export PATH=$PATH:$HOME/.dotnet

# Install EF tools
dotnet tool install --global dotnet-ef
export PATH=$PATH:$HOME/.dotnet/tools

# Clone your repo
git clone https://github.com/YOURUSERNAME/YOURREPO.git
cd YOURREPO/SolHotelBookingAppWebApi/HotelBookingAppWebApi

# Run migrations (uses your Entra identity — you are the SQL admin)
dotnet ef database update --connection "Server=tcp:stayhub-sql-server.database.windows.net,1433;Initial Catalog=dbHotelBookingApp;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;"
```

**Verify:**
- Azure Portal → SQL Database → **Query editor** → login with Microsoft Entra MFA
- Run: `SELECT COUNT(*) FROM Amenities` → must return **30** ✅

---

## PART 3 — Deploy Backend to Railway

### Step 1 — Create Railway account
1. Go to [railway.app](https://railway.app) → **Login with GitHub**

### Step 2 — Create new project
1. **New Project** → **Deploy from GitHub repo**
2. Select your repository
3. Railway detects the `Dockerfile` automatically → **Deploy**

### Step 3 — Add environment variables on Railway
Go to your Railway service → **Variables** tab → add each:

| Variable Name | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `KeyVaultUri` | `https://kv-stayhub-prod1.vault.azure.net/` |
| `ConnectionStrings__Production` | `Server=tcp:stayhub-sql-server.database.windows.net,1433;Initial Catalog=dbHotelBookingApp;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication="Active Directory Default";` |
| `Keys__Jwt` | your JWT key from Key Vault |
| `GroqApiKey` | your Groq key from Key Vault |
| `FrontendUrl` | `https://stayhub.vercel.app` (update after Vercel deploy) |

> Note: Railway uses `__` (double underscore) for nested config.
> `ConnectionStrings__Production` → maps to `ConnectionStrings:Production` in .NET

### Step 4 — Allow Railway to connect to Azure SQL

Railway needs its outbound IP whitelisted in Azure SQL:

1. Go to Railway service → **Settings** → copy **Static IP** (or all outbound IPs shown)
2. Go to Azure Portal → SQL Server `stayhub-sql-server` → **Networking**
3. Under **Firewall rules** → **+ Add a firewall rule**:
   - Name: `Railway`
   - Start IP / End IP: paste Railway's static IP
4. **Save**

### Step 5 — Get your Railway URL
- Railway service → **Settings** → **Domains** → copy the URL
- It looks like: `stayhub-api-production.up.railway.app`
- Test: `https://YOUR-RAILWAY-URL/api/public/hotels` → returns `[]` ✅

### Step 6 — Get Railway token for CI/CD
- Railway → **Account Settings** → **Tokens** → **Create token**
- Name: `github-actions`
- Copy the token — save it for GitHub Secrets

---

## PART 4 — Deploy Frontend to Vercel

### Step 1 — Create Vercel account
1. Go to [vercel.com](https://vercel.com) → **Login with GitHub**

### Step 2 — Import project
1. **New Project** → import your GitHub repo
2. **Root Directory** → set to `Fontend-Angular`
3. Framework preset: **Angular**
4. Build command: `npm run build -- --configuration production`
5. Output directory: `dist/hotel-booking-app/browser`
6. **Deploy**

### Step 3 — Get your Vercel URL
- Vercel gives you: `stayhub.vercel.app` (or similar)
- Go back to Railway → update `FrontendUrl` variable with this URL

### Step 4 — Get Vercel tokens for CI/CD
1. Vercel → **Account Settings** → **Tokens** → **Create token** → name: `github-actions` → copy
2. Vercel → your project → **Settings** → **General** → copy **Project ID**
3. Vercel → **Account Settings** → **General** → copy **Team ID** (this is Org ID)

---

## PART 5 — GitHub Actions CI/CD Setup

### Step 1 — Push code to GitHub
```bash
git init
git add .
git commit -m "initial commit"
git remote add origin https://github.com/YOURUSERNAME/YOURREPO.git
git push -u origin main
```

### Step 2 — Add GitHub Secrets
Go to GitHub repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**

Add each secret:

| Secret Name | Value | Where to get it |
|---|---|---|
| `RAILWAY_TOKEN` | Railway token | Railway → Account Settings → Tokens |
| `VERCEL_TOKEN` | Vercel token | Vercel → Account Settings → Tokens |
| `VERCEL_ORG_ID` | Vercel Team/Org ID | Vercel → Account Settings → General |
| `VERCEL_PROJECT_ID` | Vercel Project ID | Vercel → Project → Settings → General |
| `SQL_CONNECTION_STRING` | Azure SQL connection string | Azure SQL → Connection strings → ADO.NET Entra passwordless |

### Step 3 — Verify pipelines run
- Push any change to `main`
- Go to GitHub repo → **Actions** tab
- Backend pipeline: Build → Test → Migrate → Deploy Railway ✅
- Frontend pipeline: Build → Deploy Vercel ✅

---

## PART 6 — Azure DevOps CI/CD Setup (Alternative to GitHub Actions)

Use this if your company requires Azure DevOps instead of GitHub Actions.

### Step 1 — Push code to Azure DevOps
1. Go to [dev.azure.com](https://dev.azure.com) → sign in
2. **New project** → `StayHub` → **Private** → **Create**
3. **Repos** → copy clone URL
4. Push your code:
```bash
git remote add azure https://YOURORG@dev.azure.com/YOURORG/StayHub/_git/StayHub
git push azure main
```

### Step 2 — Create Service Connection
1. **Project Settings** → **Service connections** → **New** → **Azure Resource Manager**
2. Authentication: **Service principal (automatic)**
3. Subscription: `NafTech-DevTest` · Resource group: `NAFTech-Interns-RG`
4. Name: `azure-stayhub-connection` → **Save**

### Step 3 — Add Pipeline Variables
1. **Pipelines** → **Library** → **+ Variable group** → name: `stayhub-vars`
2. Add variables (click lock icon to make secret):

| Variable | Value |
|---|---|
| `RAILWAY_TOKEN` | Railway token |
| `VERCEL_TOKEN` | Vercel token |
| `VERCEL_ORG_ID` | Vercel Org ID |
| `VERCEL_PROJECT_ID` | Vercel Project ID |
| `SQL_CONNECTION_STRING` | Azure SQL connection string |

3. **Save**

### Step 4 — Create Backend Pipeline
1. **Pipelines** → **New pipeline** → **Azure Repos Git** → select repo
2. **Existing YAML file** → `/azure-pipelines-backend.yml` → **Continue** → **Save and run**

### Step 5 — Create Frontend Pipeline
1. **Pipelines** → **New pipeline** → same steps
2. Path: `/azure-pipelines-frontend.yml` → **Save and run**

### Step 6 — Create Environment
1. **Pipelines** → **Environments** → **New environment**
2. Name: `production` · Resource: **None** → **Create**

---

## PART 7 — Azure Static Web App (Alternative for Frontend)

Azure Static Web Apps is free and has no public access policy issues — it's designed for static sites.

### Step 1 — Create Static Web App
1. Search **Static Web Apps** → **Create**
2. Subscription: `NafTech-DevTest`
3. Resource group: `NAFTech-Interns-RG`
4. Name: `stayhub-frontend`
5. Plan: **Free**
6. Region: `West US 2` (closest to West US 3)
7. Deployment source: **GitHub**
8. Sign in with GitHub → select your repo → branch: `main`
9. Build presets: **Angular**
10. App location: `Fontend-Angular`
11. Output location: `dist/hotel-booking-app/browser`
12. **Review + Create** → **Create**

Azure automatically creates a GitHub Actions workflow in your repo for CI/CD.

### Step 2 — Get your Static Web App URL
- After deployment: `https://stayhub-frontend.azurestaticapps.net` (or similar)
- Update Railway `FrontendUrl` variable with this URL

### Step 3 — Update CORS in Railway
- Railway → Variables → update `FrontendUrl` to your Static Web App URL

---

## PART 8 — Final Verification

**Backend (Railway):**
```
https://YOUR-RAILWAY-URL/api/public/hotels → returns []
```

**Frontend (Vercel or Static Web App):**
- App loads ✅
- Register account → login works ✅
- Chatbot responds ✅

**CI/CD:**
- Push any change → pipeline triggers automatically ✅

---

## All Secrets Reference

| Secret | Where stored | Used by |
|---|---|---|
| SQL connection string | Key Vault + Railway env var | Backend |
| JWT key | Key Vault + Railway env var | Backend |
| Groq API key | Key Vault + Railway env var | Backend |
| Railway token | GitHub Secrets / DevOps vars | CI/CD |
| Vercel token | GitHub Secrets / DevOps vars | CI/CD |

---

## Key Vault Secrets (for reference)

| Secret Name | Key Vault URL |
|---|---|
| `ConnectionStrings--Production` | `https://kv-stayhub-prod1.vault.azure.net/secrets/ConnectionStrings--Production/` |
| `Keys--Jwt` | `https://kv-stayhub-prod1.vault.azure.net/secrets/Keys--Jwt/` |
| `GroqApiKey` | `https://kv-stayhub-prod1.vault.azure.net/secrets/GroqApiKey/` |

> ⚠️ Rotate `Keys--Jwt` and `GroqApiKey` — they were exposed in chat. Create new versions in Key Vault.
