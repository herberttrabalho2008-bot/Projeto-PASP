// Página repor-password.html
// O link do email do Supabase abre esta página com os dados depois do "#":
//   #access_token=...&type=recovery          → link válido
//   #error=access_denied&error_code=otp_expired&error_description=... → link inválido ou expirado
import { api, demoAtivo } from '../core/api.js';
import { mostrar } from '../core/common.js';

const $ = (id) => document.getElementById(id);
const hash = new URLSearchParams(location.hash.slice(1));
const accessToken = hash.get('access_token') ?? (demoAtivo ? 'demo' : '');

// Tira o token do endereço (não fica no histórico nem em capturas de ecrã).
if (location.hash) history.replaceState(null, '', location.pathname + location.search);

if (!accessToken || hash.get('error')) {
  if (hash.get('error_code') === 'otp_expired') $('sem-token-msg').textContent = 'O link de recuperação expirou. Peça um novo.';
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
    const r = await api('/auth/reset-password', { method: 'POST', json: { accessToken, novaPassword: pw } });
    $('aviso').textContent = r.message;
    $('form').hidden = true;
    $('feito').hidden = false;
  } catch (err) {
    mostrar($('erro'), err.message);
    $('enviar').disabled = false;
  }
});
