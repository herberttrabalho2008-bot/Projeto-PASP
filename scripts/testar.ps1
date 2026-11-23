# Compila a solução e corre os testes (não precisam de base de dados).
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
dotnet test (Join-Path $raiz 'Avarias.sln')
