# Base de dados

As tabelas estão no Supabase e são a fonte da verdade. A API só as lê e escreve (mapeamento em `src/Avarias.Api/Data/AppDbContext.cs`, ver [docs/base-de-dados.md](../docs/base-de-dados.md)). Os scripts desta pasta correm-se à mão no *SQL Editor* do Supabase.

| Ficheiro | Para quê |
|---|---|
| `esquema-supabase.csv` | Estrutura atual das tabelas (exportada com a consulta abaixo) |
| `scripts/criar-utilizador.sql` | Dar perfil à 1.ª conta de técnico (as outras criam-se no Backoffice) |

Alterações ao esquema: guardar o SQL em `scripts/` com a data no nome (ex.: `scripts/2026-12-01-adicionar-coluna-x.sql`) e exportar de novo o CSV.

## Exportar a estrutura

```sql
select c.table_name, c.column_name, c.data_type, c.is_nullable, c.column_default,
       tc.constraint_type, ccu.table_name as ref_tabela, ccu.column_name as ref_coluna
from information_schema.columns c
left join information_schema.key_column_usage k
  on k.table_schema = c.table_schema and k.table_name = c.table_name and k.column_name = c.column_name
left join information_schema.table_constraints tc
  on tc.constraint_name = k.constraint_name and tc.table_schema = k.table_schema
left join information_schema.constraint_column_usage ccu
  on ccu.constraint_name = tc.constraint_name and tc.constraint_type = 'FOREIGN KEY'
where c.table_schema = 'public'
order by c.table_name, c.ordinal_position;
```
