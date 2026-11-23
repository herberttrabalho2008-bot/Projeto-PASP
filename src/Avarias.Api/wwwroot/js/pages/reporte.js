// Detalhe do reporte: informação, histórico, comentários e gestão (só técnico).
import { api, apiBlobUrl } from '../core/api.js';
import {
  autenticar, montarCabecalho, mostrar, el, preencher, formatarData,
  badgeEstado, badgeUrgencia, badgePrioridade,
  ESTADOS, ESTADO_LABEL, PRIORIDADES, PRIORIDADE_LABEL,
} from '../core/common.js';

const $ = (id) => document.getElementById(id);
const REPORTE_ID = new URLSearchParams(location.search).get('id');
let USER = null;

function descreverHistorico(h) {
  const est = (v) => (v ? (ESTADO_LABEL[v] ?? v) : '—');
  const pri = (v) => (v ? (PRIORIDADE_LABEL[v] ?? v) : 'sem prioridade');
  switch (h.campo) {
    case 'Criacao': return 'Reporte criado (Pendente)';
    case 'Estado': return `Estado: ${est(h.valorAnterior)} → ${est(h.valorNovo)}`;
    case 'Prioridade': return `Prioridade: ${pri(h.valorAnterior)} → ${pri(h.valorNovo)}`;
    case 'Resolucao': return h.valorAnterior ? 'Resolução atualizada' : 'Resolução registada';
    default: return h.campo;
  }
}

const info = (titulo, valor) => [el('div', {}, el('dt', {}, titulo), el('dd', {}, valor))];

function renderDetalhe(r) {
  const foto = el('p', { class: 'muted' }, 'A carregar fotografia…');
  if (r.temFoto) {
    apiBlobUrl(`/reportes/${r.id}/foto`)
      .then((url) => foto.replaceWith(el('img', { class: 'photo', src: url, alt: `Fotografia da avaria: ${r.titulo}` })))
      .catch(() => { foto.textContent = 'Não foi possível carregar a fotografia.'; });
  }

  // filter(Boolean): replaceChildren escreveria "null" para as partes que não existem.
  $('detalhe').replaceChildren(...[
    el('div', { class: 'row-between' }, el('h1', { class: 'page-title' }, r.titulo), badgeEstado(r.estado)),
    el('div', { class: 'tags' }, badgeUrgencia(r.urgencia), r.prioridade ? badgePrioridade(r.prioridade) : null),
    el('dl', { class: 'detail-grid' },
      ...info('Local', r.local),
      ...info('Equipamento', r.equipamento ?? 'Não especificado'),
      ...info('Tipo de avaria', r.tipo),
      ...info('Reportado por', r.autor),
      ...info('Data do reporte', formatarData(r.criadoEm)),
      ...info('Última atualização', formatarData(r.atualizadoEm))),
    el('div', {}, el('h2', { class: 'eyebrow' }, 'Descrição'), el('p', { class: 'pre-wrap' }, r.descricao)),
    r.temFoto ? foto : null,
    r.resolucao ? el('div', { class: 'resolucao-box' },
      el('h2', { class: 'eyebrow' }, `Resolução${r.resolvidoEm ? ` (${formatarData(r.resolvidoEm)})` : ''}`),
      el('p', { class: 'pre-wrap' }, r.resolucao)) : null,
  ].filter(Boolean));
}

function renderGestao(r) {
  if (USER.perfil !== 'Tecnico') { $('gestao').hidden = true; return; }
  $('gestao').hidden = false;
  preencher($('g-estado'), ESTADOS.map((e) => [e, ESTADO_LABEL[e]]), null);
  $('g-estado').value = r.estado;
  preencher($('g-prio'), PRIORIDADES.map((p) => [p, PRIORIDADE_LABEL[p]]), 'Por definir');
  $('g-prio').value = r.prioridade ?? '';
  $('g-res').value = r.resolucao ?? '';
}

const comentarioEl = (c) => el('li', { class: 'comment' },
  el('p', {}, c.texto),
  el('small', {}, `${c.autor} · ${formatarData(c.criadoEm)}`));

function renderReporte(r) {
  renderDetalhe(r);
  renderGestao(r);
  $('historico').replaceChildren(...r.historico.map((h, i, lista) =>
    el('li', { class: 'timeline-item' },
      el('div', { class: 'timeline-rail', 'aria-hidden': 'true' },
        el('span', { class: 'timeline-dot' }),
        i < lista.length - 1 ? el('span', { class: 'timeline-line' }) : null),
      el('div', { class: 'timeline-body' },
        el('p', {}, descreverHistorico(h)),
        el('small', { class: 'muted' }, `${h.utilizador} · ${formatarData(h.data)}`)))));
  $('comentarios').replaceChildren(...r.comentarios.map(comentarioEl));
  $('sem-coment').hidden = r.comentarios.length > 0;
}

async function guardarGestao(e) {
  e.preventDefault();
  mostrar($('gestao-erro'), '');
  mostrar($('gestao-ok'), '');
  const estado = $('g-estado').value;
  const resolucao = $('g-res').value.trim();
  if (estado === 'Resolvido' && !resolucao) {
    return mostrar($('gestao-erro'), 'Para marcar como resolvido, registe a resolução (o que foi feito).');
  }
  const btn = $('gravar');
  btn.disabled = true;
  btn.textContent = 'A guardar…';
  try {
    const r = await api(`/reportes/${REPORTE_ID}`, {
      method: 'PATCH',
      json: { estado, prioridade: $('g-prio').value || null, resolucao },
    });
    renderReporte(r);
    mostrar($('gestao-ok'), 'Alterações guardadas.', 'ok');
  } catch (err) {
    mostrar($('gestao-erro'), err.message);
  } finally {
    btn.disabled = false;
    btn.textContent = 'Guardar alterações';
  }
}

async function comentar(e) {
  e.preventDefault();
  mostrar($('coment-erro'), '');
  const texto = $('novo-coment').value.trim();
  if (!texto) return mostrar($('coment-erro'), 'Escreva o comentário.');
  const btn = $('enviar-coment');
  btn.disabled = true;
  try {
    const c = await api(`/reportes/${REPORTE_ID}/comentarios`, { method: 'POST', json: { texto } });
    const novo = comentarioEl(c);
    novo.classList.add('is-new'); // só os comentários acabados de enviar animam a entrada
    $('comentarios').append(novo);
    $('sem-coment').hidden = true;
    $('novo-coment').value = '';
  } catch (err) {
    mostrar($('coment-erro'), err.message);
  } finally {
    btn.disabled = false;
  }
}

async function main() {
  USER = await autenticar();
  if (!USER) return;
  montarCabecalho(USER, 'painel');
  $('gestao-form').addEventListener('submit', guardarGestao);
  $('form-coment').addEventListener('submit', comentar);

  if (!REPORTE_ID) return mostrar($('erro'), 'Reporte não indicado.');
  try {
    renderReporte(await api(`/reportes/${REPORTE_ID}`));
  } catch (err) {
    $('detalhe').replaceChildren();
    mostrar($('erro'), err.message);
  }
}

main();
