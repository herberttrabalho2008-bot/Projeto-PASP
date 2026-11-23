# Handoff: Gestão de Avarias (Escola Secundária de Lousada)

Estado do projeto a 23/11/2026, para continuar numa conversa nova. Ler também o [README.md](README.md) e a pasta [docs/](docs/README.md).

Repositório: https://github.com/herberttrabalho2008-bot/Projeto-PASP (ramo `main`).

Lista do que falta: [TODO.md](TODO.md).

## O que é

Aplicação web em português de Portugal para registar e gerir avarias de equipamentos nas salas da escola (Agrupamento de Escolas de Lousada). Login obrigatório, três perfis:

| Perfil | Pode |
|---|---|
| Professor | Registar avarias, ver só as suas |
| Biblioteca | Registar avarias, ver todas (só leitura). Com `Avarias:BibliotecaVeTudo=false` fica limitada às salas do tipo "Biblioteca" |
| Técnico | Ver tudo, mudar o estado, registar a resolução (texto + fotografia). É o único com acesso ao **Backoffice** (contas, salas, equipamentos, tipos de equipamento) |

Os estados vêm da tabela `estados_avarias` (Pendente, Em análise, Em resolução, Resolvido). "Resolvido" exige resolução escrita. Cada criação e alteração grava uma cópia em `historico_avarias`.

## Stack

- **Base de dados:** PostgreSQL no Supabase, com tabelas criadas pelo utilizador no Supabase (`database/esquema-supabase.csv`). A API **não cria nem altera** o esquema; só garante os perfis em `tipos_utilizador` e os estados em `estados_avarias` (se a tabela estiver vazia).
- **Autenticação:** Supabase Auth. A API faz de intermediária: `/api/auth/login` chama o Supabase, devolve o access token (≈1 h) e o refresh token; o frontend renova sozinho com `/api/auth/refresh`. A API valida os tokens pelas chaves públicas do projeto (JWKS) ou pelo `Supabase:JwtSecret` antigo, e em cada pedido lê o perfil de `utilizadores → tipos_utilizador` e confirma que a conta não está bloqueada (`auth.users.banned_until`). Desativar uma conta = ban no Supabase.
- **Backend:** ASP.NET Core 8 Web API, EF Core + Npgsql, snake_case. Erros sempre como `{ "message": "..." }`. Rate limit 100/min por IP nas rotas de autenticação.
- **Frontend:** HTML, CSS e JavaScript puros (módulos ES, sem build), servidos pela API a partir de `wwwroot`. DOM criado com `el()` (só `textContent`).
- **Fotografias:** pasta `uploads/` no servidor; na BD fica só o nome (`foto_avaria`, `foto_resolucao`).
- **Testes:** xUnit + `WebApplicationFactory` + EF InMemory + Supabase Auth falso (`tests/Avarias.Api.Tests/ApiFactory.cs`). 6 testes.

## Estrutura

Ver [docs/arquitetura.md](docs/arquitetura.md). Resumo:

```
src/Avarias.Api/
  Program.cs, Extensions/      arranque; autenticação Supabase em ServiceCollectionExtensions
  Controllers/                 Auth, Catalogos, Avarias, Admin
  Models/                      uma classe por tabela + ContaAuth (auth.users, só leitura) + Textos (perfis/estados por texto)
  Data/                        AppDbContext (mapeamento), DbSeeder (perfis e estados)
  Services/Supabase/           ISupabaseAuth, SupabaseAuthClient (REST /auth/v1), SupabaseJwks
  Services/Fotos/              ArmazenamentoFotos
  wwwroot/                     páginas, css/, js/core/ (api, common, demo), js/pages/
tests/Avarias.Api.Tests/
database/                      esquema-supabase.csv, scripts/criar-utilizador.sql
scripts/                       configurar-segredos.ps1, correr-api.ps1, correr-demo.ps1, testar.ps1
```

## Configuração (user-secrets em desenvolvimento)

`ConnectionStrings:Default` (Session pooler), `Supabase:Url`, `Supabase:AnonKey` (publishable/anon), `Supabase:ServiceRoleKey` (secret/service_role, para o Backoffice criar contas), opcional `Supabase:JwtSecret`. Tudo com `scripts/configurar-segredos.ps1`.

No Supabase: **Authentication → URL Configuration → Redirect URLs** tem de incluir `http://localhost:5080/repor-password.html` (e o endereço de produção) para o link de recuperação funcionar.

## Correr

Ver o [README](README.md): `scripts/configurar-segredos.ps1`, `scripts/correr-api.ps1` (http://localhost:5080), `scripts/testar.ps1`, `scripts/correr-demo.ps1` (http://localhost:5173/login.html?demo=1, contas `professor@`/`biblioteca@`/`tecnico@escola.test`, sem palavra-passe). Em `.claude/launch.json` há as configurações `frontend` e `api`.

## Design system

**Backoffice Avarias** (https://claude.ai/artifact/UUEeyNicAkfyWHj4Epw1ag, privado), criado a partir de `C:\Users\PC\Downloads\backoffice_design_referencia`. Tokens copiados no topo de `styles.css`: mudar nos dois. Detalhe em [docs/design-system.md](docs/design-system.md).

## Decisões já tomadas

- Frontend sem frameworks nem Tailwind.
- Base de dados desenhada pelo utilizador no Supabase; o código adapta-se a ela.
- Supabase Auth (em vez de login próprio). O browser nunca vê as chaves do Supabase.
- Sem título, urgência, prioridade nem comentários (não existem no esquema do utilizador).
- Backoffice gerido pelo Técnico, sem perfil Admin separado.
- Textos com "palavra-passe" e "fotografia".

## Por fazer / riscos conhecidos

1. **Testar ponta a ponta contra o Supabase real** (ainda só testado com testes automáticos e modo demo).
2. **Primeira conta de técnico:** criar em Authentication → Users e dar-lhe perfil com `database/scripts/criar-utilizador.sql`.
3. **Emails do Supabase:** o serviço de email incluído no Supabase tem limites baixos; para uso real configurar SMTP próprio no Supabase (Authentication → Emails → SMTP Settings).
4. **Fotografias em disco:** num alojamento sem disco persistente perdem-se. Alternativa futura: Supabase Storage.
5. **Contraste:** branco sobre `#1B7FD5` dá 4,2:1 (abaixo de AA). Alternativa: `#1769B0`.
6. Não testado num telemóvel real.
7. `Trust Server Certificate=true` na ligação: não valida o certificado.
8. RLS: as tabelas são lidas pela API com o utilizador `postgres` (ignora RLS). Se um dia o frontend falar diretamente com o Supabase, é preciso definir políticas RLS.

## Preferências do utilizador

- Responder em português de Portugal.
- Segredos sempre por `dotnet user-secrets`, nunca no repositório.
