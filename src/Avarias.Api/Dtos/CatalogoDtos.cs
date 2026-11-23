namespace Avarias.Api.Dtos;

// Catálogos
public record EquipamentoDto(int Id, string Nome);
public record LocalDto(int Id, string Nome, bool EBiblioteca, List<EquipamentoDto> Equipamentos);
public record TipoDto(int Id, string Nome);
public record CatalogosDto(List<LocalDto> Locais, List<TipoDto> Tipos);
