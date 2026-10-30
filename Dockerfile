# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a project file changes.
COPY Directory.Build.props ./
COPY src/CompanyApi/CompanyApi.csproj src/CompanyApi/
RUN dotnet restore src/CompanyApi/CompanyApi.csproj

COPY src/CompanyApi/ src/CompanyApi/
RUN dotnet publish src/CompanyApi/CompanyApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

COPY --from=build /app/publish .

# The base image provides a non-root "app" user; APP_UID is its numeric id.
USER $APP_UID
ENTRYPOINT ["dotnet", "CompanyApi.dll"]
