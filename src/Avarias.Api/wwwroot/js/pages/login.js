// Página login.html
import { api, getToken, guardarSessao } from '../core/api.js';
import { mostrar } from '../core/common.js';

const $ = (id) => document.getElementById(id);

// Já com sessão válida: vai direto para o painel.
if (getToken()) {
  try { await api('/auth/me'); location.replace('painel.html'); }
  catch { guardarSessao(null); }
}

$('form').addEventListener('submit', async (e) => {
  e.preventDefault();
  mostrar($('erro'), '');
  const email = $('email').value.trim();
  const password = $('password').value;
  if (!email || !password) return mostrar($('erro'), 'Indique o email e a palavra-passe.');

  $('entrar').disabled = true;
  $('entrar').textContent = 'A entrar…';
  try {
    const r = await api('/auth/login', { method: 'POST', json: { email, password } });
    guardarSessao(r);
    location.replace('painel.html');
  } catch (err) {
    mostrar($('erro'), err.message);
    $('entrar').disabled = false;
    $('entrar').textContent = 'Entrar';
  }
});
