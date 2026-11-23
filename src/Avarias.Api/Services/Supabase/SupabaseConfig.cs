namespace Avarias.Api.Services.Supabase;

public static class SupabaseConfig
{
    /// <summary>Endereço do projeto, ex.: https://abcdefgh.supabase.co (sem barra no fim).</summary>
    public static string Url(IConfiguration cfg)
    {
        var url = cfg["Supabase:Url"]?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url) || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
            throw new InvalidOperationException("Configure Supabase:Url (ex.: https://abcdefgh.supabase.co). Ver README.");
        return url;
    }

    /// <summary>Emissor dos tokens do Supabase Auth.</summary>
    public static string Issuer(IConfiguration cfg) => Url(cfg) + "/auth/v1";

    /// <summary>Audiência dos tokens de utilizadores com sessão.</summary>
    public const string Audience = "authenticated";
}
