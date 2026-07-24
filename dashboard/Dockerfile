# ── Build aşaması ──────────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

# Dependency cache katmanı
COPY package*.json ./
RUN npm install

# Kaynak kod
COPY . .

# Build argümanları (docker-compose'dan geliyor)
ARG VITE_API_URL=""
ARG VITE_TRACKER_URL="https://yourdomain.com/tracker.js"
ENV VITE_API_URL=$VITE_API_URL
ENV VITE_TRACKER_URL=$VITE_TRACKER_URL

RUN npm run build

# ── Nginx aşaması ──────────────────────────────────────────────────────
FROM nginx:stable-alpine AS runtime

# React build çıktısını Nginx'e kopyala
COPY --from=build /app/dist /usr/share/nginx/html

# Nginx konfigürasyonu:
# - /api isteklerini API'ye yönlendir (proxy)
# - SPA routing için 404'leri index.html'e yönlendir
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
