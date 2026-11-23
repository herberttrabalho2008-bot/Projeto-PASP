# Pôr em produção

## 1. Base de dados (Supabase)

1. Criar um projeto em supabase.com.
2. Em *Project Settings > Database* copiar os dados de ligação (usar **Session pooler** se a ligação direta não funcionar na rede da escola).
3. As tabelas são criadas no primeiro arranque da API.

## 2. Configuração (nunca no repositório)

Em produção, por variáveis de ambiente (`__` separa as secções):

| Variável | Exemplo |
|---|---|
| `ConnectionStrings__Default` | `Host=…;Port=5432;Database=postgres;Username=postgres;Password=…;SSL Mode=Require` |
| `Jwt__Key` | chave aleatória com 32+ caracteres |
| `Smtp__Host`, `Smtp__Port`, `Smtp__Utilizador`, `Smtp__Password`, `Smtp__De` | servidor de email da escola |
| `Frontend__BaseUrl` | `https://endereco-publico-da-app` (usado nos links de recuperação) |
| `Uploads__Pasta` | pasta persistente para as fotos |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

Sem `Smtp:Host` os emails de recuperação só aparecem no log.

## 3. Publicar

```powershell
dotnet publish src/Avarias.Api -c Release -o publish
```

A pasta `publish/` contém a API e o frontend. Correr com `dotnet Avarias.Api.dll` atrás de um proxy HTTPS (IIS, nginx) ou num serviço como Azure App Service / Render.

## 4. Primeira conta de técnico

No *SQL Editor* do Supabase, correr `database/scripts/criar-utilizador.sql` com o perfil `Tecnico`. As restantes contas criam-se no Backoffice.

## Lista de verificação

- [ ] `SeedUtilizadoresTeste` é `false` (por omissão, fora de Development)
- [ ] Não existem as contas `@escola.test` na base de dados
- [ ] `Jwt:Key` forte e só no servidor
- [ ] HTTPS ativo
- [ ] Pasta de uploads persistente e com cópia de segurança
- [ ] Email de recuperação testado de ponta a ponta
- [ ] Testado num telemóvel real
