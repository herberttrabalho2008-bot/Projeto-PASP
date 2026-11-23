using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Avarias.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Avarias.Api.Services;

/// <summary>Cria os tokens JWT de sessão e os tokens de recuperação de password.</summary>
public class TokenService
{
    private readonly IConfiguration _cfg;
    public TokenService(IConfiguration cfg) => _cfg = cfg;

    public string CriarJwt(Utilizador u)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var claims = new[]
        {
            // Nomes curtos fixos (sub, name, email, role): ver MapInboundClaims = false em Program.cs.
            new Claim("sub", u.Id.ToString()),
            new Claim("name", u.Nome),
            new Claim("email", u.Email),
            // O perfil vai no token, mas a autorização é sempre validada no servidor
            // (atributos [Authorize(Roles=...)] e filtros nas consultas).
            new Claim("role", u.Perfil.ToString()),
        };

        var horas = _cfg.GetValue("Jwt:HorasValidade", 8);
        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(horas),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Gera um token aleatório (vai no email) e o respetivo hash (vai para a BD).</summary>
    public static (string Token, string Hash) NovoTokenRecuperacao()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return (token, Hash(token));
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
