using Application.Modules.Wardrobe;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SweetHomeApi.Infrastructure.Flatlay;

/// <summary>
/// Обрабатывает фото по одному: так сервис flatlay держит в памяти одну модель и одно изображение.
/// Результат сохраняется только для той ревизии фото, которую отправляли на обработку.
/// </summary>
public class WardrobeImageProcessingWorker(
    WardrobeImageQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<FlatlayOptions> options,
    ILogger<WardrobeImageProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!queue.IsEnabled)
        {
            logger.LogInformation("Flatlay processing is disabled (Flatlay:BaseUrl is empty)");
            // Фото, оставшиеся в очереди с прошлого запуска, иначе висели бы в PENDING навсегда.
            await FailPendingAsync(stoppingToken);
            return;
        }

        await EnqueuePendingAsync(stoppingToken);

        await foreach (var itemId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(itemId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while processing wardrobe image {ItemId}", itemId);
            }
        }
    }

    private async Task EnqueuePendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWardrobeRepository>();
            var pending = await repository.GetPendingImageItemIdsAsync();
            foreach (var itemId in pending)
                queue.Enqueue(itemId);

            if (pending.Count > 0)
                logger.LogInformation("Re-queued {Count} wardrobe images for background removal", pending.Count);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to re-queue pending wardrobe images");
        }
    }

    private async Task FailPendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWardrobeRepository>();
            foreach (var itemId in await repository.GetPendingImageItemIdsAsync())
            {
                stoppingToken.ThrowIfCancellationRequested();
                var image = await repository.GetPendingImageAsync(itemId);
                if (image is not null)
                    await repository.MarkImageProcessingFailedAsync(itemId, image.Revision);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to reset pending wardrobe images");
        }
    }

    public async Task ProcessAsync(string itemId, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWardrobeRepository>();
        var client = scope.ServiceProvider.GetRequiredService<IFlatlayClient>();

        // Фото могли удалить, заменить или уже обработать — тогда в БД нет PENDING-строки.
        var image = await repository.GetPendingImageAsync(itemId);
        if (image is null)
            return;

        var attempts = Math.Max(1, options.Value.ConnectRetries + 1);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                var processed = await client.ProcessAsync(image.Data, image.ContentType, stoppingToken);
                if (processed is null || processed.Length == 0)
                {
                    logger.LogInformation("Flatlay found nothing on wardrobe image {ItemId}; keeping the original", itemId);
                    await repository.MarkImageProcessingFailedAsync(itemId, image.Revision);
                    return;
                }

                var saved = await repository.SaveProcessedImageAsync(itemId, image.Revision, processed, "image/jpeg");
                if (!saved)
                    logger.LogInformation("Wardrobe image {ItemId} changed during processing; result discarded", itemId);
                return;
            }
            catch (HttpRequestException ex) when (attempt < attempts && ex.StatusCode is null or System.Net.HttpStatusCode.ServiceUnavailable
                                                  or System.Net.HttpStatusCode.BadGateway)
            {
                // Сервис ещё не поднялся (после деплоя, перезапуск контейнера) — подождём и повторим.
                // Ошибку обработки (500) не повторяем: на том же фото она повторится, а очередь будет стоять.
                logger.LogWarning(ex, "Flatlay unavailable (attempt {Attempt}/{Attempts}) for {ItemId}", attempt, attempts, itemId);
                await Task.Delay(TimeSpan.FromSeconds(options.Value.RetryDelaySeconds), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Background removal failed for wardrobe image {ItemId}", itemId);
                await repository.MarkImageProcessingFailedAsync(itemId, image.Revision);
                return;
            }
        }

        await repository.MarkImageProcessingFailedAsync(itemId, image.Revision);
    }
}
