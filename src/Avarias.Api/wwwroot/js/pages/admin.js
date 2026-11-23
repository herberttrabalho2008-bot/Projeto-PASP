// Backoffice (só técnico): contas, locais com equipamentos e tipos de avaria.
// As permissões reais são verificadas pela API (/api/admin exige o perfil Técnico).
// Visual: design system "Backoffice Avarias" (css/backoffice.css).
import { api } from '../core/api.js';
import { autenticar, montarCabecalho, mostrar, el, preencher, PERFIL_LABEL } from '../core/common.js';

const $ = (id) => document.getElementById(id);
let USER = null;

// ------------------------------------------------------------------ separadores
const SECOES = ['contas', 'locais', 'tipos'];
function mostrarSecao(nome) {
  for (const s of SECOES) {
    $(`tab-${s}`).setAttribute('aria-selected', String(s === nome));
    $(`tab-${s}`).tabIndex = s === nome ? 0 : -1;
    $(`sec-${s}`).hidden = s !== nome;
  }
  mostrar($('erro'), '');
  mostrar($('ok'), '');
}

// ------------------------------------------------------------------ diálogo de formulário
let aoGuardar = null;

/** Abre o diálogo com os campos dados. "guardar" recebe o form; se lançar erro, a mensagem aparece no diálogo. */
function abrirDialogo(titulo, campos, guardar, textoBotao = 'Guardar') {
  $('dlg-titulo').textContent = titulo;
  $('dlg-ok').textContent = textoBotao;
  $('dlg-campos').replaceChildren(...campos);
  mostrar($('dlg-erro'), '');
  aoGuardar = guardar;
  $('dlg').showModal();
  $('dlg-campos').querySelector('input, select')?.focus();
}

async function submeterDialogo(e) {
  e.preventDefault();
  mostrar($('dlg-erro'), '');
  const btn = $('dlg-ok');
  btn.disabled = true;
  try {
    const msg = await aoGuardar($('dlg-form'));
    $('dlg').close();
    if (msg) mostrar($('ok'), msg, 'ok');
  } catch (err) {
    mostrar($('dlg-erro'), err.message);
  } finally {
    btn.disabled = false;
  }
}

const campo = (id, rotulo, input, dica) => el('div', { class: 'field' },
  el('label', { class: 'label', for: id }, rotulo), input, dica ? el('p', { class: 'hint' }, dica) : null);
const texto = (id, valor = '', attrs = {}) => el('input', { id, name: id, class: 'input', type: 'text', value: valor, ...attrs });
const caixa = (id, rotulo, marcado) => el('label', { class: 'check' },
  el('input', { id, name: id, type: 'checkbox', checked: marcado }), rotulo);

// ------------------------------------------------------------------ confirmação (ConfirmDialog)
/** Modal de confirmação; resolve true só se a pessoa escolher a ação. O foco começa em "Cancelar". */
function confirmar(titulo, textoMsg, acao) {
  $('conf-titulo').textContent = titulo;
  $('conf-texto').textContent = textoMsg;
  $('conf-sim').textContent = acao;
  $('conf').showModal();
  $('conf-nao').focus();
  return new Promise((resolver) => {
    const fim = (v) => { $('conf').close(); resolver(v); };
    $('conf-sim').onclick = () => fim(true);
    $('conf-nao').onclick = () => fim(false);
    $('conf').oncancel = (e) => { e.preventDefault(); fim(false); }; // Esc
  });
}

/** Confirma, apaga e recarrega. Erros (ex.: 409 com reportes associados) vão para a caixa de erro. */
async function apagar(oque, nome, path, recarregar, msgOk) {
  const sim = await confirmar(`Apagar ${oque}`, `Vai apagar "${nome}". Esta ação não se pode desfazer.`, `Apagar ${oque}`);
  if (!sim) return;
  mostrar($('erro'), '');
  mostrar($('ok'), '');
  try {
    await api(path, { method: 'DELETE' });
    await recarregar();
    mostrar($('ok'), msgOk, 'ok');
  } catch (err) {
    mostrar($('erro'), err.message);
  }
}

const botao = (rotulo, classe, onclick, ariaLabel) =>
  el('button', { type: 'button', class: `bo-btn bo-btn--sm ${classe}`, onclick, 'aria-label': ariaLabel }, rotulo);
const acoesEditarApagar = (aoEditar, aoApagar, nome) => el('div', { class: 'bo-actions' },
  botao('Editar', 'bo-btn--secondary', aoEditar, `Editar ${nome}`),
  botao('Apagar', 'bo-btn--danger', aoApagar, `Apagar ${nome}`));

// ------------------------------------------------------------------ contas
function formConta(u) {
  const perfil = el('select', { id: 'f-perfil', class: 'input' });
  preencher(perfil, Object.entries(PERFIL_LABEL), null);
  perfil.value = u?.perfil ?? 'Professor';
  const proprio = u?.id === USER.id;
  if (proprio) perfil.disabled = true; // a API também impede: o técnico não se pode trancar fora

  return [
    campo('f-nome', 'Nome', texto('f-nome', u?.nome, { autocomplete: 'off' })),
    campo('f-email', 'Email', texto('f-email', u?.email, { type: 'email', autocomplete: 'off' })),
    campo('f-perfil', 'Perfil', perfil, proprio ? 'Não pode mudar o perfil da sua própria conta.' : null),
    u ? null : campo('f-pw', 'Palavra-passe inicial', texto('f-pw', '', { type: 'password', autocomplete: 'new-password' }),
      'Mínimo de 8 caracteres. Comunique-a à pessoa por um canal seguro.'),
    proprio ? null : caixa('f-ativo', 'Conta ativa (pode iniciar sessão)', u?.ativo ?? true),
  ].filter(Boolean);
}

const dadosConta = (u) => ({
  nome: $('f-nome').value.trim(),
  email: $('f-email').value.trim(),
  perfil: $('f-perfil').value,
  ativo: $('f-ativo') ? $('f-ativo').checked : true,
  password: u ? null : $('f-pw').value,
});

function validarConta(d) {
  if (d.nome.length < 2) throw new Error('Indique o nome.');
  if (!d.email.includes('@')) throw new Error('Indique um email válido.');
  if (d.password !== null && d.password.length < 8) throw new Error('A palavra-passe deve ter pelo menos 8 caracteres.');
}

const novaConta = () => abrirDialogo('Nova conta', formConta(null), async () => {
  const d = dadosConta(null);
  validarConta(d);
  await api('/admin/utilizadores', { method: 'POST', json: d });
  await carregarContas();
  return `Conta de ${d.nome} criada.`;
}, 'Criar conta');

const editarConta = (u) => abrirDialogo(`Editar conta: ${u.nome}`, formConta(u), async () => {
  const d = dadosConta(u);
  validarConta(d);
  await api(`/admin/utilizadores/${u.id}`, { method: 'PUT', json: d });
  await carregarContas();
  return 'Conta atualizada.';
});

const definirPassword = (u) => abrirDialogo(`Nova palavra-passe: ${u.nome}`, [
  campo('f-pw', 'Nova palavra-passe', texto('f-pw', '', { type: 'password', autocomplete: 'new-password' }), 'Mínimo de 8 caracteres.'),
  campo('f-pw2', 'Repita a palavra-passe', texto('f-pw2', '', { type: 'password', autocomplete: 'new-password' })),
], async () => {
  const pw = $('f-pw').value;
  if (pw.length < 8) throw new Error('A palavra-passe deve ter pelo menos 8 caracteres.');
  if (pw !== $('f-pw2').value) throw new Error('As palavras-passe não coincidem.');
  const r = await api(`/admin/utilizadores/${u.id}/password`, { method: 'POST', json: { novaPassword: pw } });
  return r.message;
}, 'Definir palavra-passe');

async function carregarContas() {
  const contas = await api('/admin/utilizadores');
  $('n-contas').textContent = String(contas.length);
  $('lista-contas').replaceChildren(...contas.map((u) => el('tr', { class: u.ativo ? null : 'is-off' },
    el('td', {}, el('strong', {}, u.nome), u.id === USER.id ? el('span', { class: 'muted-cell' }, ' (você)') : null),
    el('td', {}, u.email),
    el('td', {}, el('span', { class: 'bo-tag' }, PERFIL_LABEL[u.perfil])),
    el('td', {}, u.ativo
      ? el('span', { class: 'bo-status bo-status--success' }, 'Ativa')
      : el('span', { class: 'bo-status bo-status--neutral' }, 'Desativada')),
    el('td', {}, el('div', { class: 'bo-actions' },
      botao('Editar', 'bo-btn--secondary', () => editarConta(u), `Editar ${u.nome}`),
      botao('Palavra-passe', 'bo-btn--secondary', () => definirPassword(u), `Definir palavra-passe de ${u.nome}`))))));
}

// ------------------------------------------------------------------ locais e equipamentos
const formLocal = (l) => [
  campo('f-nome', 'Nome do local', texto('f-nome', l?.nome, { autocomplete: 'off', placeholder: 'Ex.: Sala 101' })),
  caixa('f-bib', 'É um espaço da biblioteca', l?.eBiblioteca ?? false),
];

const nomeValido = () => {
  const nome = $('f-nome').value.trim();
  if (nome.length < 2) throw new Error('Indique o nome (mínimo 2 caracteres).');
  return nome;
};

const novoLocal = () => abrirDialogo('Novo local', formLocal(null), async () => {
  await api('/admin/locais', { method: 'POST', json: { nome: nomeValido(), eBiblioteca: $('f-bib').checked } });
  await carregarCatalogos();
  return 'Local criado.';
}, 'Criar local');

const editarLocal = (l) => abrirDialogo(`Editar local: ${l.nome}`, formLocal(l), async () => {
  await api(`/admin/locais/${l.id}`, { method: 'PUT', json: { nome: nomeValido(), eBiblioteca: $('f-bib').checked } });
  await carregarCatalogos();
  return 'Local atualizado.';
});

const formNome = (rotulo, valor) => [campo('f-nome', rotulo, texto('f-nome', valor, { autocomplete: 'off' }))];

const novoEquipamento = (l) => abrirDialogo(`Novo equipamento em ${l.nome}`, formNome('Nome do equipamento'), async () => {
  await api(`/admin/locais/${l.id}/equipamentos`, { method: 'POST', json: { nome: nomeValido() } });
  await carregarCatalogos();
  return 'Equipamento adicionado.';
}, 'Adicionar');

const editarEquipamento = (q) => abrirDialogo('Editar equipamento', formNome('Nome do equipamento', q.nome), async () => {
  await api(`/admin/equipamentos/${q.id}`, { method: 'PUT', json: { nome: nomeValido() } });
  await carregarCatalogos();
  return 'Equipamento atualizado.';
});

function renderLocais(locais) {
  $('n-locais').textContent = String(locais.length);
  $('lista-locais').replaceChildren(...locais.map((l) => el('li', { class: 'bo-local' },
    el('div', { class: 'bo-local-head' },
      el('div', {},
        el('strong', {}, l.nome),
        el('small', {}, `${l.equipamentos.length} equipamento${l.equipamentos.length === 1 ? '' : 's'}`)),
      l.eBiblioteca ? el('span', { class: 'bo-tag' }, 'Biblioteca') : null,
      acoesEditarApagar(() => editarLocal(l),
        () => apagar('local', l.nome, `/admin/locais/${l.id}`, carregarCatalogos, 'Local apagado.'), l.nome)),
    el('ul', { class: 'bo-equip', 'aria-label': `Equipamentos de ${l.nome}` },
      ...l.equipamentos.map((q) => el('li', {},
        el('span', {}, q.nome),
        acoesEditarApagar(() => editarEquipamento(q),
          () => apagar('equipamento', q.nome, `/admin/equipamentos/${q.id}`, carregarCatalogos, 'Equipamento apagado.'), q.nome))),
      el('li', { class: 'bo-equip-add' },
        botao('+ Adicionar equipamento', 'bo-btn--secondary', () => novoEquipamento(l), `Adicionar equipamento em ${l.nome}`))))));
}

// ------------------------------------------------------------------ tipos de avaria
const novoTipo = () => abrirDialogo('Novo tipo de avaria', formNome('Nome do tipo'), async () => {
  await api('/admin/tipos', { method: 'POST', json: { nome: nomeValido() } });
  await carregarCatalogos();
  return 'Tipo criado.';
}, 'Criar tipo');

const editarTipo = (t) => abrirDialogo('Editar tipo de avaria', formNome('Nome do tipo', t.nome), async () => {
  await api(`/admin/tipos/${t.id}`, { method: 'PUT', json: { nome: nomeValido() } });
  await carregarCatalogos();
  return 'Tipo atualizado.';
});

function renderTipos(tipos) {
  $('n-tipos').textContent = String(tipos.length);
  $('lista-tipos').replaceChildren(...tipos.map((t) => el('tr', {},
    el('td', {}, el('strong', {}, t.nome)),
    el('td', {}, acoesEditarApagar(() => editarTipo(t),
      () => apagar('tipo', t.nome, `/admin/tipos/${t.id}`, carregarCatalogos, 'Tipo apagado.'), t.nome)))));
}

async function carregarCatalogos() {
  const cat = await api('/catalogos');
  renderLocais(cat.locais);
  renderTipos(cat.tipos);
}

// ------------------------------------------------------------------ arranque
async function main() {
  USER = await autenticar(['Tecnico']);
  if (!USER) return;
  montarCabecalho(USER, 'admin');

  SECOES.forEach((s, i) => {
    const tab = $(`tab-${s}`);
    tab.addEventListener('click', () => mostrarSecao(s));
    // Setas esquerda/direita entre separadores (padrão ARIA de tabs).
    tab.addEventListener('keydown', (e) => {
      const d = e.key === 'ArrowRight' ? 1 : e.key === 'ArrowLeft' ? -1 : 0;
      if (!d) return;
      const prox = SECOES[(i + d + SECOES.length) % SECOES.length];
      mostrarSecao(prox);
      $(`tab-${prox}`).focus();
    });
  });
  mostrarSecao('contas');

  $('nova-conta').addEventListener('click', novaConta);
  $('novo-local').addEventListener('click', novoLocal);
  $('novo-tipo').addEventListener('click', novoTipo);
  $('dlg-form').addEventListener('submit', submeterDialogo);
  $('dlg-cancelar').addEventListener('click', () => $('dlg').close());

  try {
    await Promise.all([carregarContas(), carregarCatalogos()]);
  } catch (err) {
    mostrar($('erro'), err.message);
  }
}

main();
