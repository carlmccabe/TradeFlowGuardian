FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["TradeFlowGuardian.Api/TradeFlowGuardian.Api.csproj", "TradeFlowGuardian.Api/"]
COPY ["TradeFlowGuardian.Core/TradeFlowGuardian.Core.csproj", "TradeFlowGuardian.Core/"]
COPY ["TradeFlowGuardian.Infrastructure/TradeFlowGuardian.Infrastructure.csproj", "TradeFlowGuardian.Infrastructure/"]
RUN dotnet restore "TradeFlowGuardian.Api/TradeFlowGuardian.Api.csproj"

COPY . .
RUN dotnet build "TradeFlowGuardian.Api/TradeFlowGuardian.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TradeFlowGuardian.Api/TradeFlowGuardian.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "TradeFlowGuardian.Api.dll"]
