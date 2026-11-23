using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Avarias.Api.Services.Supabase;

/// <summary>
/// Cliente HTTP da API REST do Supabase Auth (GoTrue): {Supabase:Url}/auth/v1/...
/// Configuração: Supabase:Url, Supabase:AnonKey (chave pública) e Supabase:ServiceRoleKey (chave secreta).
/// </summary>
public class SupabaseAuthClient : ISupabaseAuth
{
    private readonly HttpClient _http;
    private readonly string _anonKey;
    private readonly string? _serviceKey;
    private readonly ILogger<SupabaseAuthClient> _log;

    public SupabaseAuthClient(HttpClient http, IConfiguration cfg, ILogger<SupabaseAuthClient> log)
    {
        _http = http;
        _log = log;
        _http.BaseAddress = new Uri(SupabaseConfig.Url(cfg) + "/auth/v1/");
        _anonKey = cfg["Supabase:AnonKey"] ?? "";
        _serviceKey = cfg["Supabase:ServiceRoleKey"];
    }

    // ------------------------------------------------------------------ sessão
    public Task<SessaoSupabase> EntrarAsync(string email, string password) =>
        PedirSessaoAsync("token?grant_type=password", new { email, password });

    public Task<SessaoSupabase> RenovarAsync(string refreshToken) =>
        PedirSessaoAsync("token?grant_type=refresh_token", new { refresh_token = refreshToken });

    public async Task PedirRecuperacaoAsync(string email, string redirectTo)
    {
        var url = "recover?redirect_to=" + Uri.EscapeDataString(redirectTo);
        using var res = await EnviarAsync(HttpMethod.Post, url, new { email }, _anonKey);
        // Não revela se o email existe: só regista os erros.
        if (!res.IsSuccessStatusCode)
            _log.LogWarning("Supabase /recover respondeu {Status}: {Corpo}", (int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    public async Task AlterarPasswordAsync(string accessToken, string novaPassword)
    {
        using var res = await EnviarAsync(HttpMethod.Put, "user", new { password = novaPassword }, _anonKey, accessToken);
        await GarantirSucessoAsync(res);
    }

    public async Task TerminarSessaoAsync(string accessToken)
    {
        using var res = await EnviarAsync(HttpMethod.Post, "logout", null, _anonKey, accessToken);
    }

    // ------------------------------------------------------------------ administração
    public async Task<Guid> CriarContaAsync(string email, string password, string nome)
    {
        using var res = await EnviarAsync(HttpMethod.Post, "admin/users", new
        {
            email,
            password,
            email_confirm = true,
            user_metadata = new { nome },
        }, ChaveServico());
        await GarantirSucessoAsync(res);
        var json = await res.Content.ReadFromJsonAsync<JsonObject>();
        return Guid.Parse(json!["id"]!.GetValue<string>());
    }

    public async Task AtualizarContaAsync(Guid id, string? email = null, string? password = null, bool? ativa = null)
    {
        var corpo = new JsonObject();
        if (email is not null) { corpo["email"] = email; corpo["email_confirm"] = true; }
        if (password is not null) corpo["password"] = password;
        // Desativar = bloquear (ban) durante 100 anos; reativar = "none".
        if (ativa is not null) corpo["ban_duration"] = ativa.Value ? "none" : "876000h";
        if (corpo.Count == 0) return;

        using var res = await EnviarAsync(HttpMethod.Put, $"admin/users/{id}", corpo, ChaveServico());
        await GarantirSucessoAsync(res);
    }

    public async Task ApagarContaAsync(Guid id)
    {
        using var res = await EnviarAsync(HttpMethod.Delete, $"admin/users/{id}", null, ChaveServico());
        await GarantirSucessoAsync(res);
    }

    // ------------------------------------------------------------------ auxiliares
    private string ChaveServico() =>
        string.IsNullOrWhiteSpace(_serviceKey)
            ? throw new SupabaseAuthException(500, "Falta configurar Supabase:ServiceRoleKey para gerir contas.")
            : _serviceKey;

    private async Task<SessaoSupabase> PedirSessaoAsync(string url, object corpo)
    {
        using var res = await EnviarAsync(HttpMethod.Post, url, corpo, _anonKey);
        await GarantirSucessoAsync(res);
        var j = (await res.Content.ReadFromJsonAsync<JsonObject>())!;
        var expiraEm = j["expires_at"] is { } exp
            ? DateTimeOffset.FromUnixTimeSeconds(exp.GetValue<long>()).UtcDateTime
            : DateTime.UtcNow.AddSeconds(j["expires_in"]?.GetValue<int>() ?? 3600);
        return new SessaoSupabase(
            j["access_token"]!.GetValue<string>(),
            j["refresh_token"]!.GetValue<string>(),
            expiraEm,
            Guid.Parse(j["user"]!["id"]!.GetValue<string>()),
            j["user"]!["email"]?.GetValue<string>() ?? "");
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string url, object? corpo, string chave, string? bearer = null)
    {
        using var req = new HttpRequestMessage(metodo, url);
        req.Headers.Add("apikey", chave);
        // As chaves antigas são JWT e vão também no Authorization; as novas (sb_publishable_/sb_secret_) só no apikey.
        var auth = bearer ?? (chave.StartsWith("eyJ") ? chave : null);
        if (auth is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);

        try
        {
            return await _http.SendAsync(req);
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "Não foi possível contactar o Supabase Auth.");
            throw new SupabaseAuthException(503, "Não foi possível contactar o serviço de autenticação. Tente novamente.");
        }
    }

    /// <summary>Converte os erros do Supabase em mensagens em português.</summary>
    private async Task GarantirSucessoAsync(HttpResponseMessage res)
    {
        if (res.IsSuccessStatusCode) return;

        var corpo = await res.Content.ReadAsStringAsync();
        string codigo = "", texto = "";
        try
        {
            var j = JsonNode.Parse(corpo);
            codigo = j?["error_code"]?.GetValue<string>() ?? j?["error"]?.GetValue<string>() ?? "";
            texto = j?["msg"]?.GetValue<string>() ?? j?["error_description"]?.GetValue<string>() ?? j?["message"]?.GetValue<string>() ?? "";
        }
        catch (JsonException) { /* corpo sem JSON */ }

        _log.LogWarning("Supabase Auth respondeu {Status} {Codigo}: {Texto}", (int)res.StatusCode, codigo, texto);

        throw codigo switch
        {
            "invalid_credentials" or "invalid_grant" when texto.Contains("refresh", StringComparison.OrdinalIgnoreCase)
                => new SupabaseAuthException(401, "A sua sessão terminou. Inicie sessão novamente."),
            "refresh_token_not_found" or "refresh_token_already_used" or "session_not_found" or "session_expired"
                => new SupabaseAuthException(401, "A sua sessão terminou. Inicie sessão novamente."),
            "invalid_credentials" or "invalid_grant"
                => new SupabaseAuthException(401, "Email ou palavra-passe incorretos."),
            "user_banned"
                => new SupabaseAuthException(403, "Esta conta está desativada. Contacte os serviços da escola."),
            "email_not_confirmed"
                => new SupabaseAuthException(403, "O email desta conta ainda não foi confirmado."),
            "email_exists" or "user_already_exists"
                => new SupabaseAuthException(409, "Já existe uma conta com este email."),
            "weak_password"
                => new SupabaseAuthException(400, "A palavra-passe é demasiado fraca. Use pelo menos 8 caracteres, com letras e números."),
            "same_password"
                => new SupabaseAuthException(400, "A nova palavra-passe tem de ser diferente da anterior."),
            "over_request_rate_limit" or "over_email_send_rate_limit"
                => new SupabaseAuthException(429, "Demasiadas tentativas. Aguarde um pouco e tente de novo."),
            "bad_jwt" or "not_authorized" when (int)res.StatusCode is 401 or 403
                => new SupabaseAuthException(400, "O link de recuperação é inválido ou já expirou. Peça um novo."),
            "user_not_found"
                => new SupabaseAuthException(404, "Conta não encontrada."),
            "validation_failed" when texto.Contains("email", StringComparison.OrdinalIgnoreCase)
                => new SupabaseAuthException(400, "O email não é válido."),
            _ when (int)res.StatusCode is 401 or 403 && texto.Contains("key", StringComparison.OrdinalIgnoreCase)
                => new SupabaseAuthException(500, "As chaves do Supabase na configuração da API não são válidas."),
            _ => new SupabaseAuthException(502, "O serviço de autenticação devolveu um erro. Tente novamente."),
        };
    }
}
