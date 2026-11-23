// Painel: lista de avarias com contagens por estado, pesquisa e filtros.
// A API devolve só o que o perfil pode ver.
import { api } from '../core/api.js';
import {
  autenticar, montarCabecalho, mostrar, el, preencher, debounce, badgeEstado, formatarData,
} from '../core/common.js';

const $ = (id) => document.getElementById(id);
const f = { q: '', estadoId: '', nrSala: '', tipoId: '' };
const temFiltros = () => Object.values(f).some(Boolean);
let USER = null;
let ESTADOS = [];
let pedidoAtual = 0; // só a resposta do pedido mais recente é mostrada
const LIMITE_LISTA = 500; // igual ao Take(500) de AvariasController.Listar

const resumir = (t, n = 90) => (t.length <= n ? t : `${t.slice(0, n - 1)}…`);

async function carregarContagens() {
  const contagens = await api('/avarias/contagens');
  $('contagens').replaceChildren(...contagens.map(({ estado, total }) => {
    const on = f.estadoId === estado.id;
    return el('button', {
      type: 'button',
      class: `count-btn${on ? ' is-on' : ''}`,
      'aria-pressed': on ? 'true' : 'false',
      onclick: () => { f.estadoId = on ? '' : estado.id; $('f-estado').value = f.estadoId; carregarLista(); },
    }, el('span', { class: 'count-n' }, String(total)), el('span', { class: 'count-l' }, estado.descricao));
  }));
}

function renderLista(lista) {
  if (!lista.length) {
    $('lista').replaceChildren(el('div', { class: 'empty' },
      el('p', {}, el('strong', {}, 'Sem avarias para mostrar')),
      el('p', { class: 'muted' }, temFiltros() ? 'Altere a pesquisa ou os filtros.' : 'Ainda não foi registada nenhuma avaria.')));
    return;
  }
  // DataTable do design system; em telemóvel o CSS empilha cada linha.
  $('lista').replaceChildren(el('div', { class: 'card' }, el('div', { class: 'bo-table-wrap' },
    el('table', { class: 'bo-table report-table' },
      el('thead', {}, el('tr', {},
        el('th', {}, 'Avaria'), el('th', {}, 'Registada por'), el('th', {}, 'Data'), el('th', {}, 'Estado'))),
      el('tbody', {}, ...lista.map((a) => el('tr', {},
        el('td', {},
          el('a', { class: 'report-link', href: `reporte.html?id=${a.id}` }, resumir(a.descricao)),
          el('span', { class: 'sub' }, [
            a.sala ? `Sala ${a.sala}` : null,
            a.equipamento,
            a.nrSerie ? `nº ${a.nrSerie}` : null,
          ].filter(Boolean).join(' · '))),
        el('td', {}, a.autor ?? '—'),
        el('td', { class: 'num' }, formatarData(a.dataAvaria)),
        el('td', { class: 'col-estado' }, badgeEstado(a.estado)))))))),
  // replaceChildren escreveria "null" como texto, por isso o aviso entra como lista vazia ou de um elemento.
  ...(lista.length >= LIMITE_LISTA
    ? [el('p', { class: 'muted' }, `A mostrar as ${LIMITE_LISTA} avarias mais recentes. Use a pesquisa ou os filtros para encontrar as mais antigas.`)]
    : []));
}

async function carregarLista() {
  mostrar($('erro'), '');
  $('limpar').disabled = !temFiltros();
  const estado = ESTADOS.find((e) => e.id === f.estadoId);
  $('lista-titulo').textContent = estado
    ? `Avarias: ${estado.descricao}`
    : (USER.perfil === 'Professor' ? 'As minhas avarias' : 'Todas as avarias');

  const p = new URLSearchParams();
  if (f.q.trim()) p.set('q', f.q.trim());
  for (const k of ['estadoId', 'nrSala', 'tipoId']) if (f[k]) p.set(k, f[k]);

  $('lista').replaceChildren(el('p', { class: 'muted' }, 'A carregar…'));
  const n = ++pedidoAtual;
  try {
    const lista = await api(`/avarias?${p}`);
    if (n !== pedidoAtual) return; // chegou depois de um pedido mais recente
    renderLista(lista);
    carregarContagens().catch(() => {});
  } catch (e) {
    if (n !== pedidoAtual) return;
    mostrar($('erro'), e.message);
    $('lista').replaceChildren();
  }
}

async function main() {
  USER = await autenticar();
  if (!USER) return;
  montarCabecalho(USER, 'painel');
  $('novo-link').hidden = USER.perfil === 'Tecnico';

  try {
    const cat = await api('/catalogos');
    ESTADOS = cat.estados;
    preencher($('f-estado'), cat.estados.map((e) => [e.id, e.descricao]));
    preencher($('f-sala'), cat.salas.map((s) => [s.nrSala, `${s.nrSala} (${s.tipoSala})`]));
    preencher($('f-tipo'), cat.tiposEquipamento.map((t) => [t.id, t.descricao]));
  } catch (e) {
    mostrar($('erro'), e.message);
  }

  $('q').addEventListener('input', debounce((e) => { f.q = e.target.value; carregarLista(); }, 300));
  $('f-estado').addEventListener('change', (e) => { f.estadoId = e.target.value; carregarLista(); });
  $('f-sala').addEventListener('change', (e) => { f.nrSala = e.target.value; carregarLista(); });
  $('f-tipo').addEventListener('change', (e) => { f.tipoId = e.target.value; carregarLista(); });
  $('limpar').addEventListener('click', () => {
    Object.keys(f).forEach((k) => { f[k] = ''; });
    $('q').value = '';
    ['f-estado', 'f-sala', 'f-tipo'].forEach((id) => { $(id).value = ''; });
    carregarLista();
  });

  carregarLista();
}

main();
