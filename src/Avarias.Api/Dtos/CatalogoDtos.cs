namespace Avarias.Api.Dtos;

// Catálogos: salas com equipamentos, tipos de equipamento e estados
/// <summary>Chave = descrição sem acentos ("em-analise"), usada no frontend para as cores.</summary>
public record EstadoDto(Guid Id, string Descricao, string Chave);
public record TipoEquipamentoDto(Guid Id, string Descricao);
public record EquipamentoDto(Guid Id, string Descricao, string? NrSerie, Guid? IdTipo, string? Tipo);
public record SalaDto(string NrSala, string TipoSala, bool EBiblioteca, List<EquipamentoDto> Equipamentos);
public record CatalogosDto(List<SalaDto> Salas, List<TipoEquipamentoDto> TiposEquipamento, List<EstadoDto> Estados);
