# Guarda a ligação à base de dados e as chaves do Supabase em dotnet user-secrets (fora do projeto).
# Uso (na raiz do projeto): .\scripts\configurar-segredos.ps1
#
# Onde encontrar cada valor no Supabase:
#   Ligação à base de dados : Connect > Direct > Session pooler (formato .NET), com a palavra-passe
#   URL do projeto          : Project Settings > Data API (ex.: https://abcdefgh.supabase.co)
#   Chave pública           : Project Settings > API Keys > publishable key (ou "anon" nas chaves antigas)
#   Chave secreta           : Project Settings > API Keys > secret key (ou "service_role" nas chaves antigas)
# Os valores secretos não aparecem no ecrã enquanto os escreve.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$api = Join-Path $raiz 'src/Avarias.Api'

function Read-Segredo([string]$pergunta) {
    $seguro = Read-Host $pergunta -AsSecureString
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

function Set-Segredo([string]$nome, [string]$valor) {
    if ([string]::IsNullOrWhiteSpace($valor)) { Write-Host "  (sem alteração: $nome)"; return }
    dotnet user-secrets set $nome $valor.Trim() --project $api | Out-Null
    Write-Host "  guardado: $nome"
}

dotnet user-secrets init --project $api | Out-Null
Write-Host 'Deixe em branco para manter o valor que já estiver guardado.'

Set-Segredo 'ConnectionStrings:Default' (Read-Segredo 'Ligação à base de dados (User Id=...;Password=...;Server=...;Port=5432;Database=postgres;SSL Mode=Require;Trust Server Certificate=true)')
Set-Segredo 'Supabase:Url' (Read-Host 'URL do projeto Supabase (https://....supabase.co)')
Set-Segredo 'Supabase:AnonKey' (Read-Host 'Chave pública (publishable / anon)')
Set-Segredo 'Supabase:ServiceRoleKey' (Read-Segredo 'Chave secreta (secret / service_role)')

Write-Host ''
Write-Host 'Segredos guardados. Arranque com .\scripts\correr-api.ps1'
