# Task Management System (ASP.NET Core Web API, .NET 8)

Clean Architecture solution with:
- ASP.NET Core Web API (.NET 8)
- EF Core + MySQL (Pomelo)
- JWT Authentication + role-based authorization (ADMIN/AGENT)
- Swagger (JWT auth support)
- AutoMapper
- FluentValidation
- Serilog (console + rolling file)
- Excel import/export (EPPlus)

## Folder structure

```
src/
  TaskManagement.Api/
  TaskManagement.Application/
  TaskManagement.Domain/
  TaskManagement.Infrastructure/
```

## Prerequisites

- .NET 8 SDK
- MySQL 8+

## Configure

Edit `src/TaskManagement.Api/appsettings.json`:
- `ConnectionStrings:Default`
- `Jwt:*`

## Run migrations

From `src/TaskManagement.Api`:

```bash
dotnet tool install --global dotnet-ef
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet ef migrations add InitialCreate -p ../TaskManagement.Infrastructure -s .
dotnet ef database update -p ../TaskManagement.Infrastructure -s .
```

## Seed users

The app seeds:
- ADMIN: `admin@test.com` / `Admin@123`
- AGENT: `agent@test.com` / `Agent@123`

Change seed passwords in `TaskManagement.Infrastructure/Persistence/AppDbContextSeed.cs`.

