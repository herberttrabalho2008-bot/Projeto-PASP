# Handoff: Gestão de Avarias (Escola Secundária de Lousada)

Estado do projeto a 23/11/2026, para continuar numa conversa nova. Ler também o [README.md](README.md), que tem as instruções de arranque completas.

## O que é

Aplicação web em português de Portugal para reportar e gerir avarias na escola (Agrupamento de Escolas de Lousada). Login obrigatório, três perfis:

| Perfil | Pode |
|---|---|
| Professor | Criar reportes, ver só os seus, comentar |
| Biblioteca | Criar reportes, ver todos (só leitura), comentar. Com `Reportes:BibliotecaVeTudo=false` fica limitada aos locais da biblioteca |
| Técnico | Ver tudo, mudar estado, prioridade e resolução, filtrar. É o único com acesso ao **Backoffice** (contas, locais, equipamentos, tipos de avaria) |

Estados de um reporte: Pendente, Em análise, Em resolução, Resolvido (exige resolução escrita). Cada alteração fica no histórico.

## Stack

- **Backend:** ASP.NET Core 8 Web API (controllers), EF Core + Npgsql (PostgreSQL / Supabase), snake_case, `EnsureCreated` + `DbSeeder`. BCrypt (fator 11), JWT (8 h), rate limit 100/min por IP nas rotas de autenticação. Erros sempre como `{ "message": "..." }`.
- **Frontend:** HTML, CSS e JavaScript puros (módulos ES, sem build), servidos pela API a partir de `wwwroot` na mesma origem. O DOM é criado com `el()` (só `textContent`, sem `innerHTML`).
- **Testes:** xUnit + `WebApplicationFactory` com EF InMemory (não precisam de base de dados).

## Estrutura

Reorganizada a 23/11/2026 (cópia do estado anterior em `C:\Users\PC\Desktop\Projeto PASP - copia 2026-11-23`). Detalhe completo e "onde pôr código novo" em [docs/arquitetura.md](docs/arquitetura.md).

```
Avarias.sln, Directory.Build.props, global.json, .editorconfig
src/Avarias.Api/
  Program.cs                 curto; a configuração está em Extensions/
  Extensions/                ServiceCollectionExtensions (BD, JWT com conta ativa, rate limit, erros)
                             WebApplicationExtensions (pipeline, EnsureCreated + DbSeeder)
  Controllers/               Auth, Catalogos, Reportes, Admin
  Models/ (+ Enums/)         uma entidade / enum por ficheiro
  Dtos/                      AuthDtos, CatalogoDtos, ReporteDtos, AdminDtos
  Data/                      AppDbContext (única definição do esquema), DbSeeder
  Services/Auth/             TokenService
  Services/Email/            IEmailSender, ConsoleEmailSender, SmtpEmailSender
  wwwroot/
    *.html                   só marcação; cada página carrega js/pages/<página>.js
    css/styles.css           TODO o CSS, tokens do design system no topo
    js/core/                 api.js (cliente + modo demo), common.js (el(), autenticar(), cabeçalho), demo.js
    js/pages/                login, recuperar-password, repor-password, painel, novo-reporte, reporte, admin
tests/Avarias.Api.Tests/     RegrasDeAcessoTests.cs (4 testes)
database/scripts/            criar-utilizador.sql
scripts/                     configurar-segredos.ps1, correr-api.ps1, correr-demo.ps1, testar.ps1
docs/                        arquitetura, api, base-de-dados, permissoes-e-seguranca, design-system, deploy
```

## Correr

Ver o [README](README.md). Resumo (a partir da raiz): `scripts/configurar-segredos.ps1` (uma vez), `scripts/correr-api.ps1` (http://localhost:5080), `scripts/testar.ps1`, `scripts/correr-demo.ps1` (http://localhost:5173/login.html?demo=1). Sem `Jwt:Key` a API recusa arrancar (de propósito). Em `.claude/launch.json` há as configurações `frontend` e `api`.

## Contas de teste

Criadas só em Development e com a base de dados vazia (`SeedUtilizadoresTeste`). No modo demo a palavra-passe não é verificada.

| Perfil | Email | Palavra-passe |
|---|---|---|
| Professor | professor@escola.test | Professor123! |
| Biblioteca | biblioteca@escola.test | Biblioteca123! |
| Técnico | tecnico@escola.test | Tecnico123! |

## API

- `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/forgot-password`, `POST /api/auth/reset-password`
- `GET /api/catalogos` (locais com equipamentos, tipos)
- `GET /api/reportes` (filtros q, estado, localId, tipoId, urgencia, prioridade; máx. 500), `GET /api/reportes/contagens`, `GET /api/reportes/{id}`, `GET /api/reportes/{id}/foto`, `POST /api/reportes` (multipart, Professor/Biblioteca), `PATCH /api/reportes/{id}` (Técnico), `POST /api/reportes/{id}/comentarios`
- `/api/admin/...` (só Técnico): `utilizadores` (GET, POST, PUT `{id}`, POST `{id}/password`), `locais` (POST, PUT, DELETE), `locais/{id}/equipamentos` (POST), `equipamentos/{id}` (PUT, DELETE), `tipos` (POST, PUT, DELETE). Apagar algo com reportes devolve 409. O técnico não se pode desativar nem tirar o próprio perfil.

Um reporte fora do âmbito do utilizador responde 404. As permissões são sempre verificadas no servidor.

## Design system

**Backoffice Avarias**, criado a partir das imagens em `C:\Users\PC\Downloads\backoffice_design_referencia` (cores medidas pixel a pixel): https://claude.ai/artifact/UUEeyNicAkfyWHj4Epw1ag (privado). Aplicado a **toda** a app.

- Neutros cinzento/branco na maior parte do ecrã; roxo `#582C83` para a marca e "onde estou"; azul `#1B7FD5` só para a ação principal; vermelho `#D43432` em texto para destruir.
- Letra Fira Sans (interface) e Montserrat (nome da app).
- Estados: Pendente cinzento, Em análise azul-claro, Em resolução amarelo, Resolvido verde. Urgência com ponto colorido, prioridade em pílula roxa.
- Painel em tabela (em telemóvel cada linha empilha). Ações irreversíveis abrem um modal de confirmação.
- Os tokens estão no design system **e** copiados em `styles.css`: mudar nos dois.
- Sem logótipo da referência (é de outra empresa). O logótipo da escola aparece só no login.

## Decisões já tomadas

- Frontend sem frameworks nem Tailwind (pedido do utilizador).
- Backoffice gerido pelo Técnico, sem perfil Admin separado.
- `schema.sql` foi apagado: o modelo EF é a única definição do esquema.
- O design system anterior (índigo e bordô) foi descartado (pasta enviada para a Reciclagem).
- Textos com "palavra-passe" e "fotografia".

## Por fazer / riscos conhecidos

1. **O backend nunca correu contra uma base de dados real.** Falta configurar o Supabase e testar ponta a ponta.
2. **SMTP real não testado.** Sem `Smtp:Host` os emails de recuperação só aparecem no log. Configurar `Smtp:*` e `Frontend:BaseUrl` em produção.
3. **Base de dados já existente:** `EnsureCreated` não altera tabelas, por isso as regras de email em minúsculas e de perfil válido só existem numa base criada de raiz.
4. **1.ª conta de técnico em produção:** criar com `database/scripts/criar-utilizador.sql` (usa `extensions.crypt` do Supabase).
5. **Contraste:** texto branco sobre o azul `#1B7FD5` dá 4,2:1 (abaixo de AA). Alternativa: `#1769B0`.
6. Não testado num telemóvel real (só no browser a 375px). O ecrã de login e o de repor a palavra-passe não foram revistos visualmente depois do novo design.
7. `Trust Server Certificate=true` na ligação ao Supabase: não valida o certificado. Melhor: certificado do Supabase com `SSL Mode=VerifyFull`.
8. Não há migrações EF: mudanças futuras ao modelo exigem migrações ou SQL manual.

## Preferências do utilizador

- Responder em português de Portugal.
- Não há repositório git neste projeto.
- Segredos sempre por `dotnet user-secrets`, nunca no repositório.
