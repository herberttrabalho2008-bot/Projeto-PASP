// Cliente da API: sessão, pedidos e erros em português.
// A sessão é do Supabase Auth: o token dura cerca de 1 hora e é renovado sozinho com o refresh token.
// Com ?demo=1 (guardado em sessionStorage, só neste separador) os pedidos vão para js/core/demo.js,
// sem servidor. Em modo demo não há segurança: as permissões reais são da API.

const TOKEN_KEY = 'avarias_token';
const REFRESH_KEY = 'avarias_refresh';
const DEMO_KEY = 'avarias_demo';

export const demoAtivo = (() => {
  try {
    const p = new URLSearchParams(location.search);
    if (p.get('demo') === '1') sessionStorage.setItem(DEMO_KEY, '1');
    if (p.get('demo') === '0') sessionStorage.removeItem(DEMO_KEY);
    return sessionStorage.getItem(DEMO_KEY) === '1';
  } catch {
    return false;
  }
})();

const MENSAGENS = {
  401: 'A sua sessão terminou. Inicie sessão novamente.',
  403: 'Não tem permissão para realizar esta ação.',
  404: 'Não foi encontrado o que procura.',
  429: 'Demasiadas tentativas. Aguarde um minuto e tente de novo.',
  500: 'Ocorreu um erro no servidor. Tente novamente dentro de instantes.',
  // Servidor estático sem API (ex.: python -m http.server) responde 501 aos POST.
  501: 'A API não está a correr neste endereço. Para ver a aplicação sem API, abra login.html?demo=1.',
};

export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

const ler = (k) => { try { return localStorage.getItem(k); } catch { return null; } };
const escrever = (k, v) => {
  try {
    if (v) localStorage.setItem(k, v);
    else localStorage.removeItem(k);
  } catch { /* armazenamento bloqueado: a sessão fica só nesta página */ }
};

export const getToken = () => ler(TOKEN_KEY);

/** Guarda a sessão devolvida por /auth/login ou /auth/refresh. Sem argumento, termina a sessão local. */
export function guardarSessao(s) {
  escrever(TOKEN_KEY, s?.token ?? null);
  escrever(REFRESH_KEY, s?.refreshToken ?? null);
}

/** Termina a sessão no Supabase (melhor esforço) e localmente. */
export async function terminarSessao() {
  if (!demoAtivo && getToken()) {
    await fetch('/api/auth/logout', { method: 'POST', headers: { Authorization: `Bearer ${getToken()}` } }).catch(() => {});
  }
  guardarSessao(null);
}

// Uma só renovação de cada vez, mesmo com vários pedidos a falhar ao mesmo tempo.
let renovacao = null;
async function renovarSessao() {
  const refreshToken = ler(REFRESH_KEY);
  if (!refreshToken) return false;
  renovacao ??= (async () => {
    try {
      const res = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
      if (!res.ok) return false;
      guardarSessao(await res.json());
      return true;
    } catch {
      return false;
    } finally {
      setTimeout(() => { renovacao = null; }, 0);
    }
  })();
  return renovacao;
}

async function pedir(path, method, json, form) {
  const headers = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  let body;
  if (form) body = form;
  else if (json !== undefined) {
    headers['Content-Type'] = 'application/json';
    body = JSON.stringify(json);
  }

  let res;
  try {
    res = await fetch(`/api${path}`, { method, headers, body });
  } catch {
    throw new ApiError('Não foi possível contactar o servidor. Verifique a ligação e tente novamente.', 0);
  }
  const data = res.status === 204 ? undefined : await res.json().catch(() => undefined);
  return { status: res.status, data };
}

const semRenovacao = (path) => path.startsWith('/auth/login') || path.startsWith('/auth/refresh') || path.startsWith('/auth/reset');

/** Pedido JSON (ou FormData) à API. Lança ApiError com a mensagem a mostrar ao utilizador. */
export async function api(path, { method = 'GET', json, form } = {}) {
  let status;
  let data;

  if (demoAtivo) {
    const { demoRequest } = await import('./demo.js');
    ({ status, data } = await demoRequest(path, method, form ?? json));
  } else {
    ({ status, data } = await pedir(path, method, json, form));
    // Token expirado: renova a sessão e repete o pedido uma vez.
    if (status === 401 && !semRenovacao(path) && await renovarSessao()) {
      ({ status, data } = await pedir(path, method, json, form));
    }
  }

  if (status >= 400) {
    // Sessão terminada fora do login: volta ao ecrã de entrada.
    if (status === 401 && !semRenovacao(path)) {
      guardarSessao(null);
      if (!location.pathname.endsWith('login.html')) location.replace('login.html');
    }
    throw new ApiError(data?.message ?? MENSAGENS[status] ?? 'Ocorreu um erro inesperado.', status);
  }
  return data;
}

/** Descarrega uma fotografia autenticada e devolve um URL local para <img>. */
export async function apiBlobUrl(path) {
  if (demoAtivo) throw new ApiError('A fotografia não está disponível no modo de demonstração.', 404);
  const obter = () => fetch(`/api${path}`, { headers: getToken() ? { Authorization: `Bearer ${getToken()}` } : {} });
  let res = await obter();
  if (res.status === 401 && await renovarSessao()) res = await obter();
  if (!res.ok) throw new ApiError(MENSAGENS[res.status] ?? 'Não foi possível carregar a fotografia.', res.status);
  return URL.createObjectURL(await res.blob());
}
