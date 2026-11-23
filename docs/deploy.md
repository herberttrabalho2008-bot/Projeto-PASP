# Pôr em produção

## 1. Supabase

1. As tabelas já existem (ver [base-de-dados.md](base-de-dados.md)).
2. **Authentication → URL Configuration**: em *Site URL* e *Redirect URLs* pôr o endereço público com `/repor-password.html` (ex.: `https://avarias.escola.pt/repor-password.html`).
3. **Authentication → Emails → SMTP Settings**: configurar o servidor de email da escola (o serviço incluído tem limites baixos).
4. **Authentication → Sign In / Providers**: desligar "Allow new users to sign up" (as contas criam-se no Backoffice).

## 2. Configuração (nunca no repositório)

Variáveis de ambiente (`__` separa as secções):

| Variável | Valor |
|---|---|
| `ConnectionStrings__Default` | ligação Session pooler (formato .NET) |
| `Supabase__Url` | `https://<projeto>.supabase.co` |
| `Supabase__AnonKey` | chave pública (publishable / anon) |
| `Supabase__ServiceRoleKey` | chave secreta (secret / service_role) |
| `Frontend__BaseUrl` | endereço público da aplicação |
| `Uploads__Pasta` | pasta persistente para as fotografias |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

## 3. Publicar

```powershell
dotnet publish src/Avarias.Api -c Release -o publish
```

A pasta `publish/` contém a API e o frontend. Correr com `dotnet Avarias.Api.dll` atrás de um proxy HTTPS (IIS, nginx) ou num serviço como Azure App Service / Render (com disco persistente para `uploads/`).

## 4. Primeira conta de técnico

No Supabase, **Authentication → Users → Add user** (com "Auto Confirm User"), e depois `database/scripts/criar-utilizador.sql` no SQL Editor com o perfil `Técnico`. As restantes contas criam-se no Backoffice.

## Lista de verificação

- [ ] Redirect URL de recuperação configurado no Supabase
- [ ] SMTP próprio no Supabase e email de recuperação testado
- [ ] Registo público ("sign up") desligado no Supabase
- [ ] Chave secreta só no servidor
- [ ] HTTPS ativo
- [ ] Pasta de uploads persistente e com cópia de segurança
- [ ] Testado num telemóvel real
