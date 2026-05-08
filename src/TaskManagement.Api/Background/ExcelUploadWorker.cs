using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Api.Background;

public sealed class ExcelUploadWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExcelUploadWorker> _logger;

    public ExcelUploadWorker(IServiceScopeFactory scopeFactory, ILogger<ExcelUploadWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExcelUploadWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            long uploadId;
            try
            {
                uploadId = await ExcelUploadBackgroundQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var admin = scope.ServiceProvider.GetRequiredService<IAdminService>();
                await admin.ProcessExcelUploadAsync(uploadId, stoppingToken);
            }
            catch (Exception ex)
            {
                // Processing already records failures + errors in DB; this is just to keep the worker alive.
                _logger.LogError(ex, "ExcelUploadWorker failed processing uploadId={UploadId}", uploadId);
            }
        }

        _logger.LogInformation("ExcelUploadWorker stopped");
    }
}

