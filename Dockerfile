# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first — lets Docker cache the restore layer
COPY ["SmartExpense.Api/SmartExpense.Api.csproj",                         "SmartExpense.Api/"]
COPY ["SmartExpense.Application/SmartExpense.Application.csproj",         "SmartExpense.Application/"]
COPY ["SmartExpense.Core/SmartExpense.Core.csproj",                       "SmartExpense.Core/"]
COPY ["SmartExpense.Infrastructure/SmartExpense.Infrastructure.csproj",   "SmartExpense.Infrastructure/"]

RUN dotnet restore "SmartExpense.Api/SmartExpense.Api.csproj"

# Copy the rest of the source and publish in one step.

COPY . .
RUN dotnet publish "SmartExpense.Api/SmartExpense.Api.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SmartExpense.Api.dll"]