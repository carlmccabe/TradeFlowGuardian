FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["TradeFlowGuardian.Api/TradeFlowGuardian.Api.csproj", "TradeFlowGuardian.Api/"]
COPY ["TradeFlowGuardian.Core/TradeFlowGuardian.Core.csproj", "TradeFlowGuardian.Core/"]
COPY ["TradeFlowGuardian.Infrastructure/TradeFlowGuardian.Infrastructure.csproj", "TradeFlowGuardian.Infrastructure/"]
COPY ["TradeFlowGuardian.Backtesting/TradeFlowGuardian.Backtesting.csproj", "TradeFlowGuardian.Backtesting/"]
COPY ["TradeFlowGuardian.Domain/TradeFlowGuardian.Domain.csproj", "TradeFlowGuardian.Domain/"]
COPY ["TradeFlowGuardian.Strategies/TradeFlowGuardian.Strategies.csproj", "TradeFlowGuardian.Strategies/"]
RUN dotnet restore "TradeFlowGuardian.Api/TradeFlowGuardian.Api.csproj"

COPY . .
RUN dotnet build "TradeFlowGuardian.Api/TradeFlowGuardian.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TradeFlowGuardian.Api/TradeFlowGuardian.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Railway injects $PORT at runtime; default to 8080 for local/Docker Compose use.
CMD ["sh", "-c", "exec dotnet TradeFlowGuardian.Api.dll --urls http://+:${PORT:-8080}"]
