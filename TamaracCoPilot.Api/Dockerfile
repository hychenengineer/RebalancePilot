# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TamaracCoPilot.Api/TamaracCoPilot.Api.csproj", "TamaracCoPilot.Api/"]
RUN dotnet restore "TamaracCoPilot.Api/TamaracCoPilot.Api.csproj"

COPY . .
WORKDIR "/src/TamaracCoPilot.Api"
RUN dotnet build "TamaracCoPilot.Api.csproj" -c Release -o /app/build
RUN dotnet publish "TamaracCoPilot.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TamaracCoPilot.Api.dll"]
