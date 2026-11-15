using Application.Modules.Finance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SweetHomeApi.Infrastructure.Finance;

/// <summary>
/// Распознаёт чеки по одному. Результат сохраняется только для той ревизии чека,
/// которую отправляли (правка или удаление чека во время распознавания его отменяют).
/// </summary>
public class FinanceRecognitionWorker(
    FinanceRecognitionQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<ReceiptRecognitionOptions> options,
    ILogger<FinanceRecognitionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!queue.IsEnabled)
        {
            if (options.Value.MissingYandexFolder)
                logger.LogWarning("Receipt recognition is disabled: Finance:Recognition:Project (Yandex folder id) is empty");
            else
                logger.LogInformation("Receipt recognition is disabled (Finance:Recognition:ApiKey or Model is empty)");
            // Чеки, оставшиеся в очереди с прошлого запуска, иначе висели бы в PENDING навсегда.
            await FailPendingAsync(stoppingToken);
            return;
        }

        await EnqueuePendingAsync(stoppingToken);

        await foreach (var receiptId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(receiptId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while recognizing receipt {ReceiptId}", receiptId);
            }
        }
    }

    private async Task EnqueuePendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFinanceRepository>();
            var pending = await repository.GetPendingReceiptIdsAsync();
            foreach (var receiptId in pending)
                queue.Enqueue(receiptId);

            if (pending.Count > 0)
                logger.LogInformation("Re-queued {Count} receipts for recognition", pending.Count);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to re-queue pending receipts");
        }
    }

    private async Task FailPendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFinanceRepository>();
            var service = scope.ServiceProvider.GetRequiredService<IFinanceService>();
            foreach (var receiptId in await repository.GetPendingReceiptIdsAsync())
            {
                stoppingToken.ThrowIfCancellationRequested();
                var pending = await repository.GetPendingRecognitionAsync(receiptId);
                if (pending is not null)
                    await service.FailRecognitionAsync(pending, FinanceFailureReason.DISABLED);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to reset pending receipts");
        }
    }

    public async Task ProcessAsync(string receiptId, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFinanceRepository>();
        var service = scope.ServiceProvider.GetRequiredService<IFinanceService>();
        var recognizer = scope.ServiceProvider.GetRequiredService<IReceiptRecognizer>();

        FinancePendingRecognition? pending;
        try
        {
            // Чек могли удалить, отредактировать или уже распознать — тогда PENDING-строки нет.
            pending = await repository.GetPendingRecognitionAsync(receiptId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to load receipt {ReceiptId} for recognition", receiptId);
            RequeueLater(receiptId, stoppingToken);
            return;
        }

        if (pending is null)
            return;

        var response = await RecognizeAsync(recognizer, repository, service, pending, stoppingToken);
        if (response is null)
            return;

        try
        {
            var saved = await service.CompleteRecognitionAsync(pending, response);
            if (!saved)
                logger.LogInformation("Receipt {ReceiptId} changed during recognition; result discarded", receiptId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // Ответ модели есть, но сохранить его не вышло — это не вина провайдера.
            logger.LogError(ex, "Failed to save recognition result for receipt {ReceiptId}", receiptId);
            await FailSafelyAsync(service, pending, FinanceFailureReason.INTERNAL_ERROR, response, stoppingToken);
        }
    }

    /// <summary>Ответ модели или null, если чек уже помечен FAILED (или изменился и ждать нечего).</summary>
    private async Task<string?> RecognizeAsync(IReceiptRecognizer recognizer, IFinanceRepository repository, IFinanceService service,
        FinancePendingRecognition pending, CancellationToken stoppingToken)
    {
        var attempts = Math.Max(1, options.Value.Retries + 1);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return await recognizer.RecognizeAsync(pending.Data, pending.ContentType, stoppingToken);
            }
            catch (ReceiptRecognizerUnavailableException ex) when (attempt < attempts)
            {
                // Провайдер временно недоступен (сеть, 429, 5xx) — подождём и повторим, если чек ещё ждёт.
                logger.LogWarning(ex, "Recognition provider unavailable (attempt {Attempt}/{Attempts}) for {ReceiptId}",
                    attempt, attempts, pending.ReceiptId);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, options.Value.RetryDelaySeconds) * attempt), stoppingToken);

                var current = await repository.GetPendingRecognitionAsync(pending.ReceiptId);
                if (current is null || current.Revision != pending.Revision)
                    return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Recognition failed for receipt {ReceiptId}", pending.ReceiptId);
                await FailSafelyAsync(service, pending, FinanceFailureReason.PROVIDER_ERROR, null, stoppingToken);
                return null;
            }
        }

        return null;
    }

    private async Task FailSafelyAsync(IFinanceService service, FinancePendingRecognition pending, FinanceFailureReason reason,
        string? response, CancellationToken stoppingToken)
    {
        try
        {
            await service.FailRecognitionAsync(pending, reason, response);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // База недоступна — чек остался PENDING; попробуем позже, а не только после рестарта.
            logger.LogError(ex, "Failed to mark receipt {ReceiptId} as failed", pending.ReceiptId);
            RequeueLater(pending.ReceiptId, stoppingToken);
        }
    }

    private void RequeueLater(string receiptId, CancellationToken stoppingToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.Value.InternalRetryDelaySeconds)), stoppingToken);
                queue.Enqueue(receiptId);
            }
            catch (OperationCanceledException)
            {
                // Сервис останавливается — чек подхватится при следующем запуске (он всё ещё PENDING).
            }
        }, CancellationToken.None);
    }
}
