# WanderMeet API

Backend for a dating / nomadic-meetup app. .NET 10 · FastEndpoints (REPR) · vertical slice architecture · PostgreSQL + PostGIS · Azure Blob Storage.

## Repository layout

```
src/
├── WanderMeet.Api/              REPR endpoints, features, DbContext, migrations
├── WanderMeet.Shared/           Enums, error codes, validation constants (frontend-shareable)
└── WanderMeet.Infrastructure/   External integrations (Azure Blob today; FCM, Stripe Identity, etc. later)
tests/
├── WanderMeet.Api.IntegrationTests/  xUnit v3 + Testcontainers (Postgres + Azurite) + Respawn
└── WanderMeet.Api.UnitTests/         xUnit v3 + FastEndpoints.Testing + FluentValidation.TestHelper
docs/
├── roadmap.md                    Original product brief (treat as background, not literal spec)
└── specs/in-progress/            UC specs + per-UC work items used by the agent pipeline
.claude/                          Skills, agents, schemas, hooks for the UC → design → implement → review pipeline
```

## Prerequisites

- **.NET 10 SDK** (10.0.106 or newer)
- **Docker Desktop** for the local Postgres + Azurite stack (Testcontainers for tests reuses the same images)
- **`dotnet ef`** local tool (already pinned in `.config/dotnet-tools.json`); restore with `dotnet tool restore`

## First-time setup

```bash
# 1. Restore the local dotnet-ef tool
dotnet tool restore

# 2. Start the local stack (Postgres 17 + PostGIS, Azurite blob emulator)
docker compose up -d

# 3. Apply EF migrations to the local Postgres
dotnet ef database update --project src/WanderMeet.Api --startup-project src/WanderMeet.Api

# 4. Build and test
dotnet build
dotnet test
```

## Run the API locally

```bash
dotnet run --project src/WanderMeet.Api
```

The API listens on `http://localhost:5265` by default (per `Properties/launchSettings.json`). Swagger UI is available at `http://localhost:5265/swagger` in Development.

## Configuration

The API reads from `appsettings.json` + `appsettings.Development.json` (gitignored) + environment variables. Nothing else is required for the API to *start*; production features (B2C JWT validation, blob SAS issuance) need the values below filled in.

> **Never commit secrets.** `.gitignore` blocks `appsettings.*.json` and `.env*`. Use `dotnet user-secrets` or environment variables in development.

```jsonc
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },

  "AllowedHosts": "*",

  // PostgreSQL connection. Local: docker-compose Postgres on :5432.
  // Production: Azure Database for PostgreSQL Flexible Server.
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wandermeet;Username=wandermeet;Password=wandermeet"
  },

  // Strict allow-list — no wildcards.
  "Cors": {
    "AllowedOrigins": ["http://localhost:8080", "http://localhost:3000"]
  },

  // Azure AD B2C identity. All four keys required for JWT validation in production.
  // Empty values disable issuer validation (development convenience).
  "AzureAdB2C": {
    "Instance": "https://your-tenant.b2clogin.com",
    "TenantId": "your-tenant.onmicrosoft.com",
    "PolicyId": "B2C_1_signupsignin",
    "ClientId": "00000000-0000-0000-0000-000000000000"
  },

  // Local: Azurite blob emulator (the well-known dev account key below is public).
  // Production: real Azure Storage connection string from Key Vault.
  // SAS URLs are scoped to {userId}/photos/{photoId}.jpg.
  "BlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEEdGpoLsqAcL8d3XBrhrXqUEpb31gBsrZvL0NqV8BX8e+kZGsIuEEtuhKOJ8Q==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;",
    "ContainerName": "user-photos"
  }
}
```

## Common commands

```bash
# Build (CI mode — warnings as errors)
dotnet build -warnaserror

# Run the full test suite
dotnet test

# Run a specific test class
dotnet test --filter "FullyQualifiedName~RegisterEndpointTests"

# Run one test method
dotnet test --filter "Validate_FirstNameEmpty_FailsWithValidationFirstNameRequired"

# Add a migration
dotnet ef migrations add <MigrationName> \
  --project src/WanderMeet.Api \
  --startup-project src/WanderMeet.Api

# Roll back the last unapplied migration
dotnet ef migrations remove --project src/WanderMeet.Api

# Apply migrations to the local Postgres
dotnet ef database update --project src/WanderMeet.Api --startup-project src/WanderMeet.Api

# NuGet vulnerability scan
dotnet list WanderMeet.slnx package --vulnerable --include-transitive
```

## Architecture conventions

Every feature is a self-contained vertical slice under `src/WanderMeet.Api/Features/{Area}/`. Endpoint, request, response, and validator co-locate. `IFeatureConfiguration` implementations are auto-discovered at startup via reflection — drop a `{Area}FeatureConfiguration.cs` into a new feature folder and the slice appears in Swagger.

- **Endpoint**: `internal sealed`, inherits `Endpoint<TRequest, TResponse>`, `DontCatchExceptions()` mandatory in `Configure()`.
- **Validation**: FastEndpoints `Validator<TRequest>` (NOT `AbstractValidator<T>`).
- **Errors**: expected failures via `Send.XAsync(ct)` + `return;`. Unexpected failures propagate to the global exception handler.
- **EF Core**: Guid PKs, `AsNoTracking()` on reads, `.Select()` projections, snake_case column naming, enums stored as strings.
- **Spatial**: `geography (Point, 4326)` for all locations; `EF.Functions.IsWithinDistance` for proximity queries — never compute distance in C#.

Full conventions live in `.claude/rules/` (architecture, validation, ef-core, error-handling, csharp-style, naming, api-design). Project-specific traps discovered during implementation are logged at the bottom of `CLAUDE.md` (gitignored — local working notes).

## Test layout

- **Integration tests** spin up real Postgres + Azurite via Testcontainers, apply migrations once per fixture, and reset state between tests via Respawn. They cover the happy path (and a few edge cases) per endpoint.
- **Unit tests** cover validators (via `TestValidate`) and endpoint failure branches that don't need the database.

The two test projects share a fixture under `tests/WanderMeet.Api.IntegrationTests/Infrastructure/` exposing `App.CreateAuthenticatedClient(sub)`, `App.FakeTimeProvider`, `App.BlobConnectionString`, and `App.Services` for direct EF / blob assertions.

## Phase status

The codebase implements **Phase 2 of the WanderMeet roadmap**:

- ✅ Database schema (11 entities, PostGIS geography, partial unique indexes for soft-deletes)
- ✅ Auth (Azure AD B2C JWT + register + token-refresh proxy)
- ✅ Users (profile, public profile, travel history, photos)
- ✅ Discovery (nearby-user feed with cursor pagination + arriving-soon list)
- ✅ Cities (search + detail with active-nomad count)
- ✅ Places (suggest + list + detail; Google Places sync deferred)
- ⏳ Phase 3: Invites, Meetups, Reviews, Reports, SignalR realtime, FCM push

## Pipeline

This repo uses an in-house UC → design → implement → review pipeline driven by Claude Code subagents (designer, developer, design-reviewer, impl-reviewer) and orchestrated by the `/conductor` skill. UC specs land under `docs/specs/in-progress/` as JSON-in-Markdown matching `.claude/schemas/spec.v1.json`. See `.claude/skills/conductor/SKILL.md` for details.
