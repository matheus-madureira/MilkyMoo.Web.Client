<#
.SYNOPSIS
    Build multi-arquitetura da imagem MilkyMoo e push para o Docker Hub.

.DESCRIPTION
    Empacota o comando `docker buildx build` para nao depender de colar
    varias linhas no terminal. Sempre marca a versao explicita e `latest`,
    para que haja rollback possivel.

.EXAMPLE
    .\scripts\publish.ps1 1.0.0

.EXAMPLE
    # So constroi, sem publicar (valida o build multi-arch antes do push)
    .\scripts\publish.ps1 1.0.0 -NoPush
#>
[CmdletBinding()]
param(
    # Versao da imagem, ex.: 1.0.0
    [Parameter(Mandatory, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+')]
    [string]$Version,

    # Repositorio no Docker Hub
    [string]$Repository = 'blackadm7/milkymoo',

    [string]$Platforms = 'linux/amd64,linux/arm64',

    [string]$Builder = 'milkymoo-builder',

    # Pula o `--push`; util para validar o build sem publicar
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'

# Roda sempre a partir da raiz do repo, independente de onde foi invocado.
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
    # Cria o builder na primeira execucao; nas seguintes so o seleciona.
    # (Nao redirecionamos o stderr do docker: no PS 5.1 isso vira ErrorRecord
    #  e, com ErrorActionPreference='Stop', abortaria o script sem motivo.)
    $nameRegex = '^' + [regex]::Escape($Builder) + '\*?\s'
    $exists = @(docker buildx ls) -match $nameRegex

    if ($exists) {
        docker buildx use $Builder
    }
    else {
        Write-Host "Criando builder '$Builder'..." -ForegroundColor Cyan
        docker buildx create --name $Builder --use | Out-Null
    }

    $buildxArgs = @(
        'buildx', 'build'
        '--platform', $Platforms
        '-t', "${Repository}:$Version"
        '-t', "${Repository}:latest"
    )

    if ($NoPush) {
        # Sem --push nem --load: buildx multi-arch nao cabe no daemon local,
        # entao o resultado fica so no cache do builder.
        Write-Host "Build (sem push) de ${Repository}:$Version" -ForegroundColor Cyan
    }
    else {
        $buildxArgs += '--push'
        Write-Host "Build + push de ${Repository}:$Version e :latest" -ForegroundColor Cyan
    }

    $buildxArgs += '.'

    docker @buildxArgs
    if ($LASTEXITCODE -ne 0) {
        throw "docker buildx build falhou (exit $LASTEXITCODE)."
    }

    if (-not $NoPush) {
        Write-Host "`nPublicado:" -ForegroundColor Green
        docker buildx imagetools inspect "${Repository}:$Version"
    }
}
finally {
    Pop-Location
}
