namespace Avarias.Api.Services.Fotos;

/// <summary>
/// Guarda as fotografias na pasta de uploads (Uploads:Pasta) com nome gerado pelo servidor.
/// Na base de dados fica só o nome do ficheiro (foto_avaria, foto_resolucao).
/// </summary>
public class ArmazenamentoFotos
{
    public const long TamanhoMax = 5 * 1024 * 1024;
    private readonly string _pasta;

    public ArmazenamentoFotos(IWebHostEnvironment env, IConfiguration cfg) =>
        _pasta = Path.Combine(env.ContentRootPath, cfg["Uploads:Pasta"] ?? "uploads");

    /// <summary>Valida (tamanho e tipo real pelos primeiros bytes) e grava. Devolve o nome ou uma mensagem de erro.</summary>
    public async Task<(string? Ficheiro, string? Erro)> GuardarAsync(IFormFile foto)
    {
        if (foto.Length > TamanhoMax) return (null, "A fotografia é demasiado grande (máximo 5 MB).");

        var ext = await DetetarExtensaoAsync(foto);
        if (ext is null) return (null, "A fotografia deve ser uma imagem JPG, PNG ou WebP.");

        Directory.CreateDirectory(_pasta);
        var nome = Guid.NewGuid().ToString("N") + ext;
        await using var fs = File.Create(Path.Combine(_pasta, nome));
        await foto.CopyToAsync(fs);
        return (nome, null);
    }

    /// <summary>Caminho completo do ficheiro, ou null se não existir. Ignora qualquer pasta no nome.</summary>
    public string? Caminho(string? ficheiro)
    {
        if (string.IsNullOrWhiteSpace(ficheiro)) return null;
        var caminho = Path.Combine(_pasta, Path.GetFileName(ficheiro));
        return File.Exists(caminho) ? caminho : null;
    }

    public static string TipoMime(string ficheiro) => Path.GetExtension(ficheiro).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };

    private static async Task<string?> DetetarExtensaoAsync(IFormFile file)
    {
        var b = new byte[12];
        await using var s = file.OpenReadStream();
        var lidos = await s.ReadAsync(b);
        if (lidos < 12) return null;

        if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
        if (b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' &&
            b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ".webp";
        return null;
    }
}
