# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["RebalancePilot.Api/RebalancePilot.Api.csproj", "RebalancePilot.Api/"]
RUN dotnet restore "RebalancePilot.Api/RebalancePilot.Api.csproj"

COPY . .
WORKDIR "/src/RebalancePilot.Api"
RUN dotnet build "RebalancePilot.Api.csproj" -c Release -o /app/build
RUN dotnet publish "RebalancePilot.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RebalancePilot.Api.dll"]
