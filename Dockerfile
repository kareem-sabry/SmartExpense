# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first — lets Docker cache the restore layer
COPY ["src/SmartExpense.Api/SmartExpense.Api.csproj",               "src/SmartExpense.Api/"]
COPY ["src/SmartExpense.Application/SmartExpense.Application.csproj","src/SmartExpense.Application/"]
COPY ["src/SmartExpense.Core/SmartExpense.Core.csproj",             "src/SmartExpense.Core/"]
COPY ["src/SmartExpense.Infrastructure/SmartExpense.Infrastructure.csproj","src/SmartExpense.Infrastructure/"]
RUN dotnet restore "src/SmartExpense.Api/SmartExpense.Api.csproj"

# Copy the rest of the source and publish in one step.

COPY . .
RUN dotnet publish "src/SmartExpense.Api/SmartExpense.Api.csproj" \
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