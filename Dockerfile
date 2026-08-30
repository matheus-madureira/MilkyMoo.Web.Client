# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Stage 1: build + publish do Blazor WebAssembly
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# O relink nativo e feito pelo Emscripten, que precisa de Python — a imagem
# do SDK nao traz. `python` (sem o 3) e o nome que o emcc procura no PATH.
RUN apt-get update     && apt-get install -y --no-install-recommends python3     && ln -sf /usr/bin/python3 /usr/bin/python     && rm -rf /var/lib/apt/lists/*

# wasm-tools habilita o relink nativo do runtime durante o publish, o que
# reduz bastante o _framework. Camada propria: so refaz se o SDK mudar.
RUN dotnet workload install wasm-tools --skip-sign-check

# Copia so o csproj primeiro: a camada de restore so e invalidada quando as
# dependencias mudam, nao a cada alteracao de codigo.
COPY MilkyMoo.csproj ./
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore MilkyMoo.csproj

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish MilkyMoo.csproj \
        -c "$BUILD_CONFIGURATION" \
        --no-restore \
        -o /app/publish

# ---------------------------------------------------------------------------
# Stage 2: runtime estatico — nginx nao-root, escutando em 8080
# ---------------------------------------------------------------------------
FROM nginxinc/nginx-unprivileged:1.29-alpine AS final

LABEL org.opencontainers.image.title="MilkyMoo" \
      org.opencontainers.image.description="MilkyMoo — controle de estoque (Blazor WebAssembly PWA)" \
      org.opencontainers.image.source="https://github.com/matheus-madureira/MilkyMoo" \
      org.opencontainers.image.licenses="MIT"

USER root
# Remover o default.conf tambem evita que o script de entrypoint
# 10-listen-on-ipv6-on-ipv4.sh tente reescreve-lo (o que quebraria read_only).
RUN rm -f /etc/nginx/conf.d/default.conf
COPY nginx/nginx.conf            /etc/nginx/conf.d/milkymoo.conf
COPY nginx/security-headers.conf /etc/nginx/security-headers.conf
USER nginx

COPY --from=build --chown=nginx:nginx /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:8080/ || exit 1

STOPSIGNAL SIGQUIT
CMD ["nginx", "-g", "daemon off;"]
