# ── Build aşaması ──────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Önce sadece proje dosyasını kopyala (dependency cache katmanı)
COPY *.csproj ./
RUN dotnet restore

# Kalanı kopyala ve yayınla
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Runtime aşaması ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# curl: healthcheck için gerekli
RUN apk add --no-cache curl

COPY --from=build /app/publish .

# MaxMind GeoIP DB (varsa) - opsiyonel
# COPY GeoLite2-City.mmdb .

EXPOSE 5000
ENTRYPOINT ["dotnet", "TrackerForSites.Api.dll"]
