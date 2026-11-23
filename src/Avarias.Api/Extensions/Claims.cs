using System.Security.Claims;
using Avarias.Api.Models;

namespace Avarias.Api.Extensions;

/// <summary>Claims do utilizador autenticado. "sub" vem do token do Supabase; nome e perfil vêm da base de dados.</summary>
public static class Claims
{
    public const string Nome = "nome";
    public const string Perfil = "perfil";

    public static Guid UserId(this ClaimsPrincipal u) => Guid.Parse(u.FindFirstValue("sub")!);

    public static Perfil PerfilApp(this ClaimsPrincipal u) => Enum.Parse<Perfil>(u.FindFirstValue(Perfil)!);

    public static string Email(this ClaimsPrincipal u) => u.FindFirstValue("email") ?? "";
}
