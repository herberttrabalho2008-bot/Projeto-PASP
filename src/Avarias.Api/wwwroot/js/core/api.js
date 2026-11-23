// Cliente da API: token, pedidos e erros em português.
// Com ?demo=1 (guardado em sessionStorage, só neste separador) os pedidos vão para js/core/demo.js,
// sem servidor. Em modo demo não há segurança: as permissões reais são da API.

const TOKEN_KEY = 'avarias_token';
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

export function getToken() {
  try { return localStorage.getItem(TOKEN_KEY); } catch { return null; }
}

export function setToken(token) {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    else localStorage.removeItem(TOKEN_KEY);
  } catch { /* armazenamento bloqueado: a sessão fica só em memória */ }
}

/** Pedido JSON (ou FormData) à API. Lança ApiError com a mensagem a mostrar ao utilizador. */
export async function api(path, { method = 'GET', json, form } = {}) {
  let status;
  let data;

  if (demoAtivo) {
    const { demoRequest } = await import('./demo.js');
    ({ status, data } = await demoRequest(path, method, form ?? json));
  } else {
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
    status = res.status;
    data = status === 204 ? undefined : await res.json().catch(() => undefined);
  }

  if (status >= 400) {
    // Sessão expirada fora do login: volta ao ecrã de entrada.
    if (status === 401 && !path.startsWith('/auth/login')) {
      setToken(null);
      if (!location.pathname.endsWith('login.html')) location.replace('login.html');
    }
    throw new ApiError(data?.message ?? MENSAGENS[status] ?? 'Ocorreu um erro inesperado.', status);
  }
  return data;
}

/** Descarrega uma fotografia autenticada e devolve um URL local para <img>. */
export async function apiBlobUrl(path) {
  if (demoAtivo) throw new ApiError('A fotografia não está disponível no modo de demonstração.', 404);
  const token = getToken();
  const res = await fetch(`/api${path}`, { headers: token ? { Authorization: `Bearer ${token}` } : {} });
  if (!res.ok) throw new ApiError(MENSAGENS[res.status] ?? 'Não foi possível carregar a fotografia.', res.status);
  return URL.createObjectURL(await res.blob());
}
