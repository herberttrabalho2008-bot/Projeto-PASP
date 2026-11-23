# Projeto Avaria — Gestão de Avarias

Aplicação web para reportar e acompanhar avarias de equipamentos nas salas do Agrupamento de Escolas de Lousada. Os professores e a biblioteca registam a avaria, o técnico trata dela e regista a resolução, e fica tudo guardado num histórico.

## Índice

1. [Problema](#problema)
2. [Solução](#solução)
3. [Perfis de utilizador](#perfis-de-utilizador)
4. [Fluxo de dados](#fluxo-de-dados)
5. [Tecnologias](#tecnologias)
6. [Estrutura do projeto](#estrutura-do-projeto)
7. [Base de dados](#base-de-dados)
8. [Como correr](#como-correr)
9. [Estado do projeto](#estado-do-projeto)
10. [Documentação](#documentação)

## Problema

Quando um equipamento avaria numa sala, o problema é normalmente comunicado na biblioteca e registado numa folha de cálculo em Excel. A informação que chega ao técnico, e o que ele fez para resolver, não fica documentada. O processo é difícil de controlar e torna-se mais complicado à medida que a escola recebe mais equipamentos.

## Solução

Um site próprio, com login, onde:

- quem encontra a avaria escolhe a sala e o equipamento, descreve o problema e pode juntar uma fotografia;
- o técnico vê as avarias por resolver, muda o estado e escreve o relatório da resolução;
- a biblioteca consulta as avarias pendentes e o histórico;
- cada alteração fica registada.

## Perfis de utilizador

| Ação | Professor | Biblioteca | Técnico |
|---|---|---|---|
| Registar avaria | sim | sim | não |
| Ver avarias | só as suas | todas (só leitura) | todas |
| Mudar estado e registar resolução | não | não | sim |
| Gerir contas, salas, equipamentos e tipos (Backoffice) | não | não | sim |

As permissões são sempre verificadas no servidor. Detalhe em [docs/permissoes-e-seguranca.md](docs/permissoes-e-seguranca.md).

## Fluxo de dados

```mermaid
graph LR
    Professor([Professor])
    Biblioteca([Biblioteca])
    Tecnico( Técnico )
    P1[[P1:<br>Registar Avaria]]
    P2[[P2:<br>Registar Resolução]]
    P3[[P3:<br>Consultar Avarias]]
    D1[( Avarias_pendentes )]
    D2[( Historico_avarias )]
Professor -- "Info_avaria" --> P1
P1 -- "Info_avaria" --> D1
P1 -- "Info_avaria" --> Tecnico
Tecnico -- "Relatorio_resolucao" --> P2
P2 -- "Relatorio_resolucao" --> D2
P2 -- "Marcar_resolvida" --> D1
Biblioteca -- "Pedido_consulta" --> P3
Biblioteca -- "Info_avaria" --> P1
P3 -- "Consulta_pendentes" --> D1
D1 -- "Avarias_pendentes" --> P3
P3 -- "Consulta_historico" --> D2
D2 -- "Historico_avarias" --> P3
P3 -- "Avarias_pendentes" --> Biblioteca
P3 -- "Historico_avarias" --> Biblioteca
```

## Tecnologias

| Parte | Tecnologia |
|---|---|
| Frontend | HTML, CSS e JavaScript (módulos ES, sem frameworks nem passo de build) |
| Backend | Web API em C# com ASP.NET Core 8 e Entity Framework Core |
| Autenticação | Supabase Auth (a API faz de intermediária: o browser nunca vê as chaves) |
| Base de dados | PostgreSQL no Supabase |
| Testes | xUnit com `WebApplicationFactory` |

A API serve também o frontend, por isso a aplicação inteira abre num só endereço.

## Estrutura do projeto

```
Avarias.sln                    solução .NET (API + testes)
src/Avarias.Api/               API ASP.NET Core
├─ Controllers/                Auth, Catalogos, Avarias, Admin
├─ Models/                     uma classe por tabela do Supabase
├─ Dtos/                       pedidos e respostas da API
├─ Data/                       mapeamento das tabelas (AppDbContext)
├─ Services/                   Supabase Auth e fotografias
├─ Extensions/                 configuração do arranque
└─ wwwroot/                    frontend
   ├─ *.html                   uma página por ecrã
   ├─ css/styles.css           estilos e cores do design system
   └─ js/core/, js/pages/      código partilhado e um ficheiro por página
tests/Avarias.Api.Tests/       testes das regras de acesso
database/                      esquema do Supabase e scripts SQL
scripts/                       atalhos PowerShell
docs/                          documentação técnica
```

Onde pôr código novo: [docs/arquitetura.md](docs/arquitetura.md).

## Base de dados

PostgreSQL no Supabase. Estrutura atual (exportada em [database/esquema-supabase.csv](database/esquema-supabase.csv)):

```mermaid
erDiagram
    tipos_utilizador ||--o{ utilizadores : "permissao_id"
    utilizadores ||--o{ avarias : "id_utilizador"
    salas ||--o{ equipamentos : "nr_sala"
    tipos_equipamento ||--o{ equipamentos : "id_tipo"
    equipamentos ||--o{ avarias : "id_equipamento"
    estados_avarias ||--o{ avarias : "estado_id"
    avarias ||--o{ historico_avarias : "avaria_id"
    utilizadores ||--o{ historico_avarias : "id_utilizador"
```

| Tabela | Para quê | Campos principais |
|---|---|---|
| `utilizadores` | nome e perfil de cada conta (o `id` é o da conta no Supabase Auth) | `nome_utilizador`, `permissao_id` |
| `tipos_utilizador` | perfis | `tipo_utilizador` (Professor, Biblioteca, Técnico) |
| `salas` | salas e outros espaços | `nr_sala`, `tipo_sala` |
| `tipos_equipamento` | categorias de equipamento | `desc_tipo` |
| `equipamentos` | equipamentos de cada sala | `nr_serie`, `nr_sala`, `id_tipo`, `desc_equipamento` |
| `estados_avarias` | estados possíveis de uma avaria | `desc_estado` |
| `avarias` | avarias reportadas | `desc_geral`, `id_utilizador`, `id_equipamento`, `estado_id`, `data_avaria`, `foto_avaria`, `data_resolucao`, `desc_resolucao`, `foto_resolucao` |
| `historico_avarias` | cópia de cada registo, com os dados da altura (sala, equipamento, estado) | `avaria_id`, `desc_detalhes`, `desc_estado`, `data_registo` e cópias dos campos da avaria |

A API não cria nem altera tabelas: só garante os perfis em `tipos_utilizador` e os estados em `estados_avarias`. Detalhe em [docs/base-de-dados.md](docs/base-de-dados.md).

## Como correr

**Requisitos:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) e o projeto Supabase com as tabelas acima. Os comandos correm a partir da raiz do projeto, em PowerShell.

**1. Segredos.** O script pede quatro valores e guarda-os em `dotnet user-secrets`, fora do repositório:

```powershell
.\scripts\configurar-segredos.ps1
```

| Pedido | Onde está no Supabase |
|---|---|
| Ligação à base de dados | **Connect → Direct → Session pooler**, formato .NET, com a palavra-passe da base de dados |
| URL do projeto | **Project Settings → Data API** (`https://….supabase.co`) |
| Chave pública | **Project Settings → API Keys** → *publishable* (ou *anon*) |
| Chave secreta | **Project Settings → API Keys** → *secret* (ou *service_role*). Nunca a partilhe nem a ponha no código |

**2. Primeira conta de técnico.** No Supabase, **Authentication → Users → Add user** (com *Auto Confirm User*), e depois correr [database/scripts/criar-utilizador.sql](database/scripts/criar-utilizador.sql) no **SQL Editor** com o email e o nome. As outras contas criam-se no Backoffice da aplicação.

**3. Link de recuperação.** Em **Authentication → URL Configuration → Redirect URLs**, acrescentar `http://localhost:5080/repor-password.html`.

**4. Arrancar** (API e frontend em http://localhost:5080):

```powershell
.\scripts\correr-api.ps1
```

**Testes** (não precisam do Supabase):

```powershell
.\scripts\testar.ps1
```

**Modo demo.** Só o frontend, com dados de exemplo simulados no browser, sem API nem Supabase. Depois de correr o comando, abrir http://localhost:5173/login.html?demo=1 e entrar com `professor@escola.test`, `biblioteca@escola.test` ou `tecnico@escola.test` (qualquer palavra-passe):

```powershell
.\scripts\correr-demo.ps1
```

## Estado do projeto

Feito:

- [x] Base de dados no Supabase e API adaptada às tabelas
- [x] Login com Supabase Auth, sessão renovada automaticamente, recuperação de palavra-passe por email
- [x] Registar avaria (sala → equipamento, descrição, fotografia)
- [x] Painel com contagens por estado, pesquisa e filtros
- [x] Gestão técnica: estado, resolução e fotografia da resolução, com histórico
- [x] Backoffice: contas, salas, equipamentos e tipos de equipamento
- [x] Regras de acesso por perfil, com testes automáticos
- [x] Modo demo sem base de dados

Por fazer:

- [ ] Testar ponta a ponta contra o Supabase
- [ ] Configurar SMTP próprio no Supabase para os emails de recuperação
- [ ] Testar num telemóvel real
- [ ] Publicar ([docs/deploy.md](docs/deploy.md))

## Documentação

| Documento | Conteúdo |
|---|---|
| [docs/arquitetura.md](docs/arquitetura.md) | Estrutura das pastas e onde pôr código novo |
| [docs/api.md](docs/api.md) | Endpoints da API |
| [docs/base-de-dados.md](docs/base-de-dados.md) | Tabelas, regras e como mudar o esquema |
| [docs/permissoes-e-seguranca.md](docs/permissoes-e-seguranca.md) | Perfis e proteções |
| [docs/design-system.md](docs/design-system.md) | Cores, letra e componentes |
| [docs/deploy.md](docs/deploy.md) | Pôr em produção |
