# Arranca a API e o frontend em http://localhost:5080 (ambiente Development).
# Antes da 1.ª vez: configurar user-secrets (ver README.md).
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $raiz 'src/Avarias.Api')
