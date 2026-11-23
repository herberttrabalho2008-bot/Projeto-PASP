using Microsoft.IdentityModel.Tokens;

namespace Avarias.Api.Services.Supabase;

/// <summary>
/// Chaves para validar os tokens do Supabase Auth:
///  - chaves públicas (ES256/RS256) publicadas em {Url}/auth/v1/.well-known/jwks.json (projetos atuais);
///  - opcionalmente o "JWT Secret" antigo (HS256), em Supabase:JwtSecret.
/// As chaves públicas ficam em memória 10 minutos; um "kid" desconhecido força nova leitura (no máximo 1x/minuto).
/// </summary>
public class SupabaseJwks
{
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly SecurityKey? _segredo;
    private readonly ILogger<SupabaseJwks> _log;
    private readonly object _lock = new();

    private IList<SecurityKey> _chaves = Array.Empty<SecurityKey>();
    private DateTime _lidoEm = DateTime.MinValue;

    public SupabaseJwks(HttpClient http, IConfiguration cfg, ILogger<SupabaseJwks> log)
    {
        _http = http;
        _log = log;
        _url = SupabaseConfig.Url(cfg) + "/auth/v1/.well-known/jwks.json";
        var segredo = cfg["Supabase:JwtSecret"];
        if (!string.IsNullOrWhiteSpace(segredo))
            _segredo = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(segredo));
    }

    public IEnumerable<SecurityKey> Chaves(string? kid)
    {
        var chaves = Obter(forcar: false);
        if (kid is not null && chaves.All(k => k.KeyId != kid))
            chaves = Obter(forcar: true);

        return _segredo is null ? chaves : chaves.Append(_segredo);
    }

    private IList<SecurityKey> Obter(bool forcar)
    {
        lock (_lock)
        {
            var idade = DateTime.UtcNow - _lidoEm;
            if (idade < TimeSpan.FromMinutes(forcar ? 1 : 10)) return _chaves;
            _lidoEm = DateTime.UtcNow;
            try
            {
                var json = _http.GetStringAsync(_url).GetAwaiter().GetResult();
                _chaves = new JsonWebKeySet(json).GetSigningKeys();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException)
            {
                _log.LogWarning(ex, "Não foi possível ler as chaves do Supabase em {Url}.", _url);
            }
            return _chaves;
        }
    }
}
