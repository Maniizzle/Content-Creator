# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/EventContent.Migrator/ src/EventContent.Migrator/
COPY db/ db/
RUN dotnet publish src/EventContent.Migrator/EventContent.Migrator.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "EventContent.Migrator.dll"]
