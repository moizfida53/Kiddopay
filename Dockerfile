# ── Stage 1: build ──────────────────────────────────────────────────────────
# Needs the .NET SDK *and* Node.js: Kiddopay.csproj has a PublishRunWebpack
# MSBuild target that shells out to `npm install` / `npm run build` inside
# ClientApp/ during `dotnet publish` (the same thing Visual Studio does on a
# dev machine, or during an Azure publish). Node isn't in the base SDK image,
# so it's installed here to keep that target working unmodified.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# Copy the whole project. appsettings.json / appsettings.Development.json are
# excluded via .dockerignore -- every setting the app needs comes from Render
# Environment Variables instead (see the hosting guide, Section 2), the same
# reason those files are already in .gitignore.
COPY . .

RUN dotnet restore "Kiddopay.csproj"
RUN dotnet publish "Kiddopay.csproj" -c Release -o /app/publish

# ── Stage 2: runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render's web services listen on port 10000 by default (the PORT environment
# variable Render sets for the container). ASPNETCORE_HTTP_PORTS (new in
# .NET 8) is the simplest way to bind Kestrel to it with no code changes. If
# you ever change Render's PORT setting away from 10000, update this to match.
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "Kiddopay.dll"]
