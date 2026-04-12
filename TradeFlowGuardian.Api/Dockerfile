FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
# HTTP_PORTS is injected by Railway at runtime; this default covers local/Docker Compose use.
# Do NOT set ASPNETCORE_URLS here — mixing URLS and HTTP_PORTS produces a harmless but
# noisy override warning from ASP.NET Core.
ENV ASPNETCORE_HTTP_PORTS=8080

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

CMD ["dotnet", "TradeFlowGuardian.Api.dll"]
