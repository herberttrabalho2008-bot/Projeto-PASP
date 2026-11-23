# Permissões e segurança

## Perfis

| Ação | Professor | Biblioteca | Técnico |
|---|---|---|---|
| Criar reporte | sim | sim | não |
| Ver reportes | só os seus | todos (só leitura)* | todos |
| Comentar | sim (nos que vê) | sim | sim |
| Alterar estado, prioridade e resolução | não | não | sim |
| Backoffice: contas, locais, equipamentos e tipos (`admin.html`) | não | não | sim |

\* Com `"Reportes": { "BibliotecaVeTudo": false }` a Biblioteca fica limitada aos locais marcados como biblioteca.

As permissões são **sempre** verificadas no servidor (`[Authorize(Roles = …)]` e filtros nas consultas em `ReportesController`). O frontend só esconde botões. Um reporte fora do âmbito responde 404. Os testes em `tests/Avarias.Api.Tests/RegrasDeAcessoTests.cs` cobrem estas regras.

Estados de um reporte: Pendente → Em análise → Em resolução → Resolvido (exige resolução escrita). Cada alteração fica no histórico.

## Proteções

- Palavras-passe com **bcrypt** (fator 11); sessão com **JWT** de 8 h.
- Cada pedido confirma que a conta ainda está ativa (desativar corta o acesso de imediato).
- Mensagem de login igual para email inexistente e palavra-passe errada, com tempo de resposta semelhante.
- Limite de 100 pedidos por minuto por IP no login e na recuperação.
- Recuperação: token guardado só como hash SHA-256, expira em 1 hora, uso único.
- Fotos: máximo 5 MB, tipo confirmado pelos primeiros bytes (JPG, PNG, WebP), nome gerado pelo servidor, servidas só através da API.
- Frontend sem `innerHTML` (evita XSS).
- Sem `Jwt:Key` com 32+ caracteres a API recusa arrancar.
- Segredos fora do repositório (`dotnet user-secrets` / variáveis de ambiente).

## Riscos conhecidos

- `Trust Server Certificate=true` na ligação ao Supabase não valida o certificado. Melhor: `SSL Mode=VerifyFull` com o certificado do Supabase.
- Modo demo (`?demo=1`) simula a API no browser e não verifica palavras-passe: serve só para ver a interface.
