-- =====================================================================
-- Dar perfil a uma conta do Supabase Auth (executar no SQL Editor do Supabase).
--
-- Use isto só para a PRIMEIRA conta de técnico. As outras contas criam-se
-- no Backoffice da aplicação (admin.html), que faz estes dois passos sozinho.
--
-- 1. No Supabase: Authentication > Users > Add user > Create new user
--    (email + palavra-passe, com "Auto Confirm User" ligado).
-- 2. Substitua abaixo o email, o nome e o perfil ('Professor', 'Biblioteca'
--    ou 'Técnico') e execute.
--
-- Desativar uma conta: no Backoffice (Editar > desmarcar "Conta ativa").
-- =====================================================================

insert into public.utilizadores (id, nome_utilizador, permissao_id)
select u.id,
       'Nome Apelido',
       (select t.id from public.tipos_utilizador t where t.tipo_utilizador in ('Técnico', 'Tecnico') limit 1)
from auth.users u
where u.email = lower(trim('nome@escola.pt'))
on conflict (id) do update
   set nome_utilizador = excluded.nome_utilizador,
       permissao_id    = excluded.permissao_id;

-- Confirmar:
-- select u.email, p.nome_utilizador, t.tipo_utilizador
-- from public.utilizadores p
-- join auth.users u on u.id = p.id
-- left join public.tipos_utilizador t on t.id = p.permissao_id;
