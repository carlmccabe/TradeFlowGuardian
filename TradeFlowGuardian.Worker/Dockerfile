FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["TradeFlowGuardian.Worker/TradeFlowGuardian.Worker.csproj", "TradeFlowGuardian.Worker/"]
COPY ["TradeFlowGuardian.Core/TradeFlowGuardian.Core.csproj", "TradeFlowGuardian.Core/"]
COPY ["TradeFlowGuardian.Infrastructure/TradeFlowGuardian.Infrastructure.csproj", "TradeFlowGuardian.Infrastructure/"]
COPY ["TradeFlowGuardian.Domain/TradeFlowGuardian.Domain.csproj", "TradeFlowGuardian.Domain/"]
COPY ["TradeFlowGuardian.Strategies/TradeFlowGuardian.Strategies.csproj", "TradeFlowGuardian.Strategies/"]
RUN dotnet restore "TradeFlowGuardian.Worker/TradeFlowGuardian.Worker.csproj"

COPY . .
RUN dotnet build "TradeFlowGuardian.Worker/TradeFlowGuardian.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TradeFlowGuardian.Worker/TradeFlowGuardian.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TradeFlowGuardian.Worker.dll"]
