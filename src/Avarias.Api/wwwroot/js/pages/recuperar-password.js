// Página recuperar-password.html
import { api } from '../core/api.js';
import { mostrar } from '../core/common.js';

const $ = (id) => document.getElementById(id);

$('form').addEventListener('submit', async (e) => {
  e.preventDefault();
  mostrar($('aviso'), '');
  const email = $('email').value.trim();
  if (!email) return mostrar($('aviso'), 'Indique o seu email.');

  $('enviar').disabled = true;
  try {
    const r = await api('/auth/forgot-password', { method: 'POST', json: { email } });
    mostrar($('aviso'), r.message, 'ok');
  } catch (err) {
    mostrar($('aviso'), err.message);
  } finally {
    $('enviar').disabled = false;
  }
});
