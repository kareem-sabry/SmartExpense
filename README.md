# SmartExpense API

A personal finance REST API built with .NET 9 and Clean Architecture. Tracks income and expenses, enforces per-category monthly budgets, handles recurring transactions with automated daily scheduling, and exposes six analytics endpoints for spending trends, category breakdowns, and budget performance.

[![CI](https://github.com/kareem-sabry/SmartExpense/actions/workflows/ci.yml/badge.svg)](https://github.com/kareem-sabry/SmartExpense/actions/workflows/ci.yml)
[![CD](https://github.com/kareem-sabry/SmartExpense/actions/workflows/cd.yml/badge.svg)](https://github.com/kareem-sabry/SmartExpense/actions/workflows/cd.yml)

![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![SQL Server](https://img.shields.io/badge/SQL_Server-2022-CC2927?logo=microsoftsqlserver&logoColor=white)
![License](https://img.shields.io/github/license/kareem-sabry/SmartExpense)

---

## Table of Contents

- [Features](#features)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Environment Variables](#environment-variables)
- [API Reference](#api-reference)
- [Authentication Flow](#authentication-flow)
- [Security](#security)
- [Observability](#observability)
- [CI/CD Pipeline](#cicd-pipeline)
- [Running Tests](#running-tests)

---

## Features

**Domain**
- Full transaction lifecycle — create, read, update, delete, paginate, filter by date range / type / category, export to CSV
- Per-category monthly budgets with conflict detection (one budget per category per month)
- Recurring transactions (daily / weekly / monthly / yearly) with automatic daily scheduling via a dedicated Worker service and FK-based deduplication
- Six analytics endpoints: financial overview, spending trends, category breakdown, monthly comparison, budget performance, top categories

**Auth and access**
- JWT authentication with refresh token rotation and reuse detection
- Account lockout after 5 failed login attempts (15-minute window)
- Password reset via single-use tokens (2-hour expiry) delivered by email through a background queue
- Role-based access control — `User` and `Admin` roles with separate policy gates

**Infrastructure**
- IP-partitioned rate limiting — global (100 req/min), auth (5 req/min), export (50 req/min)
- Seven HTTP security headers on every response
- EF Core `SaveChanges` interceptor that auto-stamps `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy` on every write
- Structured logging with Serilog, correlation ID propagation, and Seq sink
- RFC 7807 `ProblemDetails` error responses; stack traces only in Development
- Async email delivery via an in-process background queue (`IEmailBackgroundQueue`) so auth endpoints never block on SMTP

---

## Architecture

Four-layer Clean Architecture with inward-only dependency flow:

```
SmartExpense.Api              → Controllers, middleware, filters, DI wiring
    ↓
SmartExpense.Application      → Service interfaces, DTOs, validator contracts
    ↓
SmartExpense.Core             → Entities, enums, domain exceptions, interfaces

SmartExpense.Infrastructure   → EF Core, Identity, service and repository implementations
    references Application + Core

SmartExpense.Worker           → Quartz.NET hosted service; references Application + Infrastructure
```

Controllers delegate entirely to injected services. Business logic lives in the service layer. The `Infrastructure` layer implements the interfaces declared in `Application` — the `Api` layer never references `Infrastructure` types directly. The `Worker` is a separate deployable that shares the same `Infrastructure` and `Application` layers but has no web dependencies.

**Patterns used**

- **Repository + Unit of Work** — `IGenericRepository<T>` and `IUnitOfWork` abstract all data access; services never touch `DbContext`
- **Options pattern** — `JwtOptions`, `AdminUserOptions`, `EmailOptions` bound from configuration and validated at startup
- **EF Core Interceptor** — `AuditInterceptor` stamps audit fields before every `SaveChanges`, with no changes required in service code; in the Worker context `HttpContext` is always null so it falls back to a system user constant
- **Global action filter** — `ValidationFilter` runs FluentValidation on every request argument before the action body executes
- **Background queue** — `EmailSenderBackgroundService` drains an `IEmailBackgroundQueue` channel, decoupling email dispatch from the request path

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 9 / ASP.NET Core 9 |
| ORM | Entity Framework Core 9 |
| Database | SQL Server 2022 Express |
| Identity | ASP.NET Core Identity |
| Authentication | JWT Bearer + Refresh Tokens |
| Validation | FluentValidation |
| Logging | Serilog + Seq |
| Job scheduling | Quartz.NET 3.13 (AdoJobStore, SQL Server) |
| API versioning | Asp.Versioning |
| Documentation | Swagger / Swashbuckle |
| Testing | xUnit, Moq, FluentAssertions |
| Containerisation | Docker, Docker Compose |
| CI/CD | GitHub Actions → GHCR → Railway |

---

## Project Structure

```
SmartExpense/
├── src/
│   ├── SmartExpense.Api/
│   │   ├── Controllers/        # Auth, Transaction, Category, Budget, Analytics, RecurringTransaction, Admin
│   │   ├── Extensions/         # IServiceCollection and IApplicationBuilder extension methods
│   │   ├── Filters/            # ValidationFilter
│   │   ├── Middlewares/        # GlobalExceptionHandler, CorrelationIdMiddleware
│   │   └── Program.cs
│   │
│   ├── SmartExpense.Application/
│   │   ├── Dtos/               # Request and response DTOs per domain area
│   │   ├── Interfaces/         # Service and repository contracts
│   │   └── Validators/         # FluentValidation validators per DTO
│   │
│   ├── SmartExpense.Core/
│   │   ├── Constants/          # ApplicationConstants, ErrorMessages, SuccessMessages
│   │   ├── Entities/           # User, Transaction, Category, Budget, RecurringTransaction
│   │   ├── Enums/              # TransactionType, RecurrenceFrequency, Role, BudgetStatus
│   │   ├── Exceptions/         # NotFoundException, ConflictException, ValidationException, ForbiddenException
│   │   └── Models/             # JwtOptions, AdminUserOptions, EmailOptions, PagedResult, query params
│   │
│   ├── SmartExpense.Infrastructure/
│   │   ├── Data/               # AppDbContext, DbInitializer, UnitOfWork
│   │   ├── Interceptors/       # AuditInterceptor
│   │   ├── Migrations/         # EF Core migration history
│   │   ├── Repositories/       # Generic and domain-specific repository implementations
│   │   └── Services/           # AccountService, TransactionService, AnalyticsService, EmailService,
│   │                           # EmailSenderBackgroundService, EmailBackgroundQueue, DateTimeProvider, ...
│   │
│   └── SmartExpense.Worker/
│       ├── Extensions/         # QuartzExtensions, WorkerServicesExtensions
│       ├── Infrastructure/     # QuartzSchemaInitializer, quartz-sqlserver.sql (embedded)
│       ├── Jobs/               # RecurringTransactionGenerationJob
│       ├── Dockerfile
│       └── Program.cs
│
├── tests/
│   └── SmartExpense.Tests/
│       ├── Controllers/        # Controller unit tests
│       ├── Repositories/       # Repository unit tests with in-memory EF Core
│       └── Services/           # Service unit tests (130+ tests, 90%+ coverage)
│
├── .github/workflows/          # ci.yml, cd.yml
├── docker-compose.yml          # API + Worker + SQL Server + Seq
├── Dockerfile
├── global.json
└── .env.example
```

---

## Getting Started

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (with Compose v2)
- Git

### 1. Clone and configure

```bash
git clone https://github.com/kareem-sabry/SmartExpense.git
cd SmartExpense
cp .env.example .env
```

Fill in every value in `.env`. See [Environment Variables](#environment-variables).

### 2. Start the stack

```bash
docker compose up -d
```

Four containers start:

| Container | Purpose | Port |
|---|---|---|
| `smartexpense-api` | ASP.NET Core API | `5000` |
| `smartexpense-worker` | Quartz.NET recurring transaction scheduler | — |
| `smartexpense-db` | SQL Server 2022 Express | `1433` |
| `smartexpense-seq` | Structured log UI | `8081` |

Startup order: the database health check must pass before the API starts; the API must start before the Worker. The API runs EF Core migrations and seeds the admin user. The Worker then initialises the Quartz `QRTZ_*` tables (creating them on first run) and waits for the first scheduled trigger.

### 3. Open Swagger

[http://localhost:5000](http://localhost:5000)

### 4. View structured logs (Seq)

[http://localhost:8081](http://localhost:8081) — filterable by `CorrelationId`, `UserId`, `StatusCode`, `RequestPath`, and any other structured property. Both the API and Worker write to the same Seq instance.

### Running locally without Docker

```bash
dotnet restore
dotnet build SmartExpense.sln
cd src/SmartExpense.Api
dotnet run
```

Requires a SQL Server instance at the connection string configured in `appsettings.Development.json` or user secrets. To also run the Worker:

```bash
cd src/SmartExpense.Worker
dotnet run
```

---

## Environment Variables

Copy `.env.example` to `.env` before running. The `.env` file is gitignored.

| Variable | Description | Example |
|---|---|---|
| `DB_PASSWORD` | SQL Server SA password | `YourStrong@Passw0rd` |
| `JWT_SECRET` | HS256 signing key, minimum 32 characters | *(generate: `openssl rand -base64 48`)* |
| `ADMIN_EMAIL` | Email for the seeded admin account | `admin@smartexpense.com` |
| `ADMIN_PASSWORD` | Password for the seeded admin account | `Admin@12345` |
| `SMTP_HOST` | SMTP server hostname | `smtp.mailgun.org` |
| `SMTP_PORT` | SMTP port | `587` |
| `SMTP_USERNAME` | SMTP authentication username | |
| `SMTP_PASSWORD` | SMTP password or API key | |
| `FROM_EMAIL` | Sender address for system emails | `noreply@smartexpense.com` |
| `SEQ_USER` | Seq admin username | `admin` |
| `SEQ_PASS` | Seq admin password | |

---

## API Reference

All endpoints are under `/api/v1/`. The full interactive spec is at [http://localhost:5000](http://localhost:5000) when running locally.

### Auth — `/api/v1/auth`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/register` | Public | Create a new account |
| `POST` | `/login` | Public | Authenticate and receive JWT + refresh token |
| `POST` | `/refresh-token` | Public | Exchange a refresh token for a new JWT pair |
| `POST` | `/logout` | JWT | Invalidate the current refresh token |
| `GET` | `/me` | JWT | Get the authenticated user's profile |
| `POST` | `/forgot-password` | Public | Queue a password reset email (always returns 200) |
| `POST` | `/reset-password` | Public | Complete password reset with token |
| `DELETE` | `/delete-account` | JWT | Permanently delete the authenticated account |

### Transactions — `/api/v1/transaction`

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/` | Paginated list, filterable by date range / type / category |
| `GET` | `/{id}` | Single transaction |
| `GET` | `/recent?count=10` | Most recent N transactions |
| `GET` | `/summary` | Aggregated income, expense, net balance |
| `GET` | `/export?startDate=&endDate=` | CSV export for a date range |
| `POST` | `/` | Create a transaction |
| `PUT` | `/{id}` | Update a transaction |
| `DELETE` | `/{id}` | Delete a transaction |

### Categories — `/api/v1/category`

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/` | All categories for the authenticated user |
| `GET` | `/{id}` | Single category |
| `POST` | `/` | Create a category |
| `PUT` | `/{id}` | Update a category |
| `DELETE` | `/{id}` | Delete a category |

### Budgets — `/api/v1/budget`

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/?month=&year=` | All budgets, optionally filtered by month/year |
| `GET` | `/{id}` | Single budget |
| `GET` | `/summary?month=&year=` | Aggregated totals for a given month |
| `POST` | `/` | Create a budget (one per category per month) |
| `PUT` | `/{id}` | Update a budget |
| `DELETE` | `/{id}` | Delete a budget |

### Recurring Transactions — `/api/v1/recurringtransaction`

Recurring templates define the schedule; the Worker generates the actual transactions automatically each night. On-demand generation endpoints are available for manual triggers or backfills.

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/?isActive=` | All templates, optionally filtered by active state |
| `GET` | `/{id}` | Single template |
| `POST` | `/` | Create a recurring template |
| `PUT` | `/{id}` | Update a template |
| `DELETE` | `/{id}` | Delete a template |
| `POST` | `/{id}/toggle` | Pause or resume a template |
| `POST` | `/generate` | On-demand: generate due transactions for all active templates |
| `POST` | `/{id}/generate` | On-demand: generate due transactions for one template |

### Analytics — `/api/v1/analytics`

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/overview?startDate=&endDate=` | Financial overview for a period |
| `GET` | `/spending-trends?startDate=&endDate=&groupBy=monthly` | Income and expense trends (daily / weekly / monthly) |
| `GET` | `/category-breakdown?startDate=&endDate=&expenseOnly=true` | Spending by category with percentages |
| `GET` | `/monthly-comparison?numberOfMonths=6` | Month-over-month income and expense comparison |
| `GET` | `/budget-performance?month=&year=` | Actual spend vs budget per category |
| `GET` | `/top-categories?startDate=&endDate=&count=5` | Top spending or income categories |

### Admin — `/api/v1/admin` *(Admin role required)*

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/users` | List all users with roles |
| `GET` | `/users/{userId}` | Get a user by ID |
| `POST` | `/users/{userId}/make-admin` | Grant Admin role |
| `POST` | `/users/{userId}/remove-admin` | Revoke Admin role |
| `DELETE` | `/users/{userId}` | Delete a user account |

### Health

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/health` | JSON health report including database connectivity |

---

## Authentication Flow

```
POST /api/v1/auth/register
  → account created, role assigned

POST /api/v1/auth/login
  → { accessToken, refreshToken, expiresAt }

  Every protected request:
  Authorization: Bearer <accessToken>

POST /api/v1/auth/refresh-token  (when access token expires)
  body: { accessToken, refreshToken }
  → new { accessToken, refreshToken }

POST /api/v1/auth/logout
  → refresh token invalidated server-side
```

Refresh tokens are stored as SHA-256 hashes. The previous token hash is retained to detect reuse — if a refresh token is used twice, both are invalidated immediately.

---

## Worker — Recurring Transaction Scheduler

`SmartExpense.Worker` is a separate .NET 9 Worker Service that runs alongside the API. It uses Quartz.NET with an AdoJobStore backed by SQL Server to schedule and execute the daily recurring transaction generation sweep.

**Job**: `RecurringTransactionGenerationJob` fires at **00:05:00 UTC every day** (configurable via `Quartz:RecurringGenerationCron`). It creates a new DI scope per execution, calls `IRecurringTransactionService.GenerateAllDueAsync`, and logs a structured summary — templates processed, transactions generated, and any per-template failures — queryable in Seq by `RecurringTransactionId` or `UserId`.

**Quartz persistence**: All scheduler state (`QRTZ_*` tables) is stored in SQL Server. On first startup, `QuartzSchemaInitializer` runs the official Quartz SQL Server schema script against the shared database. Subsequent starts skip this step. If the database is temporarily unreachable, Quartz retries the connection every 15 seconds rather than crashing.

**Graceful shutdown**: `WaitForJobsToComplete = true` blocks `SIGTERM` until any running generation sweep finishes. `[DisallowConcurrentExecution]` prevents overlapping executions if a sweep runs long.

**Cron override** (per environment, no recompile needed):
```
Quartz__RecurringGenerationCron=0 5 0 * * ?   # default: 00:05:00 UTC daily
```

---

## Security

| Control | Implementation |
|---|---|
| Authentication | JWT HS256, 15-minute access token lifetime |
| Token rotation | Refresh token replaced on every use; reuse invalidates both tokens |
| Account lockout | 5 failed attempts trigger a 15-minute lockout |
| Rate limiting | Global 100 req/min; auth 5 req/min; export 50 req/min — partitioned by identity or IP |
| Password policy | Minimum 8 chars, requires uppercase, lowercase, digit, special character |
| Password reset | Single-use token, 2-hour expiry; `forgot-password` always returns 200 to avoid email enumeration |
| Email delivery | Password reset emails queued in-process via `IEmailBackgroundQueue`; SMTP dispatch is non-blocking |
| HTTP headers | `X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`, `Referrer-Policy`, `Content-Security-Policy` |
| Error responses | RFC 7807 `ProblemDetails`; stack traces only in Development |
| Data isolation | All repository queries are scoped to the authenticated user's ID |
| Audit trail | `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy` auto-stamped via EF Core interceptor |

---

## Observability

Every HTTP request produces a structured Serilog event:

```
RequestMethod   GET
RequestPath     /api/v1/transaction
StatusCode      200
Elapsed         12.3ms
CorrelationId   a3f9b2c1d4e8f012
UserId          8d3a1b2c-...
MachineName     smartexpense-api
Application     SmartExpense.Api
```

The `CorrelationId` is generated per request (16-char hex) or inherited from an incoming `X-Correlation-Id` header, and is echoed on every response. Exception events include `ExceptionType`, `UserId`, and `CorrelationId` as structured properties.

The Worker emits structured job events with `TemplatesProcessed`, `TransactionsGenerated`, `FailedTemplates`, and `ElapsedMs`. Per-template failures are logged as separate error entries keyed by `RecurringTransactionId` and `UserId` — filterable in Seq independently of the run summary.

Health-check requests are logged at `Verbose` level so they don't drown out application events.

---

## CI/CD Pipeline

**CI** runs on every push to `master`, `feat/**`, `fix/**`, `refactor/**`, `docs/**`, `test/**`, and on every pull request to `master`:

```
Checkout → Restore → Build (Release) → Run tests + collect coverage → Upload .trx + coverage artifacts
```

**CD** runs automatically after CI passes on `master`:

```
Checkout → Login to GHCR → Build & push Docker image
  Tags: ghcr.io/kareem-sabry/smartexpense:latest
        ghcr.io/kareem-sabry/smartexpense:<commit-sha>
```

Railway watches the repository and redeploys on every push to `master`, pulling the freshly built image from GHCR.

---

## Running Tests

```bash
dotnet test SmartExpense.sln -c Release --verbosity normal
```

The suite covers service, repository, and controller layers using xUnit, Moq, and FluentAssertions. Repository tests use the EF Core in-memory provider.

```
130+ unit tests   |   90%+ coverage
```
