# MilkyMoo — Docker

Aplicação Blazor WebAssembly (.NET 10). O `publish` gera um site 100% estático,
então a imagem final é apenas **nginx não-root servindo os arquivos** — sem
runtime .NET no container.

## Estrutura

| Arquivo | Papel |
| --- | --- |
| `Dockerfile` | Build multi-stage: SDK .NET 10 → `nginx-unprivileged:1.29-alpine` |
| `nginx/nginx.conf` | Fallback de SPA, MIME de `.wasm`, cache e `gzip_static` |
| `nginx/security-headers.conf` | Headers de segurança incluídos em cada `location` |
| `docker-compose.yml` | Execução local/produção com healthcheck e hardening |
| `.dockerignore` | Mantém `bin/`, `obj/`, `.git/`, `.vs/` fora do contexto |

## Rodar localmente

```bash
docker compose up --build
# http://localhost:8080
```

Porta customizada:

```bash
HOST_PORT=3000 docker compose up --build
```

## Build avulso

```bash
docker build -t milkymoo:local .
docker run --rm -p 8080:8080 milkymoo:local
```

## Publicar no Docker Hub

```bash
docker login

# 1. O `image:` do docker-compose.yml já aponta para blackadm7/milkymoo
# 2. Build multi-arquitetura (amd64 + arm64) e push em um passo:
docker buildx create --use --name milkymoo-builder   # só na primeira vez

docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t blackadm7/milkymoo:1.0.0 \
  -t blackadm7/milkymoo:latest \
  --push .
```

Sempre versione (`1.0.0`) além de `latest` — assim é possível fazer rollback.

No Windows, use o script em vez de colar o comando multi-linha (a continuação
do PowerShell é crase, não `\`, e uma colagem quebrada executa cada pedaço como
um comando separado):

```powershell
.\scripts\publish.ps1 1.0.0            # build multi-arch + push (versão e latest)
.\scripts\publish.ps1 1.0.0 -NoPush    # só valida o build, sem publicar
```

Se o push falhar com `insufficient_scope: authorization failed`, o token do
`docker login` não tem permissão de escrita: gere um PAT com **Read, Write,
Delete** em https://app.docker.com/settings/personal-access-tokens e refaça
`docker logout && docker login -u <usuario>`.

## Decisões de projeto

- **`nginx-unprivileged`**: roda como UID 101, escuta em **8080**, sem `setuid`.
- **Camada de restore separada**: `COPY MilkyMoo.csproj` antes do resto do código,
  então alterar um `.razor` não refaz o `dotnet restore`.
- **Cache de NuGet via BuildKit** (`--mount=type=cache`): não incha a imagem.
- **Workload `wasm-tools`**: habilita o *relink* nativo do runtime no `publish`,
  encolhendo o `_framework`. O relink é feito pelo Emscripten, que precisa de
  **Python** — por isso o `apt-get install python3` e o symlink `python`, já que
  é esse o nome que o `emcc` procura no `PATH`. Sem isso o build falha com
  `unable to find python in $PATH`.
  Localmente o workload é opcional: sem ele o `dotnet publish` apenas avisa e
  gera um `_framework` maior, sem quebrar.
- **`gzip_static on`**: o publish do Blazor já emite `.gz` ao lado de cada asset;
  o nginx entrega o arquivo pronto em vez de comprimir a cada request.
  (Os `.br` também vão na imagem, mas exigem o módulo `ngx_brotli`, ausente na
  imagem oficial — deixe o Brotli a cargo do proxy/CDN à frente, se houver.)
- **Cache**: `_framework/` e `_content/` têm fingerprint no nome → `immutable`
  por 1 ano. `index.html`, `service-worker.js` e `service-worker-assets.js`
  vão com `no-store`, senão o PWA nunca enxerga uma nova versão.
- **`location ^~ /_framework/`**: o `^~` é necessário para vencer a regex de
  estáticos, senão `blazor.webassembly.<hash>.js` cairia na regra genérica.
- **`read_only: true` + `tmpfs`**: o `default.conf` de fábrica é removido na
  imagem, o que impede o entrypoint `10-listen-on-ipv6-on-ipv4.sh` de tentar
  reescrevê-lo — por isso o filesystem pode ser somente leitura.
- **`.pdb` e `.map` retornam 404**: símbolos de debug não vão para produção.
- **Bootstrap enxuto**: `wwwroot/lib/bootstrap/dist` guarda só os `*.min.css` /
  `*.min.js`; os arquivos não-minificados e os `.map` foram removidos
  (8.4 MB → 980 KB). Ao atualizar o Bootstrap, refaça a poda:

  ```bash
  cd wwwroot/lib/bootstrap/dist
  find . -name '*.map' -delete
  find . -type f \( -name '*.css' -o -name '*.js' \) ! -name '*.min.css' ! -name '*.min.js' -delete
  ```
- **Imagens otimizadas** (4.4 MB → 96 KB): o `avatar-example.jpg` era 6220×6220
  para exibir em 40×40 (agora 160×160); o `favicon.svg` era um PNG 606×606
  embutido em base64 (agora 192×192); os ícones PNG foram quantizados para 256
  cores. Ao trocar qualquer asset, confira o tamanho antes de commitar.

## Atrás de um proxy reverso (Traefik / Caddy / nginx)

O container serve HTTP puro em 8080. TLS, HTTP/2 e Brotli ficam no proxy.
Se for servir em um subcaminho, ajuste `<base href="/" />` em
`wwwroot/index.html` antes do build.
