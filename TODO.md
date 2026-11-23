# Por fazer

Estado a 23/11/2026. A API e o frontend já usam as tabelas do Supabase e o Supabase Auth; os segredos estão configurados neste computador (`scripts/configurar-segredos.ps1`) e a API arranca contra o Supabase sem avisos. Mais contexto em [HANDOFF.md](HANDOFF.md).

## 1. Próximo passo (no site do Supabase)

- [ ] Criar a primeira conta de técnico: **Authentication → Users → Add user → Create new user** (com *Auto Confirm User*).
- [ ] Dar-lhe o perfil: correr [database/scripts/criar-utilizador.sql](database/scripts/criar-utilizador.sql) no **SQL Editor** com o nome e o email (deve dar "Success. 1 row").
- [ ] **Authentication → URL Configuration → Redirect URLs**: acrescentar `http://localhost:5080/repor-password.html`.
- [ ] Se a palavra-passe da base de dados foi partilhada por engano, mudá-la em **Project Settings → Database → Reset database password** e voltar a correr `scripts/configurar-segredos.ps1` (só a 1.ª pergunta; Enter nas outras).

## 2. Testar ponta a ponta (com a API a correr: `scripts/correr-api.ps1`)

- [ ] Entrar em http://localhost:5080 com a conta de técnico.
- [ ] Backoffice: criar tipos de equipamento, salas (uma com tipo "Biblioteca") e equipamentos.
- [ ] Backoffice: criar uma conta de Professor e uma de Biblioteca.
- [ ] Como Professor: registar uma avaria com fotografia; confirmar que só vê as suas.
- [ ] Como Biblioteca: ver todas as avarias, sem poder mudar o estado.
- [ ] Como Técnico: passar a "Em resolução", depois a "Resolvida" com resolução e fotografia; ver o histórico.
- [ ] Desativar uma conta no Backoffice e confirmar que perde o acesso logo.
- [ ] "Esqueceu-se da palavra-passe?": confirmar que o email chega e que o link permite mudar a palavra-passe.
- [ ] Ver em `historico_avarias` (Table Editor) que cada alteração ficou registada.

## 3. Decisões em aberto

- [ ] Perfis "Administrador" e "Utilizador" em `tipos_utilizador`: a aplicação não os usa (usa Professor, Biblioteca e Tecnico). Apagar ou manter?
- [ ] Estado "Cancelada": hoje não exige justificação. Deve exigir?
- [ ] Título, urgência, prioridade e comentários foram retirados por não existirem nas tabelas. Fazem falta?

## 4. Antes de pôr em produção ([docs/deploy.md](docs/deploy.md))

- [ ] SMTP próprio no Supabase (**Authentication → Emails → SMTP Settings**); o email incluído tem limites baixos.
- [ ] Desligar o registo público em **Authentication → Sign In / Providers** ("Allow new users to sign up").
- [ ] Escolher alojamento com HTTPS e disco persistente para `uploads/` (ou passar as fotografias para o Supabase Storage).
- [ ] Configurar as variáveis de ambiente de produção e o Redirect URL com o endereço público.
- [ ] Testar num telemóvel real.
- [ ] Contraste do botão principal: trocar `#1B7FD5` por `#1769B0` para cumprir AA (design system e `styles.css`).
- [ ] Ligação com `SSL Mode=VerifyFull` e o certificado do Supabase em vez de `Trust Server Certificate=true`.

## 5. Melhorias possíveis

- [ ] Paginação no painel (hoje mostra as 500 avarias mais recentes).
- [ ] Exportar avarias para Excel/CSV.
- [ ] Notificar o técnico por email quando é registada uma avaria.
- [ ] Remover do `styles.css` as regras de urgência, prioridade e comentários, que já não se usam.
