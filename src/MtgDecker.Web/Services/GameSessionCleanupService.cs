using MtgDecker.Engine;

namespace MtgDecker.Web.Services;

public class GameSessionCleanupService : BackgroundService
{
    private readonly GameSessionManager _sessionManager;
    private readonly ILogger<GameSessionCleanupService> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxInactivity = TimeSpan.FromMinutes(30);

    public GameSessionCleanupService(GameSessionManager sessionManager, ILogger<GameSessionCleanupService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CleanupInterval, stoppingToken);

            var staleIds = _sessionManager.GetStaleSessionIds(MaxInactivity);
            foreach (var id in staleIds)
            {
                _sessionManager.RemoveSession(id);
                _logger.LogInformation("Cleaned up stale game session: {SessionId}", id);
            }
        }
    }
}
