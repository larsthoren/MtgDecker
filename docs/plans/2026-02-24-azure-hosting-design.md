# Azure Hosting Design

## Date: 2026-02-24

## Goal

Host MtgDecker on Azure free tiers so friends can play over the internet. Zero monthly cost.

## Infrastructure

### Compute: Azure Container Apps (Free tier)

- 180K vCPU-seconds + 360K GiB-seconds/month
- Full WebSocket support (required for Blazor Server)
- Scale-to-zero when idle, ~10-15 second cold start on first request
- Auto-assigned `*.azurecontainerapps.io` URL

### Database: Azure SQL Database (Free tier)

- 32GB storage, 100K vCore seconds/month
- SQL authentication (username/password)
- Firewall: allow Azure services
- Replaces LocalDB for production

### Container Registry: GitHub Container Registry (ghcr.io)

- Free for public and private images
- No Azure Container Registry cost

## Docker Setup

Multi-stage Dockerfile:
- Build stage: .NET 10 SDK, `dotnet publish` in Release mode
- Runtime stage: ASP.NET 10 runtime image
- Port 8080 (Azure Container Apps default)
- `ASPNETCORE_ENVIRONMENT=Production`
- Auto-migrate database on startup

## CI/CD: GitHub Actions

Triggers on push to `main`:
1. Build Docker image
2. Push to `ghcr.io`
3. Deploy to Azure Container Apps via `az containerapp update`

Merging a PR = automatic deployment.

## Secrets Management

### Connection string handling

- **`appsettings.json`** (checked in): No connection string. Non-sensitive config only.
- **`appsettings.Development.json`** (gitignored): Local dev connection string for LocalDB. Each developer creates their own.
- **`appsettings.Development.json.example`** (checked in): Shows expected shape without real values.
- **Azure Container Apps**: Connection string injected as secret → environment variable `ConnectionStrings__DefaultConnection`.

### Code changes

- Remove connection string from `appsettings.json`
- Add `appsettings.Development.json` to `.gitignore`
- `Program.cs` unchanged — ASP.NET configuration hierarchy handles env var override automatically

## Azure SQL Connection String

```
Server=tcp:<server>.database.windows.net,1433;Database=MtgDecker;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;
```

Stored as Azure Container Apps secret, never in source code.

## Data Considerations

- Scryfall bulk import runs against Azure SQL on first startup or as one-time job
- Game engine state is in-memory (no DB dependency) — only deck builder features hit the database
- Free tier vCore budget is sufficient for normal friend-game usage

## Files to Create/Modify

1. **Create**: `Dockerfile` (multi-stage build)
2. **Create**: `.dockerignore`
3. **Create**: `.github/workflows/deploy.yml` (CI/CD pipeline)
4. **Create**: `src/MtgDecker.Web/appsettings.Development.json.example`
5. **Modify**: `src/MtgDecker.Web/appsettings.json` (remove connection string)
6. **Modify**: `.gitignore` (add `appsettings.Development.json`)
7. **Modify**: `src/MtgDecker.Web/Program.cs` (ensure migrations run in Production if not already)
