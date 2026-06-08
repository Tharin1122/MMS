using MMS.Infrastructure.Persistence.Services;

namespace MMS.Api.Services;

public class CleaningCheckBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<CleaningCheckBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<RoomCleaningService>();
                await svc.ProcessCleaningRoomsAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CleaningCheck error");
            }

            // รันทุก 30 วินาที — delay สูงสุด 30 วิ
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}