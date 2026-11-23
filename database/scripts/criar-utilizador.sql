-- =====================================================================
-- Criar uma conta (executar no SQL Editor do Supabase).
--
-- Substitua o nome, o email, a palavra-passe inicial e o perfil
-- ('Professor', 'Biblioteca' ou 'Tecnico'). A palavra-passe é gravada
-- com hash bcrypt (pgcrypto, ativo por defeito no Supabase), compatível
-- com a API. Peça à pessoa que a mude em "Esqueceu-se da palavra-passe?".
--
-- Desativar uma conta (perde o acesso de imediato):
--   update utilizadores set ativo = false where email = 'nome@escola.pt';
-- =====================================================================

insert into utilizadores (nome, email, password_hash, perfil, ativo, criado_em)
values (
    'Nome Apelido',
    lower(trim('nome@escola.pt')),
    extensions.crypt('PalavraPasseInicial123!', extensions.gen_salt('bf', 11)),
    'Professor',
    true,
    now()
);
