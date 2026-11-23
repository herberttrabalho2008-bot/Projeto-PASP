// Página repor-password.html
import { api } from '../core/api.js';
import { mostrar } from '../core/common.js';

const $ = (id) => document.getElementById(id);
const token = new URLSearchParams(location.search).get('token') ?? '';

if (!token) {
  $('sem-token').hidden = false;
} else {
  $('form').hidden = false;
}

$('form').addEventListener('submit', async (e) => {
  e.preventDefault();
  mostrar($('erro'), '');
  const pw = $('pw').value;
  if (pw.length < 8) return mostrar($('erro'), 'A palavra-passe deve ter pelo menos 8 caracteres.');
  if (pw !== $('pw2').value) return mostrar($('erro'), 'As palavras-passe não coincidem.');

  $('enviar').disabled = true;
  try {
    const r = await api('/auth/reset-password', { method: 'POST', json: { token, novaPassword: pw } });
    $('aviso').textContent = r.message;
    $('form').hidden = true;
    $('feito').hidden = false;
  } catch (err) {
    mostrar($('erro'), err.message);
    $('enviar').disabled = false;
  }
});
