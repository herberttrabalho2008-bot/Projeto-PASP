using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Avarias.Api.Data;
using Avarias.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Avarias.Api.Tests;

public class RegrasDeAcessoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public RegrasDeAcessoTests(ApiFactory f) => _f = f;

    private record LoginResp(string Token);
    private record Criado(Guid Id);
    private record Estado(Guid Id, string Descricao, string Chave);
    private record Resumo(Guid Id);
    private record Detalhe(Guid Id, Estado Estado, List<object> Historico);
    private record ContaDto(Guid Id, string Nome, string? Email);

    // ------------------------------------------------------------------ preparação
    private AppDbContext Db(IServiceScope s) => s.ServiceProvider.GetRequiredService<AppDbContext>();

    /// <summary>Cria a conta no "Supabase Auth" e a linha em utilizadores com o perfil.</summary>
    private async Task<string> Conta(Perfil perfil, string? email = null, bool comPerfil = true)
    {
        _ = _f.Server; // garante que a API arrancou (e o DbSeeder criou os perfis e estados)
        email ??= $"{perfil.ToString().ToLower()}.{Guid.NewGuid():N}@escola.test";
        var id = Guid.NewGuid();
        SupabaseAuthFalso.Registar(id, email, "Segura123!");

        using var scope = _f.Services.CreateScope();
        var db = Db(scope);
        db.ContasAuth.Add(new ContaAuth { Id = id, Email = email });
        if (comPerfil)
        {
            var tipo = (await db.TiposUtilizador.ToListAsync()).First(t => Textos.PerfilDe(t.Descricao) == perfil);
            db.Utilizadores.Add(new Utilizador { Id = id, NomeUtilizador = email, PermissaoId = tipo.Id });
        }
        await db.SaveChangesAsync();
        return email;
    }

    private async Task<HttpClient> Entrar(string email)
    {
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = "Segura123!" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var token = (await r.Content.ReadFromJsonAsync<LoginResp>())!.Token;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task<HttpClient> EntrarComo(Perfil perfil) => await Entrar(await Conta(perfil));

    /// <summary>Sala + tipo + equipamento novos; devolve o id do equipamento.</summary>
    private async Task<(Guid Equipamento, string Sala, Guid Tipo)> Equipamento()
    {
        using var scope = _f.Services.CreateScope();
        var db = Db(scope);
        var sala = new Sala { NrSala = "S" + Guid.NewGuid().ToString("N")[..6], TipoSala = "Sala de aula" };
        var tipo = new TipoEquipamento { Id = Guid.NewGuid(), DescTipo = "Projetor " + Guid.NewGuid().ToString("N")[..6] };
        var eq = new Equipamento { Id = Guid.NewGuid(), NrSala = sala.NrSala, IdTipo = tipo.Id, DescEquipamento = "Projetor da sala" };
        db.AddRange(sala, tipo, eq);
        await db.SaveChangesAsync();
        return (eq.Id, sala.NrSala, tipo.Id);
    }

    private async Task<Guid> RegistarAvaria(HttpClient c, Guid equipamentoId)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("O projetor da sala não acende desde ontem."), "descricao" },
            { new StringContent(equipamentoId.ToString()), "equipamentoId" },
        };
        var r = await c.PostAsync("/api/avarias", form);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<Criado>())!.Id;
    }

    private static Task<HttpResponseMessage> Atualizar(HttpClient c, Guid id, Guid? estadoId, string? resolucao = null)
    {
        var form = new MultipartFormDataContent();
        if (estadoId is not null) form.Add(new StringContent(estadoId.ToString()!), "estadoId");
        if (resolucao is not null) form.Add(new StringContent(resolucao), "resolucao");
        return c.PatchAsync($"/api/avarias/{id}", form);
    }

    private async Task<Guid> EstadoId(string chave)
    {
        using var scope = _f.Services.CreateScope();
        return (await Db(scope).EstadosAvarias.ToListAsync()).First(e => Textos.Chave(e.DescEstado) == chave).Id;
    }

    // ------------------------------------------------------------------ testes
    [Fact]
    public async Task Professor_so_ve_as_suas_e_so_o_tecnico_muda_o_estado()
    {
        var autor = await EntrarComo(Perfil.Professor);
        var outro = await EntrarComo(Perfil.Professor);
        var biblioteca = await EntrarComo(Perfil.Biblioteca);
        var tecnico = await EntrarComo(Perfil.Tecnico);
        var (eq, _, _) = await Equipamento();

        var id = await RegistarAvaria(autor, eq);

        Assert.Equal(HttpStatusCode.OK, (await autor.GetAsync($"/api/avarias/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.GetAsync($"/api/avarias/{id}")).StatusCode);
        Assert.DoesNotContain((await outro.GetFromJsonAsync<List<Resumo>>("/api/avarias"))!, a => a.Id == id);
        Assert.Contains((await biblioteca.GetFromJsonAsync<List<Resumo>>("/api/avarias"))!, a => a.Id == id);

        // O técnico não regista avarias; o professor e a biblioteca não mudam o estado.
        var form = new MultipartFormDataContent { { new StringContent("Texto com mais de dez letras"), "descricao" }, { new StringContent(eq.ToString()), "equipamentoId" } };
        Assert.Equal(HttpStatusCode.Forbidden, (await tecnico.PostAsync("/api/avarias", form)).StatusCode);
        var emAnalise = await EstadoId("em-analise");
        Assert.Equal(HttpStatusCode.Forbidden, (await Atualizar(autor, id, emAnalise)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Atualizar(biblioteca, id, emAnalise)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Atualizar(tecnico, id, emAnalise)).StatusCode);

        // Resolvida exige resolução escrita; cada alteração fica no histórico.
        var resolvido = await EstadoId("resolvido");
        Assert.Equal(HttpStatusCode.BadRequest, (await Atualizar(tecnico, id, resolvido)).StatusCode);
        var ok = await Atualizar(tecnico, id, resolvido, "Lâmpada do projetor substituída.");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var d = (await ok.Content.ReadFromJsonAsync<Detalhe>())!;
        Assert.Equal("resolvido", d.Estado.Chave);
        Assert.Equal(3, d.Historico.Count);
    }

    [Fact]
    public async Task Backoffice_so_para_tecnico_e_sem_se_trancar_fora()
    {
        var professor = await EntrarComo(Perfil.Professor);
        var biblioteca = await EntrarComo(Perfil.Biblioteca);
        var emailTecnico = await Conta(Perfil.Tecnico);
        var tecnico = await Entrar(emailTecnico);

        Assert.Equal(HttpStatusCode.Forbidden, (await professor.GetAsync("/api/admin/utilizadores")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await biblioteca.PostAsJsonAsync("/api/admin/tipos-equipamento", new { descricao = "Tipo novo" })).StatusCode);

        // Criar uma conta e entrar com ela; o email fica em minúsculas.
        var criar = await tecnico.PostAsJsonAsync("/api/admin/utilizadores",
            new { nome = "Nova Pessoa", email = "Nova.Pessoa@Escola.test", perfil = "Professor", ativo = true, password = "Segura123!" });
        Assert.Equal(HttpStatusCode.Created, criar.StatusCode);
        await Entrar("nova.pessoa@escola.test");
        Assert.Equal(HttpStatusCode.Conflict, (await tecnico.PostAsJsonAsync("/api/admin/utilizadores",
            new { nome = "Repetida", email = "nova.pessoa@escola.test", perfil = "Professor", password = "Segura123!" })).StatusCode);

        // O técnico não se pode desativar a si próprio.
        var eu = (await tecnico.GetFromJsonAsync<List<ContaDto>>("/api/admin/utilizadores"))!.First(u => u.Email == emailTecnico);
        Assert.Equal(HttpStatusCode.BadRequest, (await tecnico.PutAsJsonAsync($"/api/admin/utilizadores/{eu.Id}",
            new { nome = eu.Nome, email = eu.Email, perfil = "Tecnico", ativo = false })).StatusCode);
    }

    [Fact]
    public async Task Nao_apaga_o_que_esta_em_uso()
    {
        var autor = await EntrarComo(Perfil.Professor);
        var tecnico = await EntrarComo(Perfil.Tecnico);
        var (eq, sala, tipo) = await Equipamento();
        await RegistarAvaria(autor, eq);

        Assert.Equal(HttpStatusCode.Conflict, (await tecnico.DeleteAsync($"/api/admin/equipamentos/{eq}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await tecnico.DeleteAsync($"/api/admin/salas/{sala}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await tecnico.DeleteAsync($"/api/admin/tipos-equipamento/{tipo}")).StatusCode);

        var novo = await tecnico.PostAsJsonAsync("/api/admin/tipos-equipamento", new { descricao = "Tipo sem equipamentos" });
        var idNovo = (await novo.Content.ReadFromJsonAsync<Criado>())!.Id;
        Assert.Equal(HttpStatusCode.NoContent, (await tecnico.DeleteAsync($"/api/admin/tipos-equipamento/{idNovo}")).StatusCode);
    }

    [Fact]
    public async Task Conta_desativada_perde_o_acesso_logo()
    {
        var email = await Conta(Perfil.Tecnico);
        var c = await Entrar(email);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/avarias")).StatusCode);

        using (var scope = _f.Services.CreateScope())
        {
            var db = Db(scope);
            var conta = await db.ContasAuth.FirstAsync(x => x.Email == email);
            conta.BannedUntil = DateTime.UtcNow.AddYears(100);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/avarias")).StatusCode);
    }

    [Fact]
    public async Task Conta_sem_perfil_nao_entra()
    {
        var email = await Conta(Perfil.Professor, comPerfil: false);
        var r = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "Segura123!" });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Token_invalido_e_recusado()
    {
        var c = _f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "abc.def.ghi");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/avarias")).StatusCode);
    }
}
