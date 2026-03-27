# SmartExpense API

A full-featured expense tracking REST API built with ASP.NET Core 8 and Clean Architecture. I built this to demonstrate what a real production backend looks like — proper architecture, security, testing, and features that go beyond basic CRUD.

---

## Features

- **JWT authentication** — register, login, refresh token rotation, account lockout, and password reset via email
- **Transaction management** — full CRUD with pagination, filtering, sorting, and CSV export
- **Budget tracking** — per-category monthly budgets with live spend calculation and status alerts (under / approaching / exceeded)
- **Recurring transactions** — frequency-based templates (daily/weekly/monthly/yearly) with FK-based deduplication
- **Financial analytics** — spending trends, category breakdowns, month-over-month comparisons, and budget performance
- **Admin panel** — user management and role assignment
- **Rate limiting** — global and per-endpoint limits with IP-based partitioning
- **Security headers** — X-Content-Type-Options, X-Frame-Options, CSP, and more
- **Audit trail** — every entity write stamps `CreatedBy`, `UpdatedBy`, `CreatedAtUtc`, `UpdatedAtUtc` via an EF Core interceptor
- **Health check** — `/health` returns structured JSON with per-component status

---

## Architecture

```
SmartExpense.Core           → Entities, domain exceptions, interfaces, enums
SmartExpense.Application    → DTOs, service interfaces, repository interfaces
SmartExpense.Infrastructure → EF Core repos, service implementations, interceptors
SmartExpense.Api            → Controllers, middleware, Program.cs
SmartExpense.Tests          → xUnit unit tests (services, repositories, controllers)
```

Dependencies only point inward: `Api → Infrastructure → Application → Core`.

---

## Getting Started

### Option A — Docker (recommended, zero setup)

```bash
git clone https://github.com/karem-sabry/SmartExpense.git
cd SmartExpense
cp .env.example .env        # fill in your values
docker compose up --build
```

The API will be available at `http://localhost:5000`.  
Swagger UI opens at `http://localhost:5000/swagger`.  
SQL Server data is persisted in a Docker volume across restarts.

### Option B — Local development

**Prerequisites:** .NET 8 SDK, SQL Server (or LocalDB)

1. Clone the repo

2. Set up user secrets (never commit real values):
```bash
cd SmartExpense.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=SmartExpense;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet user-secrets set "JwtOptions:Secret" "your-super-secret-jwt-key-min-32-chars"
dotnet user-secrets set "JwtOptions:Issuer" "SmartExpenseApi"
dotnet user-secrets set "JwtOptions:Audience" "SmartExpenseClient"
dotnet user-secrets set "AdminUser:Email" "admin@smartexpense.com"
dotnet user-secrets set "AdminUser:Password" "Admin@12345"
dotnet user-secrets set "AdminUser:FirstName" "Admin"
dotnet user-secrets set "AdminUser:LastName" "User"
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

130 unit tests covering service layer business logic, repository queries, and controller responses.  
90%+ code coverage via FluentAssertions + Moq.

---

## Environment Variables

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `JwtOptions__Secret` | JWT signing key (minimum 32 characters) |
| `JwtOptions__Issuer` | JWT issuer claim |
| `JwtOptions__Audience` | JWT audience claim |
| `JwtOptions__ExpirationMinutes` | Access token lifetime in minutes |
| `AdminUser__Email` | Seeded admin account email |
| `AdminUser__Password` | Seeded admin account password |
| `EmailOptions__SmtpHost` | SMTP host for password reset emails |
| `EmailOptions__SmtpPort` | SMTP port |
| `EmailOptions__FromEmail` | Sender address |

---

## API Overview

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/v1/auth/register` | — | Register a new user |
| POST | `/api/v1/auth/login` | — | Login and receive JWT + refresh token |
| POST | `/api/v1/auth/refresh-token` | — | Rotate refresh token |
| POST | `/api/v1/auth/forgot-password` | — | Send password reset email |
| GET | `/api/v1/transactions` | User | Paginated and filtered transaction list |
| POST | `/api/v1/transactions` | User | Create transaction |
| GET | `/api/v1/transactions/export` | User | Download transactions as CSV |
| GET | `/api/v1/budgets` | User | List budgets |
| GET | `/api/v1/budgets/summary` | User | Monthly budget summary with status |
| POST | `/api/v1/recurring` | User | Create recurring template |
| POST | `/api/v1/recurring/{id}/generate` | User | Manually trigger transaction generation |
| GET | `/api/v1/analytics/overview` | User | Financial overview dashboard |
| GET | `/api/v1/analytics/trends` | User | Spending trends |
| GET | `/api/v1/analytics/monthly-comparison` | User | Month-over-month comparison |
| GET | `/api/v1/admin/users` | Admin | List all users with roles |
| POST | `/api/v1/admin/users/{id}/roles` | Admin | Assign role to user |
| GET | `/health` | — | Service health status |

Full Swagger documentation is available at `/swagger` when running.

---

## Recurring Transactions

Recurring templates define a category, amount, and frequency. The service generates actual `Transaction` records for every due date since the last run. Deduplication uses a `RecurringTransactionId` FK — not string matching — so it is immune to description changes.

---

## Tech Stack

- **ASP.NET Core 8** — Web API
- **Entity Framework Core 8** — Code-first, migrations
- **SQL Server** — Relational database
- **xUnit + Moq + FluentAssertions** — Testing
- **Swagger / OpenAPI** — API documentation

---

## Contact

**Karem Sabry**

- GitHub: [@kareem-sabry](https://github.com/kareem-sabry)
- LinkedIn: [k-sabry](https://www.linkedin.com/in/kareem-sabry/)
- Email: kareemsabry.mail@gmail.com

---

## License

MIT