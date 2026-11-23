// Painel: lista de reportes com contagens, pesquisa e filtros.
// A API devolve só o que o perfil pode ver.
import { api } from '../core/api.js';
import {
  autenticar, montarCabecalho, mostrar, el, preencher, debounce,
  badgeEstado, badgeUrgencia, badgePrioridade, formatarData,
  ESTADOS, ESTADO_LABEL, URGENCIA_LABEL,
} from '../core/common.js';

const $ = (id) => document.getElementById(id);
const f = { q: '', estado: '', localId: '', tipoId: '', urgencia: '' };
const temFiltros = () => Object.values(f).some(Boolean);
let USER = null;
let pedidoAtual = 0; // só a resposta do pedido mais recente é mostrada
const LIMITE_LISTA = 500; // igual ao Take(500) de ReportesController.Listar

async function carregarContagens() {
  const c = await api('/reportes/contagens');
  $('contagens').replaceChildren(...ESTADOS.map((e) => {
    const on = f.estado === e;
    return el('button', {
      type: 'button',
      class: `count-btn${on ? ' is-on' : ''}`,
      'aria-pressed': on ? 'true' : 'false',
      onclick: () => { f.estado = on ? '' : e; $('f-estado').value = f.estado; carregarLista(); },
    }, el('span', { class: 'count-n' }, String(c[e] ?? 0)), el('span', { class: 'count-l' }, ESTADO_LABEL[e]));
  }));
}

function renderLista(lista) {
  if (!lista.length) {
    $('lista').replaceChildren(el('div', { class: 'empty' },
      el('p', {}, el('strong', {}, 'Sem reportes para mostrar')),
      el('p', { class: 'muted' }, temFiltros() ? 'Altere a pesquisa ou os filtros.' : 'Ainda não foi registada nenhuma avaria.')));
    return;
  }
  // DataTable do design system; em telemóvel o CSS empilha cada linha.
  $('lista').replaceChildren(el('div', { class: 'card' }, el('div', { class: 'bo-table-wrap' },
    el('table', { class: 'bo-table report-table' },
      el('thead', {}, el('tr', {},
        el('th', {}, 'Reporte'), el('th', {}, 'Autor'), el('th', {}, 'Data'),
        el('th', {}, 'Urgência'), el('th', {}, 'Prioridade'), el('th', {}, 'Estado'))),
      el('tbody', {}, ...lista.map((r) => el('tr', {},
        el('td', {},
          el('a', { class: 'report-link', href: `reporte.html?id=${r.id}` }, r.titulo),
          el('span', { class: 'sub' }, `${r.local}${r.equipamento ? ` · ${r.equipamento}` : ''}`)),
        el('td', {}, r.autor),
        el('td', { class: 'num' }, formatarData(r.criadoEm)),
        el('td', {}, badgeUrgencia(r.urgencia)),
        r.prioridade ? el('td', {}, badgePrioridade(r.prioridade)) : el('td', { class: 'muted-cell vazio' }, '—'),
        el('td', { class: 'col-estado' }, badgeEstado(r.estado)))))))),
  // replaceChildren escreveria "null" como texto, por isso o aviso entra como lista vazia ou de um elemento.
  ...(lista.length >= LIMITE_LISTA
    ? [el('p', { class: 'muted' }, `A mostrar os ${LIMITE_LISTA} reportes mais recentes. Use a pesquisa ou os filtros para encontrar os mais antigos.`)]
    : []));
}

async function carregarLista() {
  mostrar($('erro'), '');
  $('limpar').disabled = !temFiltros();
  $('lista-titulo').textContent = f.estado
    ? `Reportes: ${ESTADO_LABEL[f.estado]}`
    : (USER.perfil === 'Professor' ? 'Os meus reportes' : 'Todos os reportes');

  const p = new URLSearchParams();
  if (f.q.trim()) p.set('q', f.q.trim());
  for (const k of ['estado', 'localId', 'tipoId', 'urgencia']) if (f[k]) p.set(k, f[k]);

  $('lista').replaceChildren(el('p', { class: 'muted' }, 'A carregar…'));
  const n = ++pedidoAtual;
  try {
    const lista = await api(`/reportes?${p}`);
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

  preencher($('f-estado'), ESTADOS.map((e) => [e, ESTADO_LABEL[e]]));
  preencher($('f-urg'), Object.entries(URGENCIA_LABEL));
  try {
    const cat = await api('/catalogos');
    preencher($('f-local'), cat.locais.map((l) => [l.id, l.nome]));
    preencher($('f-tipo'), cat.tipos.map((t) => [t.id, t.nome]));
  } catch (e) {
    mostrar($('erro'), e.message);
  }

  $('q').addEventListener('input', debounce((e) => { f.q = e.target.value; carregarLista(); }, 300));
  $('f-estado').addEventListener('change', (e) => { f.estado = e.target.value; carregarLista(); });
  $('f-local').addEventListener('change', (e) => { f.localId = e.target.value; carregarLista(); });
  $('f-tipo').addEventListener('change', (e) => { f.tipoId = e.target.value; carregarLista(); });
  $('f-urg').addEventListener('change', (e) => { f.urgencia = e.target.value; carregarLista(); });
  $('limpar').addEventListener('click', () => {
    Object.keys(f).forEach((k) => { f[k] = ''; });
    $('q').value = '';
    ['f-estado', 'f-local', 'f-tipo', 'f-urg'].forEach((id) => { $(id).value = ''; });
    carregarLista();
  });

  carregarLista();
}

main();
