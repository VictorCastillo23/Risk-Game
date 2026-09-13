using Risk.Domain.Dice;
using Risk.Engine;
using Risk.Web.Models;

namespace Risk.Web.Services;

/// <summary>
/// Join-code -> <see cref="NetworkGameSession"/> map (registered singleton
/// in <c>Program.cs</c>). Owns code generation including collision retry:
/// on the astronomically rare duplicate it regenerates instead of
/// overwriting an live game.
/// </summary>
public sealed class NetworkGameRegistry(IGameEngine engine, IDiceRoller dice)
{
    private readonly Dictionary<string, NetworkGameSession> _games = new();

    public NetworkGameSession Create()
    {
        JoinCode code;
        do
        {
            code = JoinCode.Generate();
        } while (_games.ContainsKey(code.Value));

        var session = new NetworkGameSession(engine, dice, code);
        _games[code.Value] = session;
        return session;
    }

    public bool TryGet(JoinCode code, out NetworkGameSession? session) =>
        _games.TryGetValue(code.Value, out session);
}
