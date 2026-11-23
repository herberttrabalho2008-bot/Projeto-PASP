# Permissões e segurança

## Perfis

| Ação | Professor | Biblioteca | Técnico |
|---|---|---|---|
| Registar avaria | sim | sim | não |
| Ver avarias | só as suas | todas (só leitura)* | todas |
| Mudar estado e registar resolução | não | não | sim |
| Backoffice: contas, salas, equipamentos e tipos (`admin.html`) | não | não | sim |

\* Com `"Avarias": { "BibliotecaVeTudo": false }` a Biblioteca fica limitada às salas com `tipo_sala` = "Biblioteca".

O perfil vem da base de dados (`utilizadores.permissao_id → tipos_utilizador`), lido em cada pedido. As permissões são **sempre** verificadas no servidor (`[Authorize(Roles = …)]` e filtros em `AvariasController`). O frontend só esconde botões. Uma avaria fora do âmbito responde 404. Os testes em `tests/Avarias.Api.Tests/RegrasDeAcessoTests.cs` cobrem estas regras.

## Autenticação (Supabase Auth)

- Palavras-passe guardadas e verificadas pelo Supabase Auth; a API nunca as guarda.
- O browser só fala com a API. As chaves do Supabase ficam no servidor (user-secrets / variáveis de ambiente); a chave secreta só é usada no Backoffice.
- Tokens validados pela assinatura (chaves públicas do projeto ou `Supabase:JwtSecret`), emissor e audiência.
- Cada pedido confirma que a conta tem perfil e não está bloqueada: desativar no Backoffice corta o acesso de imediato.
- O access token dura cerca de 1 hora e renova-se com o refresh token; "Sair" termina a sessão no Supabase.
- Recuperação: o Supabase envia o link; a resposta da API é igual exista ou não o email.
- Limite de 100 pedidos por minuto por IP no login, renovação e recuperação (o Supabase tem os seus próprios limites).

## Outras proteções

- Fotografias: máximo 5 MB, tipo confirmado pelos primeiros bytes (JPG, PNG, WebP), nome gerado pelo servidor, servidas só pela API com a mesma verificação de acesso.
- Frontend sem `innerHTML` (evita XSS).
- Erros sem detalhes internos.

## Riscos conhecidos

- `Trust Server Certificate=true` na ligação ao Supabase não valida o certificado.
- O token fica no `localStorage` do browser (como o cliente oficial do Supabase): um XSS poderia lê-lo, por isso o frontend nunca usa `innerHTML`.
- Modo demo (`?demo=1`) simula a API no browser e não verifica palavras-passe: serve só para ver a interface.
