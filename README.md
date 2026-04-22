# SmartExpense API

A production-grade personal finance REST API built with .NET 9 and Clean Architecture. Tracks income and expenses, enforces per-category monthly budgets, generates recurring transactions automatically, and surfaces analytics for spending trends, category breakdowns, and month-over-month comparisons.

[![CI](https://github.com/kareem-sabry/SmartExpense/actions/workflows/ci.yml/badge.svg)](https://github.com/kareem-sabry/SmartExpense/actions/workflows/ci.yml)
[![CD](https://github.com/kareem-sabry/SmartExpense/actions/workflows/cd.yml/badge.svg)](https://github.com/kareem-sabry/SmartExpense/actions/workflows/cd.yml)
![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
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

**Core domain**
- Full transaction lifecycle — create, read, update, delete, paginate, filter by date range / type / category, and export to CSV
- Per-category monthly budgets with conflict detection (one budget per category per month)
- Recurring transactions (daily / weekly / monthly / yearly) with automatic scheduling and FK-based deduplication
- Six analytics endpoints: financial overview, spending trends, category breakdown, monthly comparison, budget performance, and top categories

**Infrastructure**
- JWT authentication with refresh token rotation and reuse detection
- Account lockout after 5 failed attempts (15-minute window)
- Password reset flow via time-limited tokens (2-hour expiry) sent by email
- Role-based access control — `User` and `Admin` roles with separate policy gates
- IP-partitioned rate limiting — global (100 req/min), auth (5 req/min), export (50 req/min)
- Seven HTTP security headers on every response (CSP, X-Frame-Options, X-XSS-Protection, HSTS, Referrer-Policy, X-Content-Type-Options)
- EF Core save interceptor that auto-stamps `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, and `UpdatedBy` on every entity write
- Structured logging with Serilog, correlation ID propagation, and Seq sink

---

## Architecture

The solution enforces a strict **4-layer Clean Architecture** with inward-only dependency flow:

```
SmartExpense.Api              → Presentation (controllers, middleware, filters)
    ↓
SmartExpense.Application      → Use-case contracts (interfaces, DTOs)
    ↓
SmartExpense.Core             → Domain (entities, enums, exceptions, interfaces)

SmartExpense.Infrastructure   → Implementation (EF Core, Identity, services, repositories)
    ↓ references Application + Core
```

The `Infrastructure` layer implements the interfaces declared in `Application`. The `Api` layer orchestrates the pipeline but contains no business logic. Controllers delegate entirely to injected services; all domain decisions happen in the service layer.

**Design patterns applied**

- **Repository + Unit of Work** — data access is abstracted behind `IGenericRepository<T>` and `IUnitOfWork`; services never reference `DbContext` directly
- **Factory** — `User.Create(...)` enforces invariants at construction time
- **Options pattern** — `JwtOptions`, `AdminUserOptions`, and `EmailOptions` are bound from configuration and validated at startup
- **Decorator** — analytics caching wraps the real `AnalyticsService` transparently without modifying it
- **EF Core Interceptor** — `AuditInterceptor` stamps audit fields before every `SaveChanges`, requiring zero changes in service code

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 9 / ASP.NET Core 9 |
| ORM | Entity Framework Core 9 |
| Database | SQL Server 2022 |
| Identity | ASP.NET Core Identity |
| Authentication | JWT Bearer + Refresh Tokens |
| Logging | Serilog + Seq |
| API versioning | Asp.Versioning |
| Documentation | Swagger / Swashbuckle |
| Testing | xUnit, Moq, FluentAssertions |
| Containerisation | Docker, Docker Compose |
| CI/CD | GitHub Actions + GHCR |

---

## Project Structure

```
SmartExpense/
├── SmartExpense.Api/
│   ├── Controllers/          # Auth, Transaction, Category, Budget, Analytics, RecurringTransaction, Admin
│   ├── Extensions/           # IServiceCollection and IApplicationBuilder extension methods
│   ├── Middlewares/          # GlobalExceptionHandler, CorrelationIdMiddleware
│   └── Program.cs
│
├── SmartExpense.Application/
│   ├── Dtos/                 # Request and response DTOs per domain (Auth, Transaction, Budget, ...)
│   └── Interfaces/           # Service and repository contracts
│
├── SmartExpense.Core/
│   ├── Constants/            # ApplicationConstants, ErrorMessages, SuccessMessages, IdentityRoleConstants
│   ├── Entities/             # User, Transaction, Category, Budget, RecurringTransaction
│   ├── Enums/                # TransactionType, RecurrenceFrequency, Role, BudgetStatus
│   ├── Exceptions/           # NotFoundException, ConflictException, ValidationException, ForbiddenException
│   └── Interfaces/           # IAuditable, IEntity, IUserOwnedEntity
│
├── SmartExpense.Infrastructure/
│   ├── Data/                 # AppDbContext, DbInitializer, entity configurations
│   ├── Interceptors/         # AuditInterceptor
│   ├── Migrations/           # EF Core migration history
│   ├── Repositories/         # Generic + domain-specific repository implementations
│   └── Services/             # AccountService, TransactionService, AnalyticsService, ...
│
├── SmartExpense.Tests/
│   ├── Controllers/          # Controller unit tests
│   ├── Repositories/         # Repository unit tests
│   └── Services/             # Service unit tests (130+ tests, 90%+ coverage)
│
├── .github/workflows/        # ci.yml, cd.yml
├── docker-compose.yml        # API + SQL Server + Seq
├── Dockerfile
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

Open `.env` and fill in every value. See [Environment Variables](#environment-variables) for details.

### 2. Start the stack

```bash
docker compose up -d
```

This starts three containers:

| Container | Purpose | Port |
|---|---|---|
| `smartexpense-api` | ASP.NET Core API | `5000` |
| `smartexpense-db` | SQL Server 2022 | `1433` |
| `smartexpense-seq` | Structured log UI | `8081` (UI) / `5341` (ingest) |

The API waits for SQL Server's health check before starting, then applies migrations and seeds the default admin user automatically.

### 3. Open Swagger

Navigate to [http://localhost:5000](http://localhost:5000) for the interactive API documentation.

### 4. Open Seq (structured logs)

Navigate to [http://localhost:8081](http://localhost:8081) to view real-time structured log events, filterable by `CorrelationId`, `UserId`, `StatusCode`, `RequestPath`, and any other structured property.

### Running locally (without Docker)

```bash
# Requires SQL Server accessible at the connection string in user secrets / appsettings
dotnet restore
dotnet build SmartExpense.sln
cd SmartExpense.Api
dotnet run
```

---

## Environment Variables

Copy `.env.example` to `.env` and populate every field before running.

| Variable | Description | Example |
|---|---|---|
| `DB_PASSWORD` | SQL Server SA password | `YourStrong@Passw0rd` |
| `JWT_SECRET` | HS256 signing key — minimum 32 characters | *(generate with `openssl rand -base64 48`)* |
| `ADMIN_EMAIL` | Email for the seeded admin account | `admin@smartexpense.com` |
| `ADMIN_PASSWORD` | Password for the seeded admin account | `Admin@12345` |
| `SMTP_HOST` | SMTP server hostname | `smtp.mailgun.org` |
| `SMTP_PORT` | SMTP port | `587` |
| `SMTP_USERNAME` | SMTP authentication username | |
| `SMTP_PASSWORD` | SMTP authentication password / API key | |
| `FROM_EMAIL` | Sender address for system emails | `noreply@smartexpense.com` |

> **Note:** `.env` is listed in `.gitignore` and will never be committed.

---

## API Reference

All endpoints are versioned under `/api/v1/`. The full interactive specification is available at [http://localhost:5000](http://localhost:5000) when the API is running.

### Auth — `/api/v1/auth`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/register` | Public | Register a new user account |
| `POST` | `/login` | Public | Authenticate and receive JWT + refresh token |
| `POST` | `/refresh-token` | Public | Exchange refresh token for a new JWT pair |
| `POST` | `/logout` | JWT | Invalidate the current refresh token |
| `GET` | `/me` | JWT | Get the authenticated user's profile |
| `POST` | `/forgot-password` | Public | Trigger password reset email |
| `POST` | `/reset-password` | Public | Complete password reset with token |
| `DELETE` | `/delete-account` | JWT | Permanently delete the authenticated account |

### Transactions — `/api/v1/transaction`

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/` | Paginated list with date / type / category filters |
| `GET` | `/{id}` | Single transaction by ID |
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
| `GET` | `/{id}` | Single category by ID |
| `POST` | `/` | Create a category |
| `PUT` | `/{id}` | Update a category |
| `DELETE` | `/{id}` | Delete a category |

### Budgets — `/api/v1/budget`

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/?month=&year=` | All budgets, optionally filtered by month/year |
| `GET` | `/{id}` | Single budget by ID |
| `GET` | `/summary?month=&year=` | Aggregated totals for a month |
| `POST` | `/` | Create a budget (one per category per month) |
| `PUT` | `/{id}` | Update a budget |
| `DELETE` | `/{id}` | Delete a budget |

### Recurring Transactions — `/api/v1/recurringtransaction`

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/?isActive=` | All recurring templates, optionally filtered by active state |
| `GET` | `/{id}` | Single template by ID |
| `POST` | `/` | Create a recurring template |
| `PUT` | `/{id}` | Update a recurring template |
| `DELETE` | `/{id}` | Delete a recurring template |
| `POST` | `/{id}/toggle` | Pause or resume a recurring template |
| `POST` | `/generate` | Generate due transactions for all active templates |
| `POST` | `/{id}/generate` | Generate due transactions for one template |

### Analytics — `/api/v1/analytics`

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/overview?startDate=&endDate=` | Full financial overview for a period |
| `GET` | `/spending-trends?startDate=&endDate=&groupBy=monthly` | Income and expense trends (daily / weekly / monthly) |
| `GET` | `/category-breakdown?startDate=&endDate=&expenseOnly=true` | Spending breakdown by category with percentages |
| `GET` | `/monthly-comparison?numberOfMonths=6` | Month-over-month income and expense comparison |
| `GET` | `/budget-performance?month=&year=` | Actual spend vs budget per category |
| `GET` | `/top-categories?startDate=&endDate=&count=5` | Top spending or income categories |

### Admin — `/api/v1/admin` *(Admin role required)*

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/users` | List all users with roles |
| `GET` | `/users/{userId}` | Get a single user by ID |
| `POST` | `/users/{userId}/make-admin` | Grant the Admin role |
| `POST` | `/users/{userId}/remove-admin` | Revoke the Admin role |
| `DELETE` | `/users/{userId}` | Delete a user account |

### Health

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/health` | JSON health report — checks database connectivity |

---

## Authentication Flow

```
POST /api/v1/auth/register
  → account created, roles assigned

POST /api/v1/auth/login
  → returns { accessToken, refreshToken, expiresAt }

  Include on every protected request:
  Authorization: Bearer <accessToken>

POST /api/v1/auth/refresh-token  (when accessToken expires)
  body: { accessToken, refreshToken }
  → returns new { accessToken, refreshToken }

POST /api/v1/auth/logout
  → refresh token invalidated server-side
```

Refresh tokens are stored as SHA-256 hashes in the database. The previous token hash is retained to detect and reject reuse, which signals token theft.

---

## Security

| Control | Implementation |
|---|---|
| Authentication | JWT HS256 with 15-minute access token lifetime |
| Token rotation | Refresh token replaced on every use; previous hash retained for reuse detection |
| Account lockout | 5 failed login attempts trigger a 15-minute lockout |
| Rate limiting | Global 100 req/min; auth endpoints 5 req/min; export 50 req/min — partitioned by user identity or IP |
| Password requirements | Minimum 8 chars, uppercase, lowercase, digit, special character |
| Password reset | Single-use token with 2-hour expiry; `forgot-password` always returns 200 to prevent email enumeration |
| HTTP headers | `X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`, `Referrer-Policy`, `Content-Security-Policy` on every response |
| Error responses | Domain exceptions return RFC 7807 `ProblemDetails`; stack traces only included in Development |
| Data ownership | All repository queries are scoped to the authenticated user's ID — cross-user data access returns 404 |
| Audit trail | `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy` auto-stamped on every entity write via EF Core interceptor |

---

## Observability

Every HTTP request produces one structured Serilog event containing:

```
RequestMethod   GET
RequestPath     /api/v1/transaction
StatusCode      200
Elapsed         12.3ms
CorrelationId   a3f9b2c1d4e8f012
UserId          8d3a1b2c-...
RequestHost     localhost:5000
UserAgent       PostmanRuntime/7.36.0
MachineName     smartexpense-api
Application     SmartExpense.Api
```

**Correlation IDs** are generated per request (16-char hex) or inherited from the incoming `X-Correlation-Id` header — enabling end-to-end tracing across service boundaries. The ID is echoed on every response header so callers can correlate client-side errors to server-side log events.

Exception events carry `ExceptionType`, `UserId`, and `CorrelationId` as structured properties, allowing Seq queries like `ExceptionType = 'NotFoundException'` or `CorrelationId = 'a3f9b2c1'` to instantly surface the full request timeline.

Health-check polling is suppressed to `Verbose` level to prevent it from drowning application events.

---

## CI/CD Pipeline

**CI** runs on every push to `master`, `feat/**`, `fix/**`, `refactor/**`, and `docs/**`, and on every pull request to `master`:

```
Checkout → Restore → Build (Release) → Run Tests → Upload .trx results
```

**CD** runs automatically after CI succeeds on `master`:

```
Checkout → Login to GHCR → Build & push multi-tag Docker image
  Tags: latest, <commit-sha>
```

The image is published to `ghcr.io/kareem-sabry/smartexpense`.

---

## Running Tests

```bash
dotnet test SmartExpense.sln -c Release --verbosity normal
```

The test suite covers service, repository, and controller layers using xUnit, Moq, and FluentAssertions, with an in-memory EF Core provider for repository tests.

```
130+ unit tests
90%+ code coverage
```