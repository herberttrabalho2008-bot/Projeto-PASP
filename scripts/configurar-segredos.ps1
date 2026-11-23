# Guarda a ligação à base de dados e a chave JWT em dotnet user-secrets (fora do projeto).
# Uso: .\scripts\configurar-segredos.ps1
# Pede a connection string; a chave JWT é gerada automaticamente.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$api = Join-Path $raiz 'src/Avarias.Api'

$ligacao = Read-Host 'Connection string do Supabase (Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require)'
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$chave = [Convert]::ToBase64String($bytes)

dotnet user-secrets init --project $api | Out-Null
dotnet user-secrets set 'ConnectionStrings:Default' $ligacao --project $api
dotnet user-secrets set 'Jwt:Key' $chave --project $api
Write-Host 'Segredos guardados. Arranque com .\scripts\correr-api.ps1'
