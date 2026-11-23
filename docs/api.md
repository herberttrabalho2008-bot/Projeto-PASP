# API

Base: `http://localhost:5080/api`. Autenticação com `Authorization: Bearer <token>` (JWT, 8 h). Enums em texto (`"Pendente"`, `"Tecnico"`…).

## Erros

Todas as respostas de erro têm o formato `{ "message": "texto para mostrar ao utilizador" }`.

| Código | Quando |
|---|---|
| 400 | Dados inválidos (validação) |
| 401 | Sem sessão, token expirado ou conta desativada |
| 403 | Perfil sem permissão para a ação |
| 404 | Não existe **ou** está fora do âmbito do utilizador (não revela que existe) |
| 409 | Conflito: apagar algo com reportes associados, nome ou email repetido |
| 429 | Demasiadas tentativas no login / recuperação (100/min por IP) |
| 500 | Erro inesperado (sem detalhes internos) |

## Autenticação — `AuthController`

| Método | Rota | Quem | Notas |
|---|---|---|---|
| POST | `/auth/login` | todos | `{ email, password }` → `{ token, user }` |
| GET | `/auth/me` | autenticado | dados da sessão |
| POST | `/auth/forgot-password` | todos | `{ email }`; responde sempre 200 |
| POST | `/auth/reset-password` | todos | `{ token, novaPassword }`; token de uso único, 1 h |

## Catálogos — `CatalogosController`

| Método | Rota | Quem | Notas |
|---|---|---|---|
| GET | `/catalogos` | autenticado | `{ locais: [{ id, nome, eBiblioteca, equipamentos }], tipos }` |

## Reportes — `ReportesController`

| Método | Rota | Quem | Notas |
|---|---|---|---|
| GET | `/reportes` | autenticado | filtros `q, estado, localId, tipoId, urgencia, prioridade`; máx. 500 |
| GET | `/reportes/contagens` | autenticado | totais por estado |
| GET | `/reportes/{id}` | autenticado | detalhe com histórico e comentários |
| GET | `/reportes/{id}/foto` | autenticado | mesma verificação de acesso que o detalhe |
| POST | `/reportes` | Professor, Biblioteca | `multipart/form-data`; foto opcional (JPG/PNG/WebP, 5 MB) |
| PATCH | `/reportes/{id}` | Técnico | `{ estado?, prioridade?, resolucao? }`; "Resolvido" exige resolução |
| POST | `/reportes/{id}/comentarios` | quem vê o reporte | `{ texto }` |

## Backoffice — `AdminController` (só Técnico)

| Método | Rota | Notas |
|---|---|---|
| GET | `/admin/utilizadores` | lista de contas |
| POST | `/admin/utilizadores` | criar (palavra-passe obrigatória) |
| PUT | `/admin/utilizadores/{id}` | editar; o técnico não se desativa nem muda o próprio perfil |
| POST | `/admin/utilizadores/{id}/password` | `{ novaPassword }` |
| POST / PUT / DELETE | `/admin/locais[/{id}]` | `{ nome, eBiblioteca }` |
| POST | `/admin/locais/{id}/equipamentos` | `{ nome }` |
| PUT / DELETE | `/admin/equipamentos/{id}` | |
| POST / PUT / DELETE | `/admin/tipos[/{id}]` | `{ nome }` |

Apagar um local, equipamento ou tipo com reportes devolve 409.
