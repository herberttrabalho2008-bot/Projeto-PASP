# Base de dados

As tabelas estão no Supabase (estrutura em `esquema-supabase.csv`). A API ainda usa o modelo anterior, definido em `src/Avarias.Api/Data/AppDbContext.cs` (ver [docs/base-de-dados.md](../docs/base-de-dados.md)), e tem de ser adaptada. Os scripts SQL desta pasta correm-se à mão no *SQL Editor* do Supabase.

| Pasta / ficheiro | Para quê |
|---|---|
| `esquema-supabase.csv` | Estrutura atual das tabelas no Supabase (exportada do SQL Editor) |
| `scripts/criar-utilizador.sql` | Criar uma conta no modelo antigo da API (a rever com o novo esquema) |

Alterações ao esquema numa base já existente: pôr aqui um script novo com data no nome, por exemplo `scripts/2026-12-01-adicionar-coluna-x.sql`.
