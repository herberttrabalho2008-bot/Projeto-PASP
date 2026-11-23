// Registar avaria (Professor e Biblioteca): sala → equipamento, descrição e fotografia.
// A data, o estado inicial (Pendente) e quem regista são definidos pelo servidor.
import { api } from '../core/api.js';
import { autenticar, montarCabecalho, mostrar, preencher } from '../core/common.js';

const $ = (id) => document.getElementById(id);
const MAX_FOTO = 5 * 1024 * 1024;
const TIPOS_FOTO = ['image/jpeg', 'image/png', 'image/webp'];
let CAT = null;
let previaUrl = null;

const salaAtual = () => CAT?.salas.find((s) => s.nrSala === $('sala').value);
const rotuloEquip = (q) => (q.nrSerie ? `${q.descricao} (nº ${q.nrSerie})` : q.descricao);

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

function escolherSala() {
  const sala = salaAtual();
  const equipamentos = sala?.equipamentos ?? [];
  $('equip').disabled = !equipamentos.length;
  preencher($('equip'), equipamentos.map((q) => [q.id, rotuloEquip(q)]),
    !sala ? 'Escolha primeiro a sala' : (equipamentos.length ? 'Escolha…' : 'Esta sala não tem equipamentos'));
  mostrarTipo();
}

function mostrarTipo() {
  const q = salaAtual()?.equipamentos.find((x) => x.id === $('equip').value);
  $('equip-info').hidden = !q?.tipo;
  $('equip-info').textContent = q?.tipo ? `Tipo: ${q.tipo}` : '';
}

async function submeter(e) {
  e.preventDefault();
  mostrar($('erro'), '');
  const descricao = $('descricao').value.trim();
  if (!$('sala').value) return mostrar($('erro'), 'Escolha a sala.');
  if (!$('equip').value) return mostrar($('erro'), 'Escolha o equipamento avariado.');
  if (descricao.length < 10) return mostrar($('erro'), 'Descreva a avaria (mínimo 10 caracteres).');

  const fd = new FormData();
  fd.append('descricao', descricao);
  fd.append('equipamentoId', $('equip').value);
  const foto = $('foto').files[0];
  if (foto) fd.append('foto', foto);

  $('enviar').disabled = true;
  $('enviar').textContent = 'A registar…';
  try {
    const r = await api('/avarias', { method: 'POST', form: fd });
    location.replace(`reporte.html?id=${r.id}`);
  } catch (err) {
    mostrar($('erro'), err.message);
    $('enviar').disabled = false;
    $('enviar').textContent = 'Registar avaria';
  }
}

async function main() {
  const user = await autenticar(['Professor', 'Biblioteca']);
  if (!user) return;
  montarCabecalho(user, 'novo');
  $('quem').textContent = user.nome;

  try {
    CAT = await api('/catalogos');
    preencher($('sala'), CAT.salas.map((s) => [s.nrSala, `${s.nrSala} (${s.tipoSala})`]), 'Escolha…');
  } catch (err) {
    mostrar($('erro'), err.message);
  }
  escolherSala();

  $('sala').addEventListener('change', escolherSala);
  $('equip').addEventListener('change', mostrarTipo);
  $('foto').addEventListener('change', () => escolherFoto($('foto').files[0] ?? null));
  $('form').addEventListener('submit', submeter);
}

main();
