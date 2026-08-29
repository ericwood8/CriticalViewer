# Build context is the /backend folder:
#   docker build -f Dockerfile -t criticalviewer-api .
# (the GitHub Actions workflow in .github/workflows/backend.yml does this for you)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/CriticalViewer.Api/CriticalViewer.Api.csproj src/CriticalViewer.Api/
COPY src/CriticalViewer.Core/CriticalViewer.Core.csproj src/CriticalViewer.Core/
COPY src/CriticalViewer.Infrastructure/CriticalViewer.Infrastructure.csproj src/CriticalViewer.Infrastructure/
RUN dotnet restore src/CriticalViewer.Api/CriticalViewer.Api.csproj

COPY src/ src/
RUN dotnet publish src/CriticalViewer.Api/CriticalViewer.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CriticalViewer.Api.dll"]
