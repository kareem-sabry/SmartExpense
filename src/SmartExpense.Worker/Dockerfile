# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first — lets Docker cache the restore layer.
COPY ["src/SmartExpense.Worker/SmartExpense.Worker.csproj",                "src/SmartExpense.Worker/"]
COPY ["src/SmartExpense.Application/SmartExpense.Application.csproj",      "src/SmartExpense.Application/"]
COPY ["src/SmartExpense.Core/SmartExpense.Core.csproj",                    "src/SmartExpense.Core/"]
COPY ["src/SmartExpense.Infrastructure/SmartExpense.Infrastructure.csproj","src/SmartExpense.Infrastructure/"]

RUN dotnet restore "src/SmartExpense.Worker/SmartExpense.Worker.csproj"

COPY . .

RUN dotnet publish "src/SmartExpense.Worker/SmartExpense.Worker.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────────────────────
# aspnet:9.0 (not runtime:9.0): AddHttpContextAccessor requires ASP.NET Core
# assemblies that are present in aspnet but not in the plain runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SmartExpense.Worker.dll"]