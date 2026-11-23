# Arquitetura

Uma só aplicação ASP.NET Core 8 que serve a **API** (`/api/...`) e o **frontend** (ficheiros estáticos em `wwwroot`) na mesma origem. Não há passo de build no frontend nem frameworks JavaScript.

```
Browser ──► ASP.NET Core (Kestrel, porta 5080)
              ├─ /*.html, /css, /js, /img   → wwwroot (estático)
              └─ /api/*                      → Controllers ──► EF Core ──► PostgreSQL (Supabase)
                                                         ├──► Supabase Auth (login, contas)
                                                         └──► pasta uploads/ (fotografias)
```

## Estrutura de pastas

```
Projeto PASP/
├─ Avarias.sln                 solução (abrir no Visual Studio / Rider / VS Code)
├─ Directory.Build.props       definições .NET comuns (net8.0, nullable…)
├─ global.json                 versão do SDK .NET
├─ .editorconfig               estilo de código
├─ README.md                   arranque rápido
├─ HANDOFF.md                  estado do projeto e decisões
│
├─ src/Avarias.Api/            API + frontend
│  ├─ Program.cs               arranque (curto; a configuração está em Extensions/)
│  ├─ Extensions/              ServiceCollectionExtensions (BD, JWT, rate limit, erros)
│  │                           WebApplicationExtensions (pipeline, criar BD + seed)
│  ├─ Controllers/             Auth, Catalogos, Avarias, Admin
│  ├─ Models/                  uma classe por tabela do Supabase; ContaAuth (auth.users, só leitura);
│  │                           Textos (compara perfis e estados por texto)
│  │  └─ Enums/                Perfil
│  ├─ Dtos/                    AuthDtos, CatalogoDtos, AvariaDtos, AdminDtos
│  ├─ Data/                    AppDbContext (mapeamento das tabelas), DbSeeder (perfis e estados)
│  ├─ Services/
│  │  ├─ Supabase/             ISupabaseAuth, SupabaseAuthClient (REST /auth/v1), SupabaseJwks
│  │  └─ Fotos/                ArmazenamentoFotos (pasta uploads/)
│  ├─ Properties/              launchSettings.json (dotnet run → Development)
│  ├─ appsettings*.json        configuração (sem segredos: esses vão em user-secrets)
│  └─ wwwroot/                 frontend
│     ├─ *.html                uma página por ecrã
│     ├─ css/styles.css        todo o CSS, tokens do design system no topo
│     ├─ img/                  logótipo da escola
│     └─ js/
│        ├─ core/              api.js (cliente da API), common.js (el(), autenticar(), cabeçalho),
│        │                     demo.js (API simulada para o modo demo)
│        └─ pages/             um módulo por página HTML (login.js, painel.js, …)
│
├─ tests/Avarias.Api.Tests/    xUnit + WebApplicationFactory + EF InMemory
├─ database/
│  └─ scripts/                 SQL para correr à mão no Supabase
├─ scripts/                    atalhos PowerShell (correr API, demo, testes)
└─ docs/                       esta documentação
```

## Fluxo de dados (do enunciado)

```mermaid
graph LR
    Professor([Professor]) -- Info_avaria --> P1[[Registar Avaria]]
    Biblioteca([Biblioteca]) -- Info_avaria --> P1
    P1 --> D1[(Avarias por resolver)]
    P1 -- notifica --> Tecnico(Técnico)
    Tecnico -- Relatório de resolução --> P2[[Registar Resolução]]
    P2 --> D2[(Histórico)]
    Biblioteca -- Pedido de consulta --> P3[[Consultar Avarias]]
    P3 --> D1
    P3 --> D2
```

Na implementação, as avarias pendentes são as da tabela `avarias` que não estão no estado "Resolvido", e cada registo e alteração grava uma cópia em `historico_avarias`.

## Onde pôr código novo

| Quero… | Onde |
|---|---|
| Uma tabela nova | Criar no Supabase; depois `Models/NovaTabela.cs` + `DbSet` e mapeamento em `Data/AppDbContext.cs` (ver [base-de-dados.md](base-de-dados.md)) |
| Um endpoint novo | Controller existente ou novo em `Controllers/`; pedidos e respostas em `Dtos/` |
| Lógica reutilizável (Supabase, ficheiros, relatórios) | `Services/<Área>/` e registar em `Extensions/ServiceCollectionExtensions.cs` |
| Uma página nova | `wwwroot/nova.html` + `wwwroot/js/pages/nova.js`; usar `el()` e `autenticar()` de `js/core/common.js` |
| Um endpoint novo no modo demo | `wwwroot/js/core/demo.js` |
| Um teste | `tests/Avarias.Api.Tests/` (usa `ApiFactory`: base de dados em memória e Supabase Auth falso) |

## Convenções

- Português de Portugal em textos, nomes de classes e comentários.
- Erros da API sempre como `{ "message": "..." }`.
- Frontend: DOM criado com `el()` (só `textContent`, nunca `innerHTML`).
- Segredos só em `dotnet user-secrets` (desenvolvimento) ou variáveis de ambiente (produção).
