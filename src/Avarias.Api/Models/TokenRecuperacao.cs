namespace Avarias.Api.Models;

public class TokenRecuperacao
{
    public int Id { get; set; }
    public int UtilizadorId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiraEm { get; set; }
    public bool Usado { get; set; }
}
