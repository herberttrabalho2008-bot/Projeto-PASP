// MODO DE DEMONSTRAÇÃO: simula a API no browser, sem servidor nem base de dados.
// Os dados ficam só neste separador e voltam ao início quando ele é fechado.
// Não verifica palavras-passe nem permissões reais.

const CHAVE_SESSAO = 'avarias_demo_email';
const CHAVE_DADOS = 'avarias_demo_dados_v2';

let seq = 1000;
const novoId = () => (globalThis.crypto?.randomUUID?.() ?? `demo-${seq++}`);
const horasAtras = (h) => new Date(Date.now() - h * 3600e3).toISOString();
const chave = (t) => (t ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim().replace(/[^a-z0-9]+/g, '-');

const ok = (data) => ({ status: 200, data });
const criado = (data) => ({ status: 201, data });
const semConteudo = { status: 204, data: undefined };
const erro = (status, message) => ({ status, data: { message } });
const proibido = () => erro(403, 'Não tem permissão para realizar esta ação.');

// ------------------------------------------------------------------ dados iniciais
function semente() {
  const estados = ['Pendente', 'Em análise', 'Em resolução', 'Resolvido'].map((d, i) => ({ id: `est-${i + 1}`, descricao: d }));
  const tipos = ['Computador', 'Projetor', 'Quadro interativo', 'Impressora', 'Rede'].map((d, i) => ({ id: `tipo-${i + 1}`, descricao: d }));
  const salas = [
    { nrSala: '101', tipoSala: 'Sala de aula' },
    { nrSala: '102', tipoSala: 'Sala de aula' },
    { nrSala: 'B1.05', tipoSala: 'Laboratório de Informática' },
    { nrSala: 'BIB', tipoSala: 'Biblioteca' },
  ];
  const eq = (nrSala, idTipo, descricao, nrSerie) => ({ id: novoId(), nrSala, idTipo, descricao, nrSerie });
  const equipamentos = [
    eq('101', 'tipo-2', 'Projetor do quadro', 'EP-1001'),
    eq('101', 'tipo-1', 'Computador do professor', 'PC-1011'),
    eq('102', 'tipo-3', 'Quadro interativo', 'QI-1020'),
    eq('B1.05', 'tipo-1', 'Computador 4', 'PC-1054'),
    eq('B1.05', 'tipo-5', 'Switch da sala', null),
    eq('BIB', 'tipo-1', 'Computador de utilização livre', 'PC-BIB1'),
    eq('BIB', 'tipo-4', 'Impressora', 'IMP-BIB'),
  ];
  const utilizadores = [
    { id: 'u-prof', nome: 'Prof. Ana Silva', email: 'professor@escola.test', perfil: 'Professor', ativo: true, criadoEm: horasAtras(720) },
    { id: 'u-bib', nome: 'Biblioteca Escolar', email: 'biblioteca@escola.test', perfil: 'Biblioteca', ativo: true, criadoEm: horasAtras(720) },
    { id: 'u-tec', nome: 'Técnico Rui Costa', email: 'tecnico@escola.test', perfil: 'Tecnico', ativo: true, criadoEm: horasAtras(720) },
  ];

  const avarias = [];
  const avaria = ({ equip, autor, horas, descricao, estado = 'est-1', resolucao = null }) => {
    const a = {
      id: novoId(), descricao, idEquipamento: equipamentos[equip].id, autorId: autor, dataAvaria: horasAtras(horas),
      estadoId: estado, resolucao, dataResolucao: estado === 'est-4' ? horasAtras(horas - 2) : null, historico: [],
    };
    a.historico.push({ id: novoId(), detalhes: 'Avaria registada', estado: 'Pendente', utilizadorId: autor, data: a.dataAvaria });
    if (estado !== 'est-1') {
      const d = estados.find((e) => e.id === estado).descricao;
      a.historico.push({ id: novoId(), detalhes: `Estado: Pendente → ${d}${resolucao ? '; Resolução registada' : ''}`, estado: d, utilizadorId: 'u-tec', data: horasAtras(horas - 2) });
    }
    avarias.push(a);
  };
  avaria({ equip: 0, autor: 'u-prof', horas: 2, descricao: 'O projetor não dá sinal desde a primeira aula. Já troquei o cabo e continua igual.' });
  avaria({ equip: 5, autor: 'u-bib', horas: 26, estado: 'est-2', descricao: 'O computador junto à janela não liga à rede desde ontem à tarde.' });
  avaria({ equip: 2, autor: 'u-prof', horas: 50, estado: 'est-3', descricao: 'O quadro interativo não reconhece o toque na parte de baixo.' });
  avaria({ equip: 6, autor: 'u-bib', horas: 72, descricao: 'A impressora encrava o papel a cada duas ou três folhas.' });
  avaria({ equip: 3, autor: 'u-prof', horas: 200, estado: 'est-4', descricao: 'Várias teclas do teclado ficam presas.', resolucao: 'Teclado substituído por um novo.' });

  return { estados, tipos, salas, equipamentos, utilizadores, avarias };
}

// Cada página recarrega este ficheiro, por isso os dados ficam no sessionStorage.
function carregar() {
  try {
    const g = JSON.parse(sessionStorage.getItem(CHAVE_DADOS));
    if (g) return g;
  } catch { /* dados inválidos: recomeça */ }
  return semente();
}
const D = carregar();
const guardar = () => {
  try { sessionStorage.setItem(CHAVE_DADOS, JSON.stringify(D)); } catch { /* ignorar */ }
};

// ------------------------------------------------------------------ conversões (mesmo formato da API)
const estadoDto = (id) => {
  const e = D.estados.find((x) => x.id === id);
  return e ? { id: e.id, descricao: e.descricao, chave: chave(e.descricao) } : null;
};
const tipoDe = (id) => D.tipos.find((t) => t.id === id)?.descricao ?? null;
const equipDto = (q) => ({ id: q.id, descricao: q.descricao, nrSerie: q.nrSerie, idTipo: q.idTipo, tipo: tipoDe(q.idTipo) });
const nomeDe = (id) => D.utilizadores.find((u) => u.id === id)?.nome ?? null;

const catalogos = () => ({
  salas: [...D.salas].sort((a, b) => a.nrSala.localeCompare(b.nrSala)).map((s) => ({
    ...s,
    eBiblioteca: chave(s.tipoSala) === 'biblioteca',
    equipamentos: D.equipamentos.filter((q) => q.nrSala === s.nrSala).map(equipDto),
  })),
  tiposEquipamento: [...D.tipos].sort((a, b) => a.descricao.localeCompare(b.descricao)),
  estados: D.estados.map((e) => estadoDto(e.id)),
});

function detalhe(a) {
  const q = D.equipamentos.find((x) => x.id === a.idEquipamento);
  const sala = D.salas.find((s) => s.nrSala === q?.nrSala);
  return {
    id: a.id, descricao: a.descricao, sala: q?.nrSala ?? null, tipoSala: sala?.tipoSala ?? null,
    equipamentoId: q?.id ?? null, equipamento: q?.descricao ?? null, nrSerie: q?.nrSerie ?? null,
    tipoEquipamento: tipoDe(q?.idTipo), estado: estadoDto(a.estadoId), autor: nomeDe(a.autorId),
    dataAvaria: a.dataAvaria, temFoto: false, dataResolucao: a.dataResolucao, resolucao: a.resolucao,
    temFotoResolucao: false,
    historico: a.historico.map((h) => ({ id: h.id, detalhes: h.detalhes, estado: h.estado, estadoChave: chave(h.estado), utilizador: nomeDe(h.utilizadorId), data: h.data })),
  };
}

const resumo = (a) => {
  const d = detalhe(a);
  return {
    id: d.id, descricao: d.descricao, sala: d.sala, equipamento: d.equipamento, nrSerie: d.nrSerie,
    tipoEquipamento: d.tipoEquipamento, estado: d.estado, autor: d.autor, dataAvaria: d.dataAvaria, temFoto: false,
  };
};

// ------------------------------------------------------------------ sessão
const atual = () => {
  try {
    const email = sessionStorage.getItem(CHAVE_SESSAO) ?? '';
    return D.utilizadores.find((u) => u.email === email && u.ativo) ?? null;
  } catch { return null; }
};
const userInfo = (u) => ({ id: u.id, nome: u.nome, email: u.email, perfil: u.perfil });

// Mesma regra da API: o professor só vê as suas avarias.
const visiveis = (u) => (u.perfil === 'Professor' ? D.avarias.filter((a) => a.autorId === u.id) : D.avarias);

// ------------------------------------------------------------------ pedidos
function responder(path, metodo, payload) {
  const [caminho, qs] = path.split('?');
  const p = new URLSearchParams(qs ?? '');
  const g = (k) => (payload instanceof FormData ? payload.get(k) : payload?.[k]);

  if (caminho === '/auth/login') {
    const u = D.utilizadores.find((x) => x.email === String(g('email') ?? '').trim().toLowerCase());
    if (!u) return erro(401, 'Email ou palavra-passe incorretos.');
    if (!u.ativo) return erro(403, 'Esta conta está desativada. Contacte os serviços da escola.');
    try { sessionStorage.setItem(CHAVE_SESSAO, u.email); } catch { /* ignorar */ }
    return ok({ token: 'demo', refreshToken: 'demo', expiraEm: horasAtras(-1), user: userInfo(u) });
  }
  if (caminho === '/auth/refresh') return ok({ token: 'demo', refreshToken: 'demo', expiraEm: horasAtras(-1) });
  if (caminho === '/auth/forgot-password') return ok({ message: 'Se o email existir, enviámos um link para repor a palavra-passe.' });
  if (caminho === '/auth/reset-password') return ok({ message: 'Palavra-passe alterada com sucesso. Já pode iniciar sessão.' });

  const u = atual();
  if (!u) return erro(401, 'A sua sessão terminou. Inicie sessão novamente.');
  if (caminho === '/auth/me') return ok(userInfo(u));
  if (caminho === '/auth/logout') { try { sessionStorage.removeItem(CHAVE_SESSAO); } catch { /* ignorar */ } return semConteudo; }
  if (caminho === '/catalogos') return ok(catalogos());
  if (caminho.startsWith('/admin/')) return u.perfil === 'Tecnico' ? admin(caminho, metodo, g, u) : proibido();

  if (caminho === '/avarias/contagens') {
    return ok(D.estados.map((e) => ({ estado: estadoDto(e.id), total: visiveis(u).filter((a) => a.estadoId === e.id).length })));
  }

  if (caminho === '/avarias' && metodo === 'GET') {
    const q = (p.get('q') ?? '').toLowerCase();
    const lista = visiveis(u).map(resumo).filter((a) =>
      (!q || [a.descricao, a.equipamento, a.nrSerie].some((t) => t?.toLowerCase().includes(q))) &&
      (!p.get('estadoId') || a.estado?.id === p.get('estadoId')) &&
      (!p.get('nrSala') || a.sala === p.get('nrSala')) &&
      (!p.get('tipoId') || D.equipamentos.find((x) => x.descricao === a.equipamento && x.nrSala === a.sala)?.idTipo === p.get('tipoId')));
    return ok(lista.sort((a, b) => b.dataAvaria.localeCompare(a.dataAvaria)));
  }

  if (caminho === '/avarias' && metodo === 'POST') {
    if (u.perfil === 'Tecnico') return proibido();
    const q = D.equipamentos.find((x) => x.id === g('equipamentoId'));
    if (!q) return erro(400, 'O equipamento escolhido não existe.');
    const agora = new Date().toISOString();
    const a = {
      id: novoId(), descricao: String(g('descricao')).trim(), idEquipamento: q.id, autorId: u.id, dataAvaria: agora,
      estadoId: D.estados[0].id, resolucao: null, dataResolucao: null,
      historico: [{ id: novoId(), detalhes: 'Avaria registada', estado: D.estados[0].descricao, utilizadorId: u.id, data: agora }],
    };
    D.avarias.unshift(a);
    return criado({ id: a.id });
  }

  const m = caminho.match(/^\/avarias\/([^/]+)$/);
  if (m) {
    const a = visiveis(u).find((x) => x.id === m[1]);
    if (!a) return erro(404, 'Avaria não encontrada.');

    if (metodo === 'PATCH') {
      if (u.perfil !== 'Tecnico') return proibido();
      const agora = new Date().toISOString();
      const mudancas = [];
      const novoEstado = g('estadoId');
      const resolucao = String(g('resolucao') ?? '').trim();
      if (novoEstado && novoEstado !== a.estadoId) {
        const e = estadoDto(novoEstado);
        mudancas.push(`Estado: ${estadoDto(a.estadoId)?.descricao ?? '—'} → ${e.descricao}`);
        a.estadoId = novoEstado;
        a.dataResolucao = e.chave === 'resolvido' ? agora : null;
      }
      if (resolucao !== (a.resolucao ?? '')) {
        mudancas.push(a.resolucao ? 'Resolução atualizada' : 'Resolução registada');
        a.resolucao = resolucao || null;
      }
      if (estadoDto(a.estadoId)?.chave === 'resolvido' && !a.resolucao) {
        return erro(400, 'Para marcar como resolvida, registe a resolução (o que foi feito).');
      }
      if (mudancas.length) {
        a.historico.push({ id: novoId(), detalhes: mudancas.join('; '), estado: estadoDto(a.estadoId)?.descricao, utilizadorId: u.id, data: agora });
      }
    }
    return ok(detalhe(a));
  }

  return erro(404, 'Não foi encontrado o que procura.');
}

// ------------------------------------------------------------------ backoffice (mesmas regras do AdminController)
function admin(caminho, metodo, g, eu) {
  const [, , recurso, idTxt, sub] = caminho.split('/'); // '', 'admin', recurso, id, sub
  const id = idTxt ? decodeURIComponent(idTxt) : null;
  const txt = (k) => String(g(k) ?? '').trim();

  if (recurso === 'utilizadores') {
    if (!id && metodo === 'GET') return ok([...D.utilizadores].sort((a, b) => a.nome.localeCompare(b.nome)));
    const email = txt('email').toLowerCase();
    if (!id && metodo === 'POST') {
      if (D.utilizadores.some((u) => u.email === email)) return erro(409, 'Já existe uma conta com este email.');
      const u = { id: novoId(), nome: txt('nome'), email, perfil: g('perfil'), ativo: g('ativo') !== false, criadoEm: new Date().toISOString() };
      D.utilizadores.push(u);
      return criado(u);
    }
    const u = D.utilizadores.find((x) => x.id === id);
    if (!u) return erro(404, 'Conta não encontrada.');
    if (sub === 'password') return ok({ message: 'Palavra-passe definida. Comunique-a à pessoa por um canal seguro.' });
    if (metodo === 'PUT') {
      if (id === eu.id && (!g('ativo') || g('perfil') !== 'Tecnico')) {
        return erro(400, 'Não pode desativar a sua própria conta nem tirar-lhe o perfil de técnico.');
      }
      if (email !== u.email && D.utilizadores.some((x) => x.email === email)) return erro(409, 'Já existe uma conta com este email.');
      Object.assign(u, { nome: txt('nome'), email, perfil: g('perfil'), ativo: !!g('ativo') });
      return ok(u);
    }
  }

  if (recurso === 'salas') {
    if (!id && metodo === 'POST') {
      if (D.salas.some((s) => s.nrSala === txt('nrSala'))) return erro(409, 'Já existe uma sala com este número.');
      D.salas.push({ nrSala: txt('nrSala'), tipoSala: txt('tipoSala') });
      return criado({ nrSala: txt('nrSala') });
    }
    const s = D.salas.find((x) => x.nrSala === id);
    if (!s) return erro(404, 'Sala não encontrada.');
    if (metodo === 'PUT') { s.tipoSala = txt('tipoSala'); return semConteudo; }
    if (metodo === 'DELETE') {
      if (D.equipamentos.some((q) => q.nrSala === id)) return erro(409, 'Esta sala tem equipamentos. Apague-os ou mude-os de sala primeiro.');
      D.salas.splice(D.salas.indexOf(s), 1);
      return semConteudo;
    }
  }

  if (recurso === 'equipamentos') {
    const dados = () => ({ nrSala: txt('nrSala'), idTipo: g('idTipo') || null, nrSerie: txt('nrSerie') || null, descricao: txt('descricao') });
    const serieRepetida = (d, outroId) => d.nrSerie && D.equipamentos.some((q) => q.nrSerie === d.nrSerie && q.id !== outroId);
    if (!id && metodo === 'POST') {
      const d = dados();
      if (serieRepetida(d)) return erro(409, 'Já existe um equipamento com este número de série.');
      const q = { id: novoId(), ...d };
      D.equipamentos.push(q);
      return criado({ id: q.id });
    }
    const q = D.equipamentos.find((x) => x.id === id);
    if (!q) return erro(404, 'Equipamento não encontrado.');
    if (metodo === 'PUT') {
      const d = dados();
      if (serieRepetida(d, q.id)) return erro(409, 'Já existe um equipamento com este número de série.');
      Object.assign(q, d);
      return semConteudo;
    }
    if (metodo === 'DELETE') {
      if (D.avarias.some((a) => a.idEquipamento === id)) {
        return erro(409, 'Este equipamento tem avarias registadas e não pode ser apagado. Pode mudar-lhe a descrição.');
      }
      D.equipamentos.splice(D.equipamentos.indexOf(q), 1);
      return semConteudo;
    }
  }

  if (recurso === 'tipos-equipamento') {
    if (!id && metodo === 'POST') {
      if (D.tipos.some((t) => t.descricao === txt('descricao'))) return erro(409, 'Já existe um tipo com esta descrição.');
      const t = { id: novoId(), descricao: txt('descricao') };
      D.tipos.push(t);
      return criado({ id: t.id });
    }
    const t = D.tipos.find((x) => x.id === id);
    if (!t) return erro(404, 'Tipo não encontrado.');
    if (metodo === 'PUT') {
      if (txt('descricao') !== t.descricao && D.tipos.some((x) => x.descricao === txt('descricao'))) return erro(409, 'Já existe um tipo com esta descrição.');
      t.descricao = txt('descricao');
      return semConteudo;
    }
    if (metodo === 'DELETE') {
      if (D.equipamentos.some((q) => q.idTipo === id)) return erro(409, 'Há equipamentos deste tipo, por isso não pode ser apagado. Pode mudar-lhe a descrição.');
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
    const r = responder(path, metodo, payload);
    if (metodo !== 'GET') guardar();
    return r;
  } catch {
    return erro(500, 'Ocorreu um erro no servidor. Tente novamente dentro de instantes.');
  }
}
