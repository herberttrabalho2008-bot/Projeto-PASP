// Detalhe da avaria: informação, fotografias, histórico e gestão (só técnico).
import { api, apiBlobUrl } from '../core/api.js';
import {
  autenticar, montarCabecalho, mostrar, el, preencher, formatarData, badgeEstado,
} from '../core/common.js';

const $ = (id) => document.getElementById(id);
const AVARIA_ID = new URLSearchParams(location.search).get('id');
const MAX_FOTO = 5 * 1024 * 1024;
let USER = null;
let ESTADOS = [];

const info = (titulo, valor) => [el('div', {}, el('dt', {}, titulo), el('dd', {}, valor ?? '—'))];

/** Espaço para uma fotografia autenticada, carregada depois de a página aparecer. */
function fotografia(path, alt) {
  const lugar = el('p', { class: 'muted' }, 'A carregar fotografia…');
  apiBlobUrl(path)
    .then((url) => lugar.replaceWith(el('img', { class: 'photo', src: url, alt })))
    .catch(() => { lugar.textContent = 'Não foi possível carregar a fotografia.'; });
  return lugar;
}

function renderDetalhe(a) {
  const local = [a.sala ? `Sala ${a.sala}` : null, a.tipoSala].filter(Boolean).join(' · ');
  // filter(Boolean): replaceChildren escreveria "null" para as partes que não existem.
  $('detalhe').replaceChildren(...[
    el('div', { class: 'row-between' },
      el('h1', { class: 'page-title' }, a.equipamento ?? 'Avaria'),
      badgeEstado(a.estado)),
    el('dl', { class: 'detail-grid' },
      ...info('Sala', local || null),
      ...info('Equipamento', a.equipamento),
      ...info('Nº de série', a.nrSerie),
      ...info('Tipo de equipamento', a.tipoEquipamento),
      ...info('Registada por', a.autor),
      ...info('Data da avaria', formatarData(a.dataAvaria))),
    el('div', {}, el('h2', { class: 'eyebrow' }, 'Descrição'), el('p', { class: 'pre-wrap' }, a.descricao)),
    a.temFoto ? fotografia(`/avarias/${a.id}/foto`, 'Fotografia da avaria') : null,
    a.resolucao || a.temFotoResolucao ? el('div', { class: 'resolucao-box stack' }, ...[
      el('h2', { class: 'eyebrow' }, `Resolução${a.dataResolucao ? ` (${formatarData(a.dataResolucao)})` : ''}`),
      a.resolucao ? el('p', { class: 'pre-wrap' }, a.resolucao) : null,
      a.temFotoResolucao ? fotografia(`/avarias/${a.id}/foto-resolucao`, 'Fotografia da resolução') : null,
    ].filter(Boolean)) : null,
  ].filter(Boolean));
}

function renderGestao(a) {
  if (USER.perfil !== 'Tecnico') { $('gestao').hidden = true; return; }
  $('gestao').hidden = false;
  preencher($('g-estado'), ESTADOS.map((e) => [e.id, e.descricao]), null);
  $('g-estado').value = a.estado?.id ?? '';
  $('g-res').value = a.resolucao ?? '';
  $('g-foto').value = '';
}

function renderAvaria(a) {
  renderDetalhe(a);
  renderGestao(a);
  $('historico').replaceChildren(...a.historico.map((h, i, lista) =>
    el('li', { class: 'timeline-item' },
      el('div', { class: 'timeline-rail', 'aria-hidden': 'true' },
        el('span', { class: 'timeline-dot' }),
        i < lista.length - 1 ? el('span', { class: 'timeline-line' }) : null),
      el('div', { class: 'timeline-body' },
        el('p', {}, h.detalhes ?? 'Alteração'),
        el('small', { class: 'muted' },
          [h.utilizador, formatarData(h.data), h.estado ? `estado: ${h.estado}` : null].filter(Boolean).join(' · '))))));
}

async function guardarGestao(e) {
  e.preventDefault();
  mostrar($('gestao-erro'), '');
  mostrar($('gestao-ok'), '');
  const estado = ESTADOS.find((x) => x.id === $('g-estado').value);
  const resolucao = $('g-res').value.trim();
  const foto = $('g-foto').files[0];
  if (estado?.resolvido && !resolucao) {
    return mostrar($('gestao-erro'), 'Para marcar como resolvida, registe a resolução (o que foi feito).');
  }
  if (foto && foto.size > MAX_FOTO) return mostrar($('gestao-erro'), 'A fotografia é demasiado grande (máximo 5 MB).');

  const fd = new FormData();
  if (estado) fd.append('estadoId', estado.id);
  fd.append('resolucao', resolucao);
  if (foto) fd.append('fotoResolucao', foto);

  const btn = $('gravar');
  btn.disabled = true;
  btn.textContent = 'A guardar…';
  try {
    renderAvaria(await api(`/avarias/${AVARIA_ID}`, { method: 'PATCH', form: fd }));
    mostrar($('gestao-ok'), 'Alterações guardadas.', 'ok');
  } catch (err) {
    mostrar($('gestao-erro'), err.message);
  } finally {
    btn.disabled = false;
    btn.textContent = 'Guardar alterações';
  }
}

async function main() {
  USER = await autenticar();
  if (!USER) return;
  montarCabecalho(USER, 'painel');
  $('gestao-form').addEventListener('submit', guardarGestao);

  if (!AVARIA_ID) return mostrar($('erro'), 'Avaria não indicada.');
  try {
    const [avaria, cat] = await Promise.all([api(`/avarias/${AVARIA_ID}`), api('/catalogos')]);
    ESTADOS = cat.estados;
    renderAvaria(avaria);
  } catch (err) {
    $('detalhe').replaceChildren();
    mostrar($('erro'), err.message);
  }
}

main();
