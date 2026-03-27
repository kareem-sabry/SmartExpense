# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first — lets Docker cache the restore layer
COPY ["SmartExpense.Api/SmartExpense.Api.csproj",                         "SmartExpense.Api/"]
COPY ["SmartExpense.Application/SmartExpense.Application.csproj",         "SmartExpense.Application/"]
COPY ["SmartExpense.Core/SmartExpense.Core.csproj",                       "SmartExpense.Core/"]
COPY ["SmartExpense.Infrastructure/SmartExpense.Infrastructure.csproj",   "SmartExpense.Infrastructure/"]

RUN dotnet restore "SmartExpense.Api/SmartExpense.Api.csproj"

# Copy the rest of the source and build
COPY . .
WORKDIR "/src/SmartExpense.Api"
RUN dotnet build "SmartExpense.Api.csproj" -c Release -o /app/build --no-restore

# ── Publish stage ─────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "SmartExpense.Api.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SmartExpense.Api.dll"]