namespace Backyard_Mech.Services
{
    public sealed class RecordsDispositionCleanupService : BackgroundService
    {
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RecordsDispositionCleanupService> _logger;

        public RecordsDispositionCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<RecordsDispositionCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RunCleanupAsync(stoppingToken);

            using var timer = new PeriodicTimer(CleanupInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCleanupAsync(stoppingToken);
            }
        }

        private async Task RunCleanupAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<RecordsDispositionService>();
                var deletedCount = await service.PurgeEligibleAsync(cancellationToken);
                if (deletedCount > 0)
                {
                    _logger.LogInformation("Permanently deleted {DeletedCount} expired records-disposition item(s).", deletedCount);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Records-disposition automatic cleanup failed.");
            }
        }
    }
}
