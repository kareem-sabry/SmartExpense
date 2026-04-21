# SmartExpense API

[![CI](https://github.com/kareem-sabry/SmartExpense/actions/workflows/ci.yml/badge.svg)](https://github.com/kareem-sabry/SmartExpense/actions/workflows/ci.yml)
[![CD](https://github.com/kareem-sabry/SmartExpense/actions/workflows/cd.yml/badge.svg)](https://github.com/kareem-sabry/SmartExpense/actions/workflows/cd.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A production-grade personal finance REST API built with ASP.NET Core 9 and Clean Architecture. Designed as a portfolio project that demonstrates real backend engineering: proper layering, security, testing, background processing, and features driven by genuine business logic — not just CRUD scaffolding.

---

## Features

- **JWT authentication** — register, login, refresh token rotation, account lockout, and password reset via email
- **Transaction management** — full CRUD with pagination, filtering, sorting, and CSV export
- **Budget tracking** — per-category monthly budgets with live spend calculation and status alerts (Under / Approaching / Exceeded)
- **Recurring transactions** — frequency-based templates (daily / weekly / monthly / yearly) with FK-based deduplication and a hosted background service for automatic generation
- **Financial analytics** — spending trends, category breakdowns, month-over-month comparisons, and budget performance summaries
- **Soft delete** — all entities are soft-deleted via a global EF Core query filter; data is never physically removed
- **Admin panel** — user management and role assignment
- **Rate limiting** — global and per-endpoint limits with IP-based partitioning
- **Security headers** — `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, and more applied via middleware
- **Audit trail** — every write automatically stamps `CreatedBy`, `UpdatedBy`, `CreatedAtUtc`, `UpdatedAtUtc` via an EF Core `SaveChangesInterceptor`
- **Health check** — `/health` returns structured JSON with per-component status

---

## Architecture
SmartExpense.Core           → Entities, domain exceptions, interfaces, enums, constants
SmartExpense.Application    → DTOs, service interfaces, repository interfaces
SmartExpense.Infrastructure → EF Core repos, service implementations, interceptors, DbContext
SmartExpense.Api            → Controllers, middleware, Program.cs
SmartExpense.Tests          → xUnit unit tests (services, repositories, controllers)

Dependencies point strictly inward: `Api → Infrastructure → Application → Core`.  
`AppDbContext` is never injected outside Infrastructure. All DB access goes through `IUnitOfWork`.

---

## CI / CD

| Pipeline | Trigger | What it does |
|---|---|---|
| **CI** (`ci.yml`) | Push to `master`, `fix/**`, `feat/**`, `refactor/**`, `docs/**`; every PR to `master` | Restore → Build (Release) → Run all unit tests → Upload `.trx` test results as artifact |
| **CD** (`cd.yml`) | Push to `master` (after CI passes) | Build Docker image → Push to GitHub Container Registry (`ghcr.io`) tagged as `latest` and the commit SHA |

The published image is available at:
ghcr.io/kareem-sabry/smartexpense:latest

---

## Getting Started

### Option A — Docker (recommended)

Pulls the pre-built image from GHCR. No SDK required.

```bash
git clone https://github.com/kareem-sabry/SmartExpense.git
cd SmartExpense
cp .env.example .env        # fill in your values — see Environment Variables below
docker compose up
```

The API will be available at `http://localhost:5000`.  
Swagger UI opens at `http://localhost:5000/swagger`.  
SQL Server data is persisted in a Docker volume across restarts.

> **Build locally instead of pulling from GHCR:**  
> `docker compose up --build`

### Option B — Local development

**Prerequisites:** .NET 9 SDK, SQL Server (or LocalDB)

1. Clone the repo:
```bash
git clone https://github.com/kareem-sabry/SmartExpense.git
cd SmartExpense
```

2. Configure secrets (never commit real values to source control):
```bash
cd SmartExpense.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-connection-string>"
dotnet user-secrets set "JwtOptions:Secret" "<min-32-char-signing-key>"
dotnet user-secrets set "JwtOptions:Issuer" "SmartExpenseApi"
dotnet user-secrets set "JwtOptions:Audience" "SmartExpenseClient"
dotnet user-secrets set "JwtOptions:ExpirationMinutes" "60"
dotnet user-secrets set "AdminUser:Email" "<admin-email>"
dotnet user-secrets set "AdminUser:Password" "<admin-password>"
dotnet user-secrets set "AdminUser:FirstName" "Admin"
dotnet user-secrets set "AdminUser:LastName" "User"
dotnet user-secrets set "EmailOptions:SmtpHost" "<smtp-host>"
dotnet user-secrets set "EmailOptions:SmtpPort" "<smtp-port>"
dotnet user-secrets set "EmailOptions:FromEmail" "<sender-address>"
```

3. Apply migrations and run:
```bash
dotnet ef database update --project SmartExpense.Infrastructure --startup-project SmartExpense.Api
dotnet run --project SmartExpense.Api
```

---

## Running Tests

```bash
dotnet test SmartExpense.sln --verbosity normal
```

Unit tests cover service-layer business logic, repository query behaviour, and controller responses.  
Uses **xUnit**, **Moq**, and **FluentAssertions**.

---

## Environment Variables

All secrets are supplied at runtime via environment variables (Docker) or `dotnet user-secrets` (local). No secrets are committed to the repository.

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `JwtOptions__Secret` | JWT signing key — minimum 32 characters |
| `JwtOptions__Issuer` | JWT issuer claim |
| `JwtOptions__Audience` | JWT audience claim |
| `JwtOptions__ExpirationMinutes` | Access token lifetime in minutes |
| `AdminUser__Email` | Seeded admin account email |
| `AdminUser__Password` | Seeded admin account password |
| `EmailOptions__SmtpHost` | SMTP host for password reset emails |
| `EmailOptions__SmtpPort` | SMTP port |
| `EmailOptions__FromEmail` | Sender address for outbound email |

See `.env.example` for a full template with placeholder values.

---

## API Overview

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/v1/auth/register` | — | Register a new user |
| `POST` | `/api/v1/auth/login` | — | Login — returns JWT + refresh token |
| `POST` | `/api/v1/auth/refresh-token` | — | Rotate refresh token |
| `POST` | `/api/v1/auth/forgot-password` | — | Trigger password reset email |
| `GET` | `/api/v1/transactions` | User | Paginated, filtered transaction list |
| `POST` | `/api/v1/transactions` | User | Create a transaction |
| `GET` | `/api/v1/transactions/export` | User | Download transactions as CSV |
| `GET` | `/api/v1/budgets` | User | List budgets |
| `GET` | `/api/v1/budgets/summary` | User | Monthly budget summary with status |
| `POST` | `/api/v1/recurring` | User | Create a recurring transaction template |
| `POST` | `/api/v1/recurring/{id}/generate` | User | Manually trigger transaction generation |
| `GET` | `/api/v1/analytics/overview` | User | Financial overview dashboard |
| `GET` | `/api/v1/analytics/trends` | User | Spending trends |
| `GET` | `/api/v1/analytics/monthly-comparison` | User | Month-over-month comparison |
| `GET` | `/api/v1/admin/users` | Admin | List all users with roles |
| `POST` | `/api/v1/admin/users/{id}/roles` | Admin | Assign a role to a user |
| `GET` | `/health` | — | Structured health status |

Full interactive documentation is available at `/swagger` when running locally.

---

## Design Highlights

**Recurring transactions — deduplication by FK, not string matching**  
Generated `Transaction` records carry a `RecurringTransactionId` foreign key. Deduplication queries on that FK, not on description text, so it is immune to content changes.

**Soft delete via global query filter**  
Entities implement `ISoftDeletable`. A global EF Core query filter excludes soft-deleted rows from every query automatically — no per-query `.Where(x => !x.IsDeleted)` callouts needed anywhere.

**Background service for recurring generation**  
`RecurringTransactionBackgroundService` runs as a hosted service, polling for due templates and generating transactions automatically without any HTTP trigger.

**IDateTimeProvider abstraction**  
`DateTime.UtcNow` is never called directly in services or middleware. All time reads go through `IDateTimeProvider`, making time-dependent logic fully testable.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Web framework | ASP.NET Core 9 |
| ORM | Entity Framework Core 9, Code-First migrations |
| Database | SQL Server |
| Auth | ASP.NET Core Identity + custom JWT |
| Testing | xUnit, Moq, FluentAssertions |
| Containerisation | Docker, Docker Compose |
| CI/CD | GitHub Actions |
| API docs | Swagger / OpenAPI (Swashbuckle) |

---

## Contact

**Kareem Sabry**

- GitHub: [@kareem-sabry](https://github.com/kareem-sabry)
- LinkedIn: [kareem-sabry](https://www.linkedin.com/in/kareem-sabry/)
- Email: kareemsabry.mail@gmail.com

---

## License

[MIT](LICENSE)