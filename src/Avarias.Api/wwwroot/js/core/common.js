// Funções partilhadas: criação de elementos, rótulos, etiquetas, sessão e cabeçalho.
import { api, getToken, guardarSessao, terminarSessao, demoAtivo } from './api.js';

export const PERFIL_LABEL = { Professor: 'Professor', Biblioteca: 'Biblioteca', Tecnico: 'Técnico' };

// Os estados vêm da base de dados (estados_avarias). A "chave" é o nome sem acentos (ex.: "em-analise")
// e decide a cor; um estado novo sem cor própria aparece em cinzento.
const ESTADO_CLS = {
  pendente: 'estado-pendente',
  'em-analise': 'estado-analise',
  'em-resolucao': 'estado-resolucao',
  resolvida: 'estado-resolvido',
  resolvido: 'estado-resolvido',
  cancelada: 'estado-cancelado',
};

/**
 * Cria um elemento. Os textos entram como texto (textContent), nunca como HTML,
 * por isso os dados vindos da API não podem injetar código.
 */
export function el(tag, attrs = {}, ...filhos) {
  const n = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === null || v === undefined || v === false) continue;
    if (k.startsWith('on')) n.addEventListener(k.slice(2), v);
    else n.setAttribute(k, v === true ? '' : String(v));
  }
  for (const f of filhos.flat()) {
    if (f === null || f === undefined || f === false) continue;
    n.append(f instanceof Node ? f : document.createTextNode(String(f)));
  }
  return n;
}

export const badgeEstado = (e) => (e
  ? el('span', { class: `pill pill-estado ${ESTADO_CLS[e.chave] ?? 'estado-pendente'}` }, e.descricao)
  : el('span', { class: 'pill pill-estado estado-pendente' }, 'Sem estado'));

export const formatarData = (iso) => (!iso ? '—' :
  new Date(iso).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' }));

/** Preenche um <select>. Com placeholder null não há opção vazia. */
export function preencher(sel, pares, placeholder = 'Todos') {
  sel.replaceChildren(
    ...(placeholder ? [el('option', { value: '' }, placeholder)] : []),
    ...pares.map(([v, l]) => el('option', { value: v }, l)),
  );
}

/** Mostra uma mensagem numa caixa (tipo: 'erro' ou 'ok'). Mensagem vazia esconde a caixa. */
export function mostrar(caixa, msg, tipo = 'erro') {
  caixa.className = msg ? `alert alert-${tipo}` : '';
  caixa.textContent = msg ?? '';
}

export function debounce(fn, ms) {
  let t;
  return (...args) => {
    clearTimeout(t);
    t = setTimeout(() => fn(...args), ms);
  };
}

const iniciais = (nome) => {
  const partes = nome.replace(/^(Prof\.|Técnico)\s+/, '').trim().split(/\s+/);
  const ultima = partes.length > 1 ? partes[partes.length - 1][0] : '';
  return ((partes[0]?.[0] ?? '') + ultima).toUpperCase();
};

/**
 * Confirma a sessão junto da API. Sem sessão vai para o login.
 * Se o perfil não estiver em "perfis", vai para o painel.
 * A verificação real de permissões é feita pela API em cada pedido.
 */
export async function autenticar(perfis) {
  if (!getToken()) {
    location.replace('login.html');
    return null;
  }
  let user;
  try {
    user = await api('/auth/me');
  } catch {
    guardarSessao(null);
    location.replace('login.html');
    return null;
  }
  if (perfis && !perfis.includes(user.perfil)) {
    location.replace('painel.html');
    return null;
  }
  return user;
}

/** Monta o cabeçalho índigo com navegação, utilizador e "Sair". */
export function montarCabecalho(user, ativo) {
  const destinos = [
    ['painel', 'painel.html', 'Painel'],
    user.perfil === 'Tecnico' ? ['admin', 'admin.html', 'Backoffice'] : ['novo', 'novo-reporte.html', 'Registar avaria'],
  ];
  const header = el('header', { class: 'app-header on-dark' },
    el('div', { class: 'app-header-inner' },
      el('a', { class: 'app-title', href: 'painel.html' }, 'Gestão de Avarias'),
      el('nav', { class: 'app-nav', 'aria-label': 'Principal' },
        ...destinos.map(([id, href, rotulo]) =>
          el('a', { href, 'aria-current': ativo === id ? 'page' : null }, rotulo))),
      el('div', { class: 'app-user', title: `${user.nome} (${PERFIL_LABEL[user.perfil]})` },
        el('span', { class: 'avatar', 'aria-hidden': 'true' }, iniciais(user.nome)),
        el('span', { class: 'user-name-block' }, el('strong', {}, user.nome), PERFIL_LABEL[user.perfil])),
      el('button', {
        type: 'button',
        class: 'btn-logout',
        onclick: async () => { await terminarSessao(); location.replace('login.html'); },
      }, 'Sair')));
  document.body.prepend(header);
  if (demoAtivo) {
    document.body.prepend(el('div', { class: 'demo-banner' },
      'Modo de demonstração: dados de exemplo'));
  }
}
