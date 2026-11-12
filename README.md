# Elevate Workforce Solution

Elevate Workforce Solution is a Nepal-focused recruitment and workforce platform that connects job seekers, employers, and administrators through a database-backed hiring workflow.

## Overview

The platform supports the complete recruitment lifecycle:

- Employers create and manage company profiles and vacancies.
- Administrators review and approve or reject vacancy submissions.
- Job seekers search, filter, save, and apply for vacancies.
- Candidates manage profiles, education, experience, skills, certifications, languages, and resumes.
- Employers review applications and update their status.
- Candidates and employers track notifications and recruitment activity.

## Features

### Job seekers

- Register, sign in, and sign out.
- Create and update a professional profile.
- Add education, experience, certifications, and languages.
- Upload and manage CV/resume files.
- Search, filter, and sort job vacancies.
- Save jobs and track applications.
- View notifications and recent activity.

### Employers

- Register and sign in.
- Create and manage a company profile.
- Create, edit, publish, and close vacancies.
- Submit vacancies for administrator review.
- Review applicants and resumes.
- Update application status throughout the recruitment pipeline.

### Administrators

- Manage users, employers, and companies.
- Review and approve or reject vacancy submissions.
- Monitor application activity and platform status.
- Review moderation activity and platform reports.

## Technology stack

- .NET 10
- ASP.NET Core MVC
- C#
- ASP.NET Core Identity
- Entity Framework Core
- PostgreSQL 16
- Razor Views
- Bootstrap 5
- JavaScript
- Docker
- xUnit

## Architecture

The solution uses a modular application architecture:

- `ElevateWorkforceSolution.Web` — MVC controllers, Razor views, and application startup.
- `ElevateWorkforceSolution.Application` — application services, DTOs, and business interfaces.
- `ElevateWorkforceSolution.Domain` — entities, enums, roles, and domain rules.
- `ElevateWorkforceSolution.Infrastructure` — EF Core data access and storage implementations.
- `ElevateWorkforceSolution.UnitTests` — unit tests for domain and workflow rules.

The request flow is:

`Web -> Application -> Domain -> Infrastructure -> PostgreSQL`

## Project structure

```text
ElevateWorkforceSolution/
├── src/
│   ├── ElevateWorkforceSolution.Web/
│   ├── ElevateWorkforceSolution.Application/
│   ├── ElevateWorkforceSolution.Domain/
│   └── ElevateWorkforceSolution.Infrastructure/
├── tests/
│   └── ElevateWorkforceSolution.UnitTests/
├── docker/
│   └── docker-compose.yml
├── ElevateWorkforceSolution.slnx
└── README.md
```

## Prerequisites

- .NET SDK 10 or later
- Docker Desktop or a compatible Docker engine
- Docker Compose
- A terminal running from the repository root

## Local database setup

The Docker Compose configuration starts PostgreSQL 16 with the following development settings:

- Database: `elevate_workforce`
- User: `ews`
- Password: `ews_dev_password`
- Host port: `5433`
- Container port: `5432`

Start the database with:

```bash
docker compose -f docker/docker-compose.yml up -d
```

Confirm that PostgreSQL is ready:

```bash
docker compose -f docker/docker-compose.yml ps
```

The development connection string is configured in `src/ElevateWorkforceSolution.Web/appsettings.Development.json` and must match the Docker service configuration.

## Build and run

Restore and build the solution:

```bash
dotnet restore
dotnet build ElevateWorkforceSolution.slnx --no-restore
```

Start the application with the HTTP launch profile:

```bash
dotnet run --project src/ElevateWorkforceSolution.Web --launch-profile http --no-build
```

The application is available at:

- `http://localhost:5075/`

The HTTPS launch profile also exposes the application at `https://localhost:7139` and `http://localhost:5075`.

## Database initialization

When the application starts, it:

1. Applies pending EF Core migrations.
2. Creates the required role records.
3. Seeds development data when the configured seed option is enabled.
4. Expires vacancies whose application deadline has passed.

The development seed configuration is in `src/ElevateWorkforceSolution.Web/appsettings.json`. Review and replace development-only credentials before using the application outside a local environment.

To generate a new migration, use the EF Core CLI from the repository root:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/ElevateWorkforceSolution.Infrastructure \
  --startup-project src/ElevateWorkforceSolution.Web
```

The application normally applies migrations automatically during startup, so a manual migration command is only required when explicitly updating the database schema.

## Testing

Run all unit tests:

```bash
dotnet test ElevateWorkforceSolution.slnx --no-restore
```

Run tests for a specific project or test class when working on a focused area.

## Configuration notes

- Development database settings are loaded from `appsettings.Development.json`.
- File uploads are stored under the configured application data directory.
- Resume and logo upload limits are configured in `appsettings.json`.
- Authentication and role management use ASP.NET Core Identity.
- Do not expose development credentials or seed accounts in production.

## Troubleshooting

### PostgreSQL is unavailable

```bash
docker compose -f docker/docker-compose.yml up -d
```

Then verify the service and confirm that port `5433` is available.

### The application cannot bind to port 5075

Stop the stale process using the local port, then restart the application:

```bash
lsof -nP -iTCP:5075 -sTCP:LISTEN
kill <PID>
```

Avoid running multiple application instances on the same launch profile port.

### Database migration errors

Confirm that the configured database connection string is correct and that the PostgreSQL container is healthy. The application will report migration failures during startup.
