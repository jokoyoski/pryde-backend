# syntax=docker/dockerfile:1

# ---- Base: restore + full source, shared by publish and migrator stages ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Pryde.Api/Pryde.Api.csproj", "Pryde.Api/"]
COPY ["Pryde.Domain/Pryde.Domain.csproj", "Pryde.Domain/"]
COPY ["Pryde.Persistence/Pryde.Persistence.csproj", "Pryde.Persistence/"]
COPY ["Pryde.Services/Pryde.Services.csproj", "Pryde.Services/"]
COPY ["Pryde.Contracts/Pryde.Contracts.csproj", "Pryde.Contracts/"]
RUN dotnet restore "Pryde.Api/Pryde.Api.csproj"

COPY . .

# ---- Publish: produces the API runtime output ----
FROM build AS publish
WORKDIR /src/Pryde.Api
RUN dotnet publish "Pryde.Api.csproj" -c Release -o /app/publish --no-restore

# ---- Migrator: standalone, one-shot image that only applies EF Core migrations ----
# Pinned to match Microsoft.EntityFrameworkCore.Design version in Pryde.Persistence.csproj
FROM build AS migrator
RUN dotnet tool install --global dotnet-ef --version 9.0.13
ENV PATH="$PATH:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "ef", "database", "update", \
    "--project", "Pryde.Persistence/Pryde.Persistence.csproj", \
    "--startup-project", "Pryde.Api/Pryde.Api.csproj"]

# ---- Final: slim runtime image, no SDK, no EF tooling ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Pryde.Api.dll"]
