# Modo demo: só o frontend, com a API simulada no browser (sem base de dados).
# Abrir http://localhost:5173/login.html?demo=1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
python -m http.server 5173 --directory (Join-Path $raiz 'src/Avarias.Api/wwwroot')
