using System.Globalization;
using System.Text;

namespace Avarias.Api.Models;

/// <summary>
/// Os perfis e os estados estão na base de dados como texto livre ("Técnico", "Em análise"...).
/// Estas funções comparam esses textos sem acentos nem maiúsculas.
/// </summary>
public static class Textos
{
    /// <summary>"Em Análise" → "em-analise".</summary>
    public static string Chave(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var semAcentos = new StringBuilder();
        foreach (var c in texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            semAcentos.Append(char.IsLetterOrDigit(c) ? c : '-');
        }
        return string.Join('-', semAcentos.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    public static Perfil? PerfilDe(string? tipoUtilizador) => Chave(tipoUtilizador) switch
    {
        "professor" => Perfil.Professor,
        "biblioteca" => Perfil.Biblioteca,
        "tecnico" => Perfil.Tecnico,
        _ => null,
    };

    /// <summary>Texto guardado em tipos_utilizador para cada perfil (quando a API o cria).</summary>
    public static string TextoPerfil(Perfil p) => p switch
    {
        Perfil.Tecnico => "Técnico",
        _ => p.ToString(),
    };

    public const string EstadoInicial = "pendente";

    /// <summary>"Resolvida" ou "Resolvido": exige resolução escrita e marca a data de resolução.</summary>
    public static bool EResolvido(string? estado) => Chave(estado) is "resolvida" or "resolvido";

    /// <summary>Estados criados se a tabela estados_avarias estiver vazia.</summary>
    public static readonly string[] EstadosBase = { "Pendente", "Em resolução", "Resolvida", "Cancelada" };

    /// <summary>Ordem de apresentação; estados que não estejam aqui aparecem no fim, por nome.</summary>
    public static readonly string[] OrdemEstados =
        { "pendente", "em-analise", "em-resolucao", "resolvida", "resolvido", "cancelada", "cancelado" };

    public static bool EBiblioteca(string? tipoSala) => Chave(tipoSala) == "biblioteca";
}
