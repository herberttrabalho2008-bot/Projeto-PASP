// Backoffice (só técnico): contas, salas com equipamentos e tipos de equipamento.
// As permissões reais são verificadas pela API (/api/admin exige o perfil Técnico).
// Visual: design system "Backoffice Avarias" (css/backoffice.css).
import { api } from '../core/api.js';
import { autenticar, montarCabecalho, mostrar, el, preencher, PERFIL_LABEL } from '../core/common.js';

const $ = (id) => document.getElementById(id);
let USER = null;

// ------------------------------------------------------------------ separadores
const SECOES = ['contas', 'salas', 'tipos'];
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

/** Confirma, apaga e recarrega. Erros (ex.: 409 com avarias associadas) vão para a caixa de erro. */
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
    el('td', {}, u.email ?? '—'),
    el('td', {}, u.perfil ? el('span', { class: 'bo-tag' }, PERFIL_LABEL[u.perfil]) : el('span', { class: 'muted-cell' }, 'Sem perfil')),
    el('td', {}, u.ativo
      ? el('span', { class: 'bo-status bo-status--success' }, 'Ativa')
      : el('span', { class: 'bo-status bo-status--neutral' }, 'Desativada')),
    el('td', {}, el('div', { class: 'bo-actions' },
      botao('Editar', 'bo-btn--secondary', () => editarConta(u), `Editar ${u.nome}`),
      botao('Palavra-passe', 'bo-btn--secondary', () => definirPassword(u), `Definir palavra-passe de ${u.nome}`))))));
}

// ------------------------------------------------------------------ salas e equipamentos
let CAT = { salas: [], tiposEquipamento: [] };

const exigir = (id, rotulo, min = 2) => {
  const v = $(id).value.trim();
  if (v.length < min) throw new Error(`Indique ${rotulo}.`);
  return v;
};

const formSala = (s) => [
  s ? null : campo('f-nr', 'Número ou nome da sala', texto('f-nr', '', { autocomplete: 'off', placeholder: 'Ex.: 101 ou B2.04' }),
    'Não se pode mudar depois de criada.'),
  campo('f-tipo', 'Tipo de sala', texto('f-tipo', s?.tipoSala, { autocomplete: 'off', placeholder: 'Ex.: Sala de aula, Laboratório, Biblioteca' }),
    'Use "Biblioteca" para os espaços da biblioteca.'),
].filter(Boolean);

const novaSala = () => abrirDialogo('Nova sala', formSala(null), async () => {
  await api('/admin/salas', { method: 'POST', json: { nrSala: exigir('f-nr', 'o número da sala', 1), tipoSala: exigir('f-tipo', 'o tipo de sala') } });
  await carregarCatalogos();
  return 'Sala criada.';
}, 'Criar sala');

const editarSala = (s) => abrirDialogo(`Editar sala ${s.nrSala}`, formSala(s), async () => {
  await api(`/admin/salas/${encodeURIComponent(s.nrSala)}`, { method: 'PUT', json: { tipoSala: exigir('f-tipo', 'o tipo de sala') } });
  await carregarCatalogos();
  return 'Sala atualizada.';
});

function formEquipamento(q, nrSala) {
  const sala = el('select', { id: 'f-sala', class: 'input' });
  preencher(sala, CAT.salas.map((s) => [s.nrSala, `${s.nrSala} (${s.tipoSala})`]), null);
  sala.value = nrSala;
  const tipo = el('select', { id: 'f-tipoeq', class: 'input' });
  preencher(tipo, CAT.tiposEquipamento.map((t) => [t.id, t.descricao]), 'Sem tipo');
  tipo.value = q?.idTipo ?? '';
  return [
    campo('f-desc', 'Descrição', texto('f-desc', q?.descricao, { autocomplete: 'off', placeholder: 'Ex.: Projetor Epson do quadro' })),
    campo('f-tipoeq', 'Tipo de equipamento', tipo),
    campo('f-serie', 'Número de série (opcional)', texto('f-serie', q?.nrSerie, { autocomplete: 'off' })),
    campo('f-sala', 'Sala', sala),
  ];
}

const dadosEquipamento = () => ({
  descricao: exigir('f-desc', 'a descrição do equipamento'),
  idTipo: $('f-tipoeq').value || null,
  nrSerie: $('f-serie').value.trim() || null,
  nrSala: $('f-sala').value,
});

const novoEquipamento = (s) => abrirDialogo(`Novo equipamento na sala ${s.nrSala}`, formEquipamento(null, s.nrSala), async () => {
  await api('/admin/equipamentos', { method: 'POST', json: dadosEquipamento() });
  await carregarCatalogos();
  return 'Equipamento adicionado.';
}, 'Adicionar');

const editarEquipamento = (q, s) => abrirDialogo('Editar equipamento', formEquipamento(q, s.nrSala), async () => {
  await api(`/admin/equipamentos/${q.id}`, { method: 'PUT', json: dadosEquipamento() });
  await carregarCatalogos();
  return 'Equipamento atualizado.';
});

const rotuloEquip = (q) => [q.descricao, q.tipo, q.nrSerie ? `nº ${q.nrSerie}` : null].filter(Boolean).join(' · ');

function renderSalas(salas) {
  $('n-salas').textContent = String(salas.length);
  $('lista-salas').replaceChildren(...salas.map((s) => el('li', { class: 'bo-local' },
    el('div', { class: 'bo-local-head' },
      el('div', {},
        el('strong', {}, `Sala ${s.nrSala}`),
        el('small', {}, `${s.tipoSala} · ${s.equipamentos.length} equipamento${s.equipamentos.length === 1 ? '' : 's'}`)),
      s.eBiblioteca ? el('span', { class: 'bo-tag' }, 'Biblioteca') : null,
      acoesEditarApagar(() => editarSala(s),
        () => apagar('sala', s.nrSala, `/admin/salas/${encodeURIComponent(s.nrSala)}`, carregarCatalogos, 'Sala apagada.'), `sala ${s.nrSala}`)),
    el('ul', { class: 'bo-equip', 'aria-label': `Equipamentos da sala ${s.nrSala}` },
      ...s.equipamentos.map((q) => el('li', {},
        el('span', {}, rotuloEquip(q)),
        acoesEditarApagar(() => editarEquipamento(q, s),
          () => apagar('equipamento', q.descricao, `/admin/equipamentos/${q.id}`, carregarCatalogos, 'Equipamento apagado.'), q.descricao))),
      el('li', { class: 'bo-equip-add' },
        botao('+ Adicionar equipamento', 'bo-btn--secondary', () => novoEquipamento(s), `Adicionar equipamento na sala ${s.nrSala}`))))));
}

// ------------------------------------------------------------------ tipos de equipamento
const formTipo = (valor) => [campo('f-desc', 'Descrição', texto('f-desc', valor, { autocomplete: 'off', placeholder: 'Ex.: Projetor' }))];

const novoTipo = () => abrirDialogo('Novo tipo de equipamento', formTipo(), async () => {
  await api('/admin/tipos-equipamento', { method: 'POST', json: { descricao: exigir('f-desc', 'a descrição') } });
  await carregarCatalogos();
  return 'Tipo criado.';
}, 'Criar tipo');

const editarTipo = (t) => abrirDialogo('Editar tipo de equipamento', formTipo(t.descricao), async () => {
  await api(`/admin/tipos-equipamento/${t.id}`, { method: 'PUT', json: { descricao: exigir('f-desc', 'a descrição') } });
  await carregarCatalogos();
  return 'Tipo atualizado.';
});

function renderTipos(tipos) {
  $('n-tipos').textContent = String(tipos.length);
  $('lista-tipos').replaceChildren(...tipos.map((t) => el('tr', {},
    el('td', {}, el('strong', {}, t.descricao)),
    el('td', {}, acoesEditarApagar(() => editarTipo(t),
      () => apagar('tipo', t.descricao, `/admin/tipos-equipamento/${t.id}`, carregarCatalogos, 'Tipo apagado.'), t.descricao)))));
}

async function carregarCatalogos() {
  CAT = await api('/catalogos');
  renderSalas(CAT.salas);
  renderTipos(CAT.tiposEquipamento);
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
  $('nova-sala').addEventListener('click', novaSala);
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
