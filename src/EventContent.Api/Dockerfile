# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
COPY db/ db/
RUN dotnet publish src/EventContent.Api/EventContent.Api.csproj -c Release -o /app --no-self-contained

# Chiseled image: no shell or package manager, runs as non-root user "app".
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "EventContent.Api.dll"]
