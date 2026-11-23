// MODO DE DEMONSTRAÇÃO: simula a API no browser, sem servidor nem base de dados.
// Os dados ficam só neste separador e voltam ao início quando ele é fechado.
// Não verifica palavras-passe nem permissões reais.

const CHAVE_SESSAO = 'avarias_demo_email';

const UTIL = {
  'professor@escola.test': { id: 1, nome: 'Prof. Ana Silva', email: 'professor@escola.test', perfil: 'Professor', ativo: true },
  'biblioteca@escola.test': { id: 2, nome: 'Biblioteca Escolar', email: 'biblioteca@escola.test', perfil: 'Biblioteca', ativo: true },
  'tecnico@escola.test': { id: 3, nome: 'Técnico Rui Costa', email: 'tecnico@escola.test', perfil: 'Tecnico', ativo: true },
};
let D = null; // dados guardados no separador (definidos mais abaixo)

const EQUIP = ['Computador', 'Projetor', 'Quadro interativo', 'Colunas de som'];
const LOCAIS = ['Sala 101', 'Sala 102', 'Sala 201', 'Laboratório de Informática', 'Sala de Professores', 'Biblioteca']
  .map((nome, i) => ({
    id: i + 1,
    nome,
    eBiblioteca: nome === 'Biblioteca',
    equipamentos: (nome === 'Biblioteca' ? ['Computador de utilização livre', 'Impressora', 'Digitalizador'] : EQUIP)
      .map((e, j) => ({ id: (i + 1) * 10 + j, nome: e })),
  }));
const TIPOS = ['Computador / informática', 'Projetor / quadro interativo', 'Rede / internet',
  'Eletricidade / iluminação', 'Mobiliário', 'Climatização', 'Outro']
  .map((nome, i) => ({ id: i + 1, nome }));

let proximoId = 100;
const horasAtras = (h) => new Date(Date.now() - h * 3600e3).toISOString();

function reporte({ id, titulo, descricao, localId, equipamento = null, tipoId, urgencia,
  prioridade = null, estado = 'Pendente', autor, horas, resolucao = null }) {
  const criadoEm = horasAtras(horas);
  const tecnico = UTIL['tecnico@escola.test'].nome;
  const historico = [{ id: proximoId++, campo: 'Criacao', valorAnterior: null, valorNovo: 'Pendente', utilizador: autor.nome, data: criadoEm }];
  if (estado !== 'Pendente') {
    historico.push({ id: proximoId++, campo: 'Estado', valorAnterior: 'Pendente', valorNovo: estado, utilizador: tecnico, data: horasAtras(horas - 1) });
  }
  if (prioridade) {
    historico.push({ id: proximoId++, campo: 'Prioridade', valorAnterior: null, valorNovo: prioridade, utilizador: tecnico, data: horasAtras(horas - 1) });
  }
  return {
    id, titulo, descricao, localId, tipoId, autorId: autor.id,
    local: (D?.locais ?? LOCAIS).find((l) => l.id === localId).nome, equipamento,
    tipo: (D?.tipos ?? TIPOS).find((t) => t.id === tipoId).nome,
    urgencia, prioridade, estado, resolucao, autor: autor.nome,
    criadoEm, atualizadoEm: horasAtras(Math.max(horas - 1, 0)),
    resolvidoEm: estado === 'Resolvido' ? horasAtras(horas - 1) : null,
    temFoto: false, historico, comentarios: [],
  };
}

const prof = UTIL['professor@escola.test'];
const bib = UTIL['biblioteca@escola.test'];
const SEMENTE = () => [
  reporte({ id: 5, titulo: 'Projetor não liga', descricao: 'O projetor não dá sinal desde a primeira aula. Já troquei o cabo e continua igual.', localId: 1, equipamento: 'Projetor', tipoId: 2, urgencia: 'Alta', autor: prof, horas: 2 }),
  reporte({ id: 4, titulo: 'Computador da biblioteca sem internet', descricao: 'O computador junto à janela não liga à rede desde ontem à tarde.', localId: 6, equipamento: 'Computador de utilização livre', tipoId: 3, urgencia: 'Media', prioridade: 'Media', estado: 'EmAnalise', autor: bib, horas: 26 }),
  reporte({ id: 3, titulo: 'Quadro interativo sem som', descricao: 'As colunas do quadro não dão som, mesmo com o volume no máximo.', localId: 3, equipamento: 'Colunas de som', tipoId: 7, urgencia: 'Baixa', prioridade: 'Alta', estado: 'EmResolucao', autor: prof, horas: 50 }),
  reporte({ id: 2, titulo: 'Impressora encrava o papel', descricao: 'A impressora encrava a cada duas ou três folhas.', localId: 6, equipamento: 'Impressora', tipoId: 7, urgencia: 'Media', autor: bib, horas: 72 }),
  reporte({ id: 1, titulo: 'Teclado com teclas presas', descricao: 'Várias teclas do teclado do computador 4 ficam presas.', localId: 4, equipamento: 'Computador', tipoId: 1, urgencia: 'Baixa', prioridade: 'Baixa', estado: 'Resolvido', autor: prof, horas: 200, resolucao: 'Teclado substituído por um novo.' }),
];

// Cada página recarrega este ficheiro, por isso os dados ficam no sessionStorage
// (duram enquanto o separador estiver aberto).
const CHAVE_DADOS = 'avarias_demo_dados';
function carregar() {
  const criadoEm = horasAtras(24 * 30);
  const base = {
    utilizadores: Object.values(UTIL).map((u) => ({ ...u, criadoEm })),
    locais: structuredClone(LOCAIS),
    tipos: structuredClone(TIPOS),
  };
  try {
    const g = JSON.parse(sessionStorage.getItem(CHAVE_DADOS));
    if (g) { proximoId = g.proximoId; return { ...base, ...g }; } // dados antigos sem contas nem catálogos ficam com os de base
  } catch { /* dados inválidos: recomeça */ }
  const reportes = SEMENTE();
  reportes[1].comentarios.push({ id: proximoId++, autor: 'Técnico Rui Costa', texto: 'Vou verificar a ligação ao intervalo.', criadoEm: horasAtras(20) });
  return { ...base, reportes };
}
D = carregar();
const REPORTES = D.reportes;
const guardar = () => {
  try { sessionStorage.setItem(CHAVE_DADOS, JSON.stringify({ ...D, proximoId })); } catch { /* ignorar */ }
};

const porEmail = (email) => D.utilizadores.find((u) => u.email === email && u.ativo) ?? null;
const atual = () => {
  try { return porEmail(sessionStorage.getItem(CHAVE_SESSAO) ?? ''); } catch { return null; }
};

// Mesma regra da API: o professor só vê os seus reportes.
const visiveis = (u) => (u.perfil === 'Professor' ? REPORTES.filter((r) => r.autorId === u.id) : REPORTES);

const resumo = (r) => ({
  id: r.id, titulo: r.titulo, local: r.local, equipamento: r.equipamento, tipo: r.tipo,
  urgencia: r.urgencia, prioridade: r.prioridade, estado: r.estado, autor: r.autor,
  criadoEm: r.criadoEm, temFoto: r.temFoto,
});

const ok = (data) => ({ status: 200, data });
const erro = (status, message) => ({ status, data: { message } });

async function responder(path, metodo, payload) {
  const [caminho, qs] = path.split('?');
  const p = new URLSearchParams(qs ?? '');
  const g = (k) => (payload instanceof FormData ? payload.get(k) : payload?.[k]);

  if (caminho === '/auth/login') {
    const u = porEmail(String(g('email') ?? '').trim().toLowerCase());
    if (!u) return erro(401, 'Email ou palavra-passe incorretos.');
    try { sessionStorage.setItem(CHAVE_SESSAO, u.email); } catch { /* ignorar */ }
    return ok({ token: 'demo', user: u });
  }
  if (caminho === '/auth/forgot-password') return ok({ message: 'Se o email existir, enviámos um link para repor a palavra-passe.' });
  if (caminho === '/auth/reset-password') return ok({ message: 'Palavra-passe alterada com sucesso. Já pode iniciar sessão.' });

  const u = atual();
  if (!u) return erro(401, 'A sua sessão terminou. Inicie sessão novamente.');
  if (caminho === '/auth/me') return ok(u);
  if (caminho === '/catalogos') return ok({ locais: D.locais, tipos: D.tipos });
  if (caminho.startsWith('/admin/')) return u.perfil === 'Tecnico' ? admin(caminho, metodo, g, u) : erro(403, 'Não tem permissão para realizar esta ação.');

  if (caminho === '/reportes/contagens') {
    const c = { Pendente: 0, EmAnalise: 0, EmResolucao: 0, Resolvido: 0 };
    visiveis(u).forEach((r) => { c[r.estado]++; });
    return ok(c);
  }

  if (caminho === '/reportes' && metodo === 'GET') {
    const q = (p.get('q') ?? '').toLowerCase();
    const lista = visiveis(u).filter((r) =>
      (!q || (r.titulo + r.descricao).toLowerCase().includes(q)) &&
      (!p.get('estado') || r.estado === p.get('estado')) &&
      (!p.get('localId') || r.localId === Number(p.get('localId'))) &&
      (!p.get('tipoId') || r.tipoId === Number(p.get('tipoId'))) &&
      (!p.get('urgencia') || r.urgencia === p.get('urgencia')));
    return ok(lista.map(resumo));
  }

  if (caminho === '/reportes' && metodo === 'POST') {
    if (u.perfil === 'Tecnico') return erro(403, 'Não tem permissão para realizar esta ação.');
    const localId = Number(g('localId'));
    const equipId = Number(g('equipamentoId') || 0);
    const equip = D.locais.find((l) => l.id === localId)?.equipamentos.find((e) => e.id === equipId)?.nome ?? null;
    const r = reporte({
      id: Math.max(...REPORTES.map((x) => x.id)) + 1,
      titulo: String(g('titulo')).trim(), descricao: String(g('descricao')).trim(),
      localId, equipamento: equip, tipoId: Number(g('tipoId')), urgencia: g('urgencia'),
      autor: u, horas: 0,
    });
    REPORTES.unshift(r);
    return { status: 201, data: { id: r.id } };
  }

  const m = caminho.match(/^\/reportes\/(\d+)(\/comentarios)?$/);
  if (m) {
    const r = visiveis(u).find((x) => x.id === Number(m[1]));
    if (!r) return erro(404, 'Reporte não encontrado.');
    const agora = new Date().toISOString();

    if (m[2]) {
      if (metodo !== 'POST') return erro(405, 'Método não permitido.');
      const c = { id: proximoId++, autor: u.nome, texto: String(g('texto') ?? '').trim(), criadoEm: agora };
      r.comentarios.push(c);
      return { status: 201, data: c };
    }

    if (metodo === 'PATCH') {
      if (u.perfil !== 'Tecnico') return erro(403, 'Não tem permissão para realizar esta ação.');
      const resolucao = String(g('resolucao') ?? '').trim();
      const novoEstado = g('estado');
      const novaPrio = g('prioridade');
      if (novoEstado === 'Resolvido' && !resolucao) {
        return erro(400, 'Para marcar como resolvido, registe a resolução (o que foi feito).');
      }
      const registar = (campo, antes, depois) =>
        r.historico.push({ id: proximoId++, campo, valorAnterior: antes, valorNovo: depois, utilizador: u.nome, data: agora });

      if (novoEstado && novoEstado !== r.estado) {
        registar('Estado', r.estado, novoEstado);
        r.estado = novoEstado;
        r.resolvidoEm = novoEstado === 'Resolvido' ? agora : null;
      }
      if (novaPrio && novaPrio !== r.prioridade) {
        registar('Prioridade', r.prioridade, novaPrio);
        r.prioridade = novaPrio;
      }
      if (resolucao !== (r.resolucao ?? '')) {
        registar('Resolucao', r.resolucao, resolucao || null);
        r.resolucao = resolucao || null;
      }
      r.atualizadoEm = agora;
    }
    return ok(r);
  }

  return erro(404, 'Não foi encontrado o que procura.');
}

// Backoffice: mesmas regras do AdminController.
const semConteudo = { status: 204, data: undefined };
const nome = (g) => String(g('nome') ?? '').trim();

function admin(caminho, metodo, g, eu) {
  const [, , recurso, idTxt, sub] = caminho.split('/'); // '', 'admin', recurso, id, sub
  const id = Number(idTxt);

  if (recurso === 'utilizadores') {
    if (!idTxt && metodo === 'GET') return ok([...D.utilizadores].sort((a, b) => a.nome.localeCompare(b.nome)));
    const email = String(g('email') ?? '').trim().toLowerCase();
    if (!idTxt && metodo === 'POST') {
      if (D.utilizadores.some((u) => u.email === email)) return erro(409, 'Já existe uma conta com este email.');
      const u = { id: proximoId++, nome: nome(g), email, perfil: g('perfil'), ativo: g('ativo') !== false, criadoEm: new Date().toISOString() };
      D.utilizadores.push(u);
      return { status: 201, data: u };
    }
    const u = D.utilizadores.find((x) => x.id === id);
    if (!u) return erro(404, 'Conta não encontrada.');
    if (sub === 'password') return ok({ message: 'Palavra-passe definida. Comunique-a à pessoa por um canal seguro.' });
    if (metodo === 'PUT') {
      if (id === eu.id && (!g('ativo') || g('perfil') !== 'Tecnico')) {
        return erro(400, 'Não pode desativar a sua própria conta nem tirar-lhe o perfil de técnico.');
      }
      if (email !== u.email && D.utilizadores.some((x) => x.email === email)) return erro(409, 'Já existe uma conta com este email.');
      Object.assign(u, { nome: nome(g), email, perfil: g('perfil'), ativo: !!g('ativo') });
      return ok(u);
    }
  }

  if (recurso === 'locais') {
    if (!idTxt && metodo === 'POST') {
      if (D.locais.some((l) => l.nome === nome(g))) return erro(409, 'Já existe um local com este nome.');
      const l = { id: proximoId++, nome: nome(g), eBiblioteca: !!g('eBiblioteca'), equipamentos: [] };
      D.locais.push(l);
      D.locais.sort((a, b) => a.nome.localeCompare(b.nome));
      return { status: 201, data: { id: l.id } };
    }
    const l = D.locais.find((x) => x.id === id);
    if (!l) return erro(404, 'Local não encontrado.');
    if (sub === 'equipamentos' && metodo === 'POST') {
      l.equipamentos.push({ id: proximoId++, nome: nome(g) });
      return { status: 201, data: {} };
    }
    if (metodo === 'PUT') {
      if (nome(g) !== l.nome && D.locais.some((x) => x.nome === nome(g))) return erro(409, 'Já existe um local com este nome.');
      Object.assign(l, { nome: nome(g), eBiblioteca: !!g('eBiblioteca') });
      return semConteudo;
    }
    if (metodo === 'DELETE') {
      if (REPORTES.some((r) => r.localId === id)) return erro(409, 'Este local tem reportes associados e não pode ser apagado. Pode mudar-lhe o nome.');
      D.locais.splice(D.locais.indexOf(l), 1);
      return semConteudo;
    }
  }

  if (recurso === 'equipamentos') {
    const l = D.locais.find((x) => x.equipamentos.some((q) => q.id === id));
    const q = l?.equipamentos.find((x) => x.id === id);
    if (!q) return erro(404, 'Equipamento não encontrado.');
    if (metodo === 'PUT') { q.nome = nome(g); return semConteudo; }
    if (metodo === 'DELETE') {
      if (REPORTES.some((r) => r.localId === l.id && r.equipamento === q.nome)) {
        return erro(409, 'Este equipamento tem reportes associados e não pode ser apagado. Pode mudar-lhe o nome.');
      }
      l.equipamentos.splice(l.equipamentos.indexOf(q), 1);
      return semConteudo;
    }
  }

  if (recurso === 'tipos') {
    if (!idTxt && metodo === 'POST') {
      if (D.tipos.some((t) => t.nome === nome(g))) return erro(409, 'Já existe um tipo com este nome.');
      D.tipos.push({ id: proximoId++, nome: nome(g) });
      D.tipos.sort((a, b) => a.nome.localeCompare(b.nome));
      return { status: 201, data: {} };
    }
    const t = D.tipos.find((x) => x.id === id);
    if (!t) return erro(404, 'Tipo não encontrado.');
    if (metodo === 'PUT') {
      if (nome(g) !== t.nome && D.tipos.some((x) => x.nome === nome(g))) return erro(409, 'Já existe um tipo com este nome.');
      t.nome = nome(g);
      return semConteudo;
    }
    if (metodo === 'DELETE') {
      if (REPORTES.some((r) => r.tipoId === id)) return erro(409, 'Este tipo tem reportes associados e não pode ser apagado. Pode mudar-lhe o nome.');
      D.tipos.splice(D.tipos.indexOf(t), 1);
      return semConteudo;
    }
  }

  return erro(404, 'Não foi encontrado o que procura.');
}

/** Responde a um pedido como a API faria, com um pequeno atraso de rede. */
export async function demoRequest(path, metodo, payload) {
  await new Promise((r) => setTimeout(r, 150));
  try {
    const r = await responder(path, metodo, payload);
    if (metodo !== 'GET') guardar();
    return r;
  } catch {
    return erro(500, 'Ocorreu um erro no servidor. Tente novamente dentro de instantes.');
  }
}
