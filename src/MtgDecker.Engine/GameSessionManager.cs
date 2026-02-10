using System.Collections.Concurrent;

namespace MtgDecker.Engine;

public class GameSessionManager
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();

    public GameSession CreateGame()
    {
        var gameId = GenerateGameId();
        var session = new GameSession(gameId);
        _sessions[gameId] = session;
        return session;
    }

    public GameSession? GetSession(string gameId) =>
        _sessions.TryGetValue(gameId, out var session) ? session : null;

    public void RemoveSession(string gameId) =>
        _sessions.TryRemove(gameId, out _);

    private string GenerateGameId()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string id;
        do
        {
            id = new string(Enumerable.Range(0, 6)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());
        } while (_sessions.ContainsKey(id));
        return id;
    }
}
