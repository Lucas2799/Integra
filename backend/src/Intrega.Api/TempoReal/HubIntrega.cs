using Intrega.Infraestrutura.TempoReal;
using Intrega.Nucleo.Autenticacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Intrega.Api.TempoReal;

/// <summary>
/// Canal de tempo real com o app. Cada usuário entra no grupo dele e, se for gestor,
/// no grupo dos gestores da organização. O app só escuta; quem envia são os módulos.
/// </summary>
[Authorize]
public sealed class HubIntrega : Hub
{
    public static string GrupoDoUsuario(Guid usuarioId) => $"usuario:{usuarioId}";
    public static string GrupoDosGestores(Guid organizacaoId) => $"gestores:{organizacaoId}";

    public override async Task OnConnectedAsync()
    {
        var usuario = Context.User;
        if (Guid.TryParse(usuario?.FindFirst("sub")?.Value, out var usuarioId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoDoUsuario(usuarioId));

        if (usuario?.FindFirst(ClaimsIntrega.PapelNaOrganizacao)?.Value == ClaimsIntrega.PapelGestor &&
            Guid.TryParse(usuario.FindFirst(ClaimsIntrega.OrganizacaoId)?.Value, out var organizacaoId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoDosGestores(organizacaoId));

        await base.OnConnectedAsync();
    }
}

/// <summary>Implementação de <see cref="INotificadorTempoReal"/> usada pelos módulos.</summary>
public sealed class NotificadorSignalR(IHubContext<HubIntrega> hub) : INotificadorTempoReal
{
    public Task NotificarUsuarioAsync(Guid usuarioId, string evento, object dados, CancellationToken ct = default) =>
        hub.Clients.Group(HubIntrega.GrupoDoUsuario(usuarioId)).SendAsync(evento, dados, ct);

    public Task NotificarGestoresAsync(Guid organizacaoId, string evento, object dados, CancellationToken ct = default) =>
        hub.Clients.Group(HubIntrega.GrupoDosGestores(organizacaoId)).SendAsync(evento, dados, ct);
}
