using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Risk.Web.Services;

/// <summary>
/// Captures each Blazor circuit's id into its scoped
/// <see cref="NetworkedSeatContext"/>, so Lobby/Join can bind seats to the
/// calling device without any JS interop (which is unsafe during the
/// static prerender pass — see Game.razor's OnInitialized comment).
///
/// On close it frees the seat binding (host role migrates inside
/// <see cref="NetworkGameSession.Leave"/>); the seat itself survives for a
/// reconnect rebind.
/// </summary>
public sealed class NetworkCircuitHandler(NetworkedSeatContext context, NetworkGameRegistry games) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        context.CircuitId = circuit.Id;
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (context.Code is not null
            && context.Seat is { } seat
            && games.TryGet(context.Code, out var session)
            && session is not null)
        {
            session.Leave(seat);
        }

        return Task.CompletedTask;
    }
}
