// Formulário de novo reporte (Professor e Biblioteca). Data e autor são definidos pelo servidor.
import { api } from '../core/api.js';
import { autenticar, montarCabecalho, mostrar, preencher } from '../core/common.js';

const $ = (id) => document.getElementById(id);
const MAX_FOTO = 5 * 1024 * 1024;
const TIPOS_FOTO = ['image/jpeg', 'image/png', 'image/webp'];
let CAT = null;
let previaUrl = null;

function escolherFoto(file) {
  mostrar($('erro'), '');
  if (file && file.size > MAX_FOTO) {
    $('foto').value = '';
    return mostrar($('erro'), 'A fotografia é demasiado grande (máximo 5 MB).');
  }
  if (file && !TIPOS_FOTO.includes(file.type)) {
    $('foto').value = '';
    return mostrar($('erro'), 'A fotografia deve ser uma imagem JPG, PNG ou WebP.');
  }
  if (previaUrl) URL.revokeObjectURL(previaUrl);
  previaUrl = file ? URL.createObjectURL(file) : null;
  $('previa').hidden = !file;
  $('previa').src = previaUrl ?? '';
}

async function submeter(e) {
  e.preventDefault();
  mostrar($('erro'), '');
  const titulo = $('titulo').value.trim();
  const descricao = $('descricao').value.trim();
  if (titulo.length < 3) return mostrar($('erro'), 'Indique um título (mínimo 3 caracteres).');
  if (!$('local').value) return mostrar($('erro'), 'Escolha o local da avaria.');
  if (!$('tipo').value) return mostrar($('erro'), 'Escolha o tipo de avaria.');
  if (descricao.length < 10) return mostrar($('erro'), 'Descreva o problema (mínimo 10 caracteres).');

  const fd = new FormData();
  fd.append('titulo', titulo);
  fd.append('descricao', descricao);
  fd.append('localId', $('local').value);
  if ($('equip').value) fd.append('equipamentoId', $('equip').value);
  fd.append('tipoId', $('tipo').value);
  fd.append('urgencia', $('urg').value);
  const foto = $('foto').files[0];
  if (foto) fd.append('foto', foto);

  $('enviar').disabled = true;
  $('enviar').textContent = 'A enviar…';
  try {
    const r = await api('/reportes', { method: 'POST', form: fd });
    location.replace(`reporte.html?id=${r.id}`);
  } catch (err) {
    mostrar($('erro'), err.message);
    $('enviar').disabled = false;
    $('enviar').textContent = 'Enviar reporte';
  }
}

async function main() {
  const user = await autenticar(['Professor', 'Biblioteca']);
  if (!user) return;
  montarCabecalho(user, 'novo');
  $('quem').textContent = user.nome;

  preencher($('urg'), [['Baixa', 'Baixa'], ['Media', 'Média'], ['Alta', 'Alta']], null);
  $('urg').value = 'Media';
  preencher($('equip'), [], 'Escolha primeiro o local');
  $('equip').disabled = true;

  try {
    CAT = await api('/catalogos');
    preencher($('local'), CAT.locais.map((l) => [l.id, l.nome]), 'Escolha…');
    preencher($('tipo'), CAT.tipos.map((t) => [t.id, t.nome]), 'Escolha…');
  } catch (err) {
    mostrar($('erro'), err.message);
  }

  $('local').addEventListener('change', () => {
    const loc = CAT?.locais.find((l) => String(l.id) === $('local').value);
    $('equip').disabled = !loc;
    preencher($('equip'), (loc?.equipamentos ?? []).map((q) => [q.id, q.nome]),
      loc ? 'Não especificado' : 'Escolha primeiro o local');
  });
  $('foto').addEventListener('change', () => escolherFoto($('foto').files[0] ?? null));
  $('form').addEventListener('submit', submeter);
}

main();
