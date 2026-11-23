# API

Base: `http://localhost:5080/api`. Autenticação com `Authorization: Bearer <token>`, em que o token é o access token do **Supabase Auth** devolvido por `/auth/login` (dura cerca de 1 hora; renova-se com `/auth/refresh`). Ids em `uuid`.

## Erros

Todas as respostas de erro têm o formato `{ "message": "texto para mostrar ao utilizador" }`.

| Código | Quando |
|---|---|
| 400 | Dados inválidos (validação) |
| 401 | Sem sessão, token expirado, conta desativada ou sem perfil |
| 403 | Perfil sem permissão para a ação |
| 404 | Não existe **ou** está fora do âmbito do utilizador (não revela que existe) |
| 409 | Conflito: apagar algo em uso, número de sala, número de série ou email repetido |
| 429 | Demasiadas tentativas no login / recuperação |
| 500 / 502 / 503 | Erro inesperado ou o Supabase não respondeu |

## Autenticação — `AuthController`

| Método | Rota | Quem | Notas |
|---|---|---|---|
| POST | `/auth/login` | todos | `{ email, password }` → `{ token, refreshToken, expiraEm, user }`. 403 se a conta não tiver linha em `utilizadores` |
| POST | `/auth/refresh` | todos | `{ refreshToken }` → `{ token, refreshToken, expiraEm }` |
| GET | `/auth/me` | autenticado | `{ id, nome, email, perfil }` |
| POST | `/auth/logout` | autenticado | termina a sessão no Supabase |
| POST | `/auth/forgot-password` | todos | `{ email }`; o Supabase envia o email; responde sempre 200 |
| POST | `/auth/reset-password` | todos | `{ accessToken, novaPassword }`; o token vem no link do email (`#access_token=`) |

## Catálogos — `CatalogosController`

| Método | Rota | Quem | Notas |
|---|---|---|---|
| GET | `/catalogos` | autenticado | `{ salas: [{ nrSala, tipoSala, eBiblioteca, equipamentos }], tiposEquipamento, estados: [{ id, descricao, chave }] }` |

## Avarias — `AvariasController`

| Método | Rota | Quem | Notas |
|---|---|---|---|
| GET | `/avarias` | autenticado | filtros `q, estadoId, nrSala, tipoId` (tipo de equipamento); máx. 500 |
| GET | `/avarias/contagens` | autenticado | `[{ estado, total }]` |
| GET | `/avarias/{id}` | autenticado | detalhe com histórico |
| GET | `/avarias/{id}/foto`, `/avarias/{id}/foto-resolucao` | autenticado | mesma verificação de acesso que o detalhe |
| POST | `/avarias` | Professor, Biblioteca | `multipart/form-data`: `descricao`, `equipamentoId`, `foto` (opcional; JPG/PNG/WebP, 5 MB) |
| PATCH | `/avarias/{id}` | Técnico | `multipart/form-data`: `estadoId`, `resolucao`, `fotoResolucao` (todos opcionais); "Resolvido" exige resolução |

## Backoffice — `AdminController` (só Técnico)

| Método | Rota | Notas |
|---|---|---|
| GET | `/admin/utilizadores` | contas (nome, email, perfil, ativa) |
| POST | `/admin/utilizadores` | `{ nome, email, perfil, ativo, password }`; cria no Supabase Auth e em `utilizadores` |
| PUT | `/admin/utilizadores/{id}` | o técnico não se desativa nem muda o próprio perfil |
| POST | `/admin/utilizadores/{id}/password` | `{ novaPassword }` |
| POST | `/admin/salas` | `{ nrSala, tipoSala }` |
| PUT / DELETE | `/admin/salas/{nrSala}` | `{ tipoSala }`; o número não muda; apagar exige sala sem equipamentos |
| POST | `/admin/equipamentos` | `{ nrSala, idTipo, nrSerie, descricao }` |
| PUT / DELETE | `/admin/equipamentos/{id}` | apagar exige equipamento sem avarias |
| POST / PUT / DELETE | `/admin/tipos-equipamento[/{id}]` | `{ descricao }`; apagar exige tipo sem equipamentos |
