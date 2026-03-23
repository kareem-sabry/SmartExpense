FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SmartExpense.Api/SmartExpense.Api.csproj", "SmartExpense.Api/"]
COPY ["SmartExpense.Application/SmartExpense.Application.csproj", "SmartExpense.Application/"]
COPY ["SmartExpense.Infrastructure/SmartExpense.Infrastructure.csproj", "SmartExpense.Infrastructure/"]
COPY ["SmartExpense.Core/SmartExpense.Core.csproj", "SmartExpense.Core/"]
RUN dotnet restore "SmartExpense.Api/SmartExpense.Api.csproj"
COPY . .
RUN dotnet build "SmartExpense.Api/SmartExpense.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SmartExpense.Api/SmartExpense.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartExpense.Api.dll"]