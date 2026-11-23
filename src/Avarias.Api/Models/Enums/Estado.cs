namespace Avarias.Api.Models;

// Os enums são guardados como texto na base de dados (ver AppDbContext).
public enum Estado
{
    Pendente,
    EmAnalise,
    EmResolucao,
    Resolvido,
}
