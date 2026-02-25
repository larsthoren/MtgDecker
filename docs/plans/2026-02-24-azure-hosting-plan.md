# Azure Hosting Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Prepare the MtgDecker app for Azure Container Apps deployment with secrets management, Docker containerization, and CI/CD.

**Architecture:** Add Dockerfile for containerized builds, move connection string out of checked-in config into environment variables, enable auto-migration in Production, and add a GitHub Actions workflow for automated deployment.

**Tech Stack:** Docker, .NET 10, GitHub Actions, Azure Container Apps, Azure SQL

---

### Task 1: Remove connection string from appsettings.json

The connection string is currently checked into `appsettings.json`. Move it to `appsettings.Development.json` (which we'll gitignore) and add an example file.

**Files:**
- Modify: `src/MtgDecker.Web/appsettings.json`
- Modify: `src/MtgDecker.Web/appsettings.Development.json`
- Create: `src/MtgDecker.Web/appsettings.Development.json.example`
- Modify: `.gitignore`

**Step 1: Remove ConnectionStrings from appsettings.json**

Replace the entire contents of `src/MtgDecker.Web/appsettings.json` with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Step 2: Add connection string to appsettings.Development.json**

Replace the entire contents of `src/MtgDecker.Web/appsettings.Development.json` with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MtgDecker;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

**Step 3: Create appsettings.Development.json.example**

Create `src/MtgDecker.Web/appsettings.Development.json.example`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MtgDecker;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

**Step 4: Add appsettings.Development.json to .gitignore**

Add this line at the end of `.gitignore`:

```
# Development settings (contains connection strings)
**/appsettings.Development.json
```

**Step 5: Remove appsettings.Development.json from git tracking**

Since `appsettings.Development.json` is already tracked by git, adding it to `.gitignore` won't stop tracking it. You must also untrack it:

```bash
git rm --cached src/MtgDecker.Web/appsettings.Development.json
```

This removes it from the index without deleting the local file.

**Step 6: Verify build**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/
```

Expected: Build succeeded. The app reads `DefaultConnection` from the configuration hierarchy — `appsettings.Development.json` is automatically loaded in Development environment.

**Step 7: Commit**

```bash
git add .gitignore src/MtgDecker.Web/appsettings.json src/MtgDecker.Web/appsettings.Development.json.example
git commit -m "chore: remove connection string from checked-in config

Move connection string to appsettings.Development.json (gitignored).
Add appsettings.Development.json.example for new developer setup.
Production uses ConnectionStrings__DefaultConnection env var."
```

---

### Task 2: Enable auto-migration in Production

Currently `Program.cs` only auto-migrates in Development. For a friend-games app, auto-migration on startup is fine in Production too. Also enable deck seeding in Production.

**Files:**
- Modify: `src/MtgDecker.Web/Program.cs`

**Step 1: Change migration to run in all environments**

In `src/MtgDecker.Web/Program.cs`, find this block (lines 35-41):

```csharp
// Auto-migrate database on startup (dev only; use explicit migrations in production)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MtgDeckerDbContext>();
    db.Database.Migrate();
}
```

Replace with:

```csharp
// Auto-migrate database on startup
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MtgDeckerDbContext>();
    db.Database.Migrate();
}
```

**Step 2: Enable deck seeding in all environments**

Find this block (lines 43-56):

```csharp
// Seed preset decks for game testing
if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var mediator = seedScope.ServiceProvider.GetRequiredService<IMediator>();
    var seedResult = await mediator.Send(new SeedPresetDecksCommand());

    foreach (var name in seedResult.Created)
        Console.WriteLine($"[Seed] {name} deck created.");
    foreach (var name in seedResult.Skipped)
        Console.WriteLine($"[Seed] {name} — already exists, skipped.");
    foreach (var (name, cards) in seedResult.Unresolved)
        Console.WriteLine($"[Seed] {name} — unresolved: {string.Join(", ", cards)}");
}
```

Replace with:

```csharp
// Seed preset decks for game testing
{
    using var seedScope = app.Services.CreateScope();
    var mediator = seedScope.ServiceProvider.GetRequiredService<IMediator>();
    var seedResult = await mediator.Send(new SeedPresetDecksCommand());

    foreach (var name in seedResult.Created)
        Console.WriteLine($"[Seed] {name} deck created.");
    foreach (var name in seedResult.Skipped)
        Console.WriteLine($"[Seed] {name} — already exists, skipped.");
    foreach (var (name, cards) in seedResult.Unresolved)
        Console.WriteLine($"[Seed] {name} — unresolved: {string.Join(", ", cards)}");
}
```

**Step 3: Verify build**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/
```

**Step 4: Commit**

```bash
git add src/MtgDecker.Web/Program.cs
git commit -m "feat: enable auto-migration and deck seeding in all environments"
```

---

### Task 3: Create Dockerfile

Multi-stage Dockerfile at the repository root. Build stage uses .NET 10 SDK, runtime stage uses ASP.NET 10 runtime.

**Files:**
- Create: `Dockerfile`
- Create: `.dockerignore`

**Step 1: Create Dockerfile**

Create `Dockerfile` at the repository root:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files and restore
COPY src/MtgDecker.Domain/MtgDecker.Domain.csproj src/MtgDecker.Domain/
COPY src/MtgDecker.Application/MtgDecker.Application.csproj src/MtgDecker.Application/
COPY src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj src/MtgDecker.Infrastructure/
COPY src/MtgDecker.Engine/MtgDecker.Engine.csproj src/MtgDecker.Engine/
COPY src/MtgDecker.Web/MtgDecker.Web.csproj src/MtgDecker.Web/
RUN dotnet restore src/MtgDecker.Web/MtgDecker.Web.csproj

# Copy everything and publish
COPY src/ src/
RUN dotnet publish src/MtgDecker.Web/MtgDecker.Web.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MtgDecker.Web.dll"]
```

**Step 2: Create .dockerignore**

Create `.dockerignore` at the repository root:

```
**/.git
**/.vs
**/.vscode
**/.idea
**/bin
**/obj
**/node_modules
**/.worktrees
**/TestResults
**/tests
**/docs
**/*.md
**/appsettings.Development.json
.gitignore
.dockerignore
```

**Step 3: Verify build**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/
```

(We won't run `docker build` here since Docker may not be installed, but the Dockerfile follows standard .NET multi-stage patterns.)

**Step 4: Commit**

```bash
git add Dockerfile .dockerignore
git commit -m "feat: add Dockerfile for containerized deployment"
```

---

### Task 4: Create GitHub Actions deploy workflow

CI/CD pipeline that builds the Docker image, pushes to GitHub Container Registry, and deploys to Azure Container Apps.

**Files:**
- Create: `.github/workflows/deploy.yml`

**Step 1: Create the workflow directory and file**

Create `.github/workflows/deploy.yml`:

```yaml
name: Build and Deploy

on:
  push:
    branches: [main]
  workflow_dispatch:

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Log in to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push Docker image
        uses: docker/build-push-action@v6
        with:
          context: .
          push: true
          tags: |
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:latest
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}

      - name: Deploy to Azure Container Apps
        uses: azure/container-apps-deploy-action@v2
        with:
          registryUrl: ${{ env.REGISTRY }}
          imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}
          containerAppName: ${{ vars.AZURE_CONTAINER_APP_NAME }}
          resourceGroup: ${{ vars.AZURE_RESOURCE_GROUP }}
        env:
          AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
          AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
          AZURE_SUBSCRIPTION_ID: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

**Note:** The Azure deploy step requires these GitHub secrets/variables to be configured later (when you set up Azure):
- Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- Variables: `AZURE_CONTAINER_APP_NAME`, `AZURE_RESOURCE_GROUP`

The build+push step will work immediately (uses `GITHUB_TOKEN` for ghcr.io). The deploy step will fail gracefully until Azure is configured.

**Step 2: Commit**

```bash
mkdir -p .github/workflows
git add .github/workflows/deploy.yml
git commit -m "feat: add GitHub Actions CI/CD for Azure Container Apps deployment"
```

---

### Task 5: Verify everything builds and commit the design doc

Final verification that all changes work together.

**Step 1: Full build**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/
```

**Step 2: Verify git status is clean**

```bash
git status
```

Everything should be committed. The only untracked file should be `appsettings.Development.json` (now gitignored) and any plan docs.

**Step 3: Commit design doc**

```bash
git add docs/plans/2026-02-24-azure-hosting-design.md docs/plans/2026-02-24-azure-hosting-plan.md
git commit -m "docs: add Azure hosting design and implementation plan"
```
