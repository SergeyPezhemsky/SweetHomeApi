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
    ILogger<FinanceRecognitionWorker> logger,
    IOptions<AnthropicRecognitionOptions>? anthropicOptions = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!queue.IsEnabled)
        {
            if (options.Value.MissingYandexFolder)
                logger.LogWarning("Receipt recognition is disabled: Finance:Recognition:Project (Yandex folder id) is empty");
            else
                logger.LogInformation("Receipt recognition is disabled: no provider from Finance:Recognition:Order is configured");
            // Чеки, оставшиеся в очереди с прошлого запуска, иначе висели бы в PENDING навсегда.
            await FailPendingAsync(stoppingToken);
            return;
        }

        // Yandex без каталога пропускается молча, если работает другой провайдер, — предупредим при старте.
        if (options.Value.MissingYandexFolder && RecognizerOrder.Includes(options.Value.Order, OpenAiCompatibleReceiptRecognizer.ProviderName))
            logger.LogWarning("Yandex recognition is skipped: Finance:Recognition:Project (Yandex folder id) is empty");

        var anthropic = anthropicOptions ?? Options.Create(new AnthropicRecognitionOptions());
        if (anthropic.Value.HasInvalidEffort)
            logger.LogWarning("Finance:Recognition:Anthropic:Effort '{Effort}' is not one of low/medium/high/xhigh/max and is ignored",
                anthropic.Value.Effort);

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
        var recognizers = RecognizerOrder.Arrange(scope.ServiceProvider.GetServices<IReceiptRecognizer>(), options.Value.Order);

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

        if (recognizers.Count == 0)
        {
            await FailSafelyAsync(service, pending, FinanceFailureReason.DISABLED, null, null, stoppingToken);
            return;
        }

        // Провайдеры по порядку: следующий зовём, если предыдущий недоступен или не смог прочитать чек.
        // Итоговая причина неудачи — от последнего провайдера, который ответил; если не ответил никто — PROVIDER_ERROR.
        var failure = FinanceFailureReason.PROVIDER_ERROR;
        string? failedResponse = null;
        string? failedModel = null;
        foreach (var recognizer in recognizers)
        {
            var attempt = await RecognizeAsync(recognizer, repository, pending, stoppingToken);
            if (attempt.Stop)
                return;
            if (attempt.Response is null)
                continue;

            FinanceRecognitionBatch? result;
            FinanceFailureReason? reason;
            try
            {
                (result, reason) = await service.EvaluateRecognitionAsync(pending, attempt.Response, recognizer.Model);
                if (result is not null)
                {
                    var saved = await service.SaveRecognitionAsync(pending, result);
                    if (!saved)
                        logger.LogInformation("Receipt {ReceiptId} changed during recognition; result discarded", receiptId);
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // Ответ модели есть, но обработать или сохранить его не вышло — это не вина провайдера.
                logger.LogError(ex, "Failed to save recognition result for receipt {ReceiptId}", receiptId);
                await FailSafelyAsync(service, pending, FinanceFailureReason.INTERNAL_ERROR, attempt.Response, recognizer.Model,
                    stoppingToken);
                return;
            }

            logger.LogInformation("{Provider} could not read receipt {ReceiptId}: {Reason}", recognizer.Provider, receiptId, reason);
            failure = reason!.Value;
            failedResponse = attempt.Response;
            failedModel = recognizer.Model;

            // Пока первый провайдер думал, чек могли удалить или заполнить вручную — тогда фото дальше не отправляем.
            if (recognizer != recognizers[^1] && !await StillPendingAsync(repository, pending, stoppingToken))
                return;
        }

        await FailSafelyAsync(service, pending, failure, failedResponse, failedModel, stoppingToken);
    }

    /// <summary>
    /// Ответ одного провайдера с повторами при временной недоступности.
    /// Response = null — провайдер не ответил (можно звать следующего); Stop — чек изменился или отложен, дальше не идём.
    /// </summary>
    private async Task<(string? Response, bool Stop)> RecognizeAsync(IReceiptRecognizer recognizer, IFinanceRepository repository,
        FinancePendingRecognition pending, CancellationToken stoppingToken)
    {
        var attempts = Math.Max(1, options.Value.Retries + 1);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return (await recognizer.RecognizeAsync(pending.Data, pending.ContentType, stoppingToken), false);
            }
            catch (ReceiptRecognizerUnavailableException ex) when (attempt < attempts)
            {
                // Провайдер временно недоступен (сеть, 429, 5xx) — подождём и повторим, если чек ещё ждёт.
                logger.LogWarning(ex, "{Provider} unavailable (attempt {Attempt}/{Attempts}) for {ReceiptId}",
                    recognizer.Provider, attempt, attempts, pending.ReceiptId);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, options.Value.RetryDelaySeconds) * attempt), stoppingToken);

                if (!await StillPendingAsync(repository, pending, stoppingToken))
                    return (null, true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "{Provider} failed to recognize receipt {ReceiptId}", recognizer.Provider, pending.ReceiptId);
                // Перед следующим провайдером убеждаемся, что чек ещё ждёт: его могли отредактировать или удалить.
                return await StillPendingAsync(repository, pending, stoppingToken) ? (null, false) : (null, true);
            }
        }

        return (null, false);
    }

    /// <summary>Чек всё ещё ждёт эту ревизию. При ошибке базы — откладываем чек и говорим «нет».</summary>
    private async Task<bool> StillPendingAsync(IFinanceRepository repository, FinancePendingRecognition pending, CancellationToken stoppingToken)
    {
        try
        {
            var current = await repository.GetPendingRecognitionAsync(pending.ReceiptId);
            return current is not null && current.Revision == pending.Revision;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // База недоступна между попытками — чек остаётся PENDING, вернёмся к нему позже.
            logger.LogError(ex, "Failed to re-check receipt {ReceiptId}", pending.ReceiptId);
            RequeueLater(pending.ReceiptId, stoppingToken);
            return false;
        }
    }

    private async Task FailSafelyAsync(IFinanceService service, FinancePendingRecognition pending, FinanceFailureReason reason,
        string? response, string? model, CancellationToken stoppingToken)
    {
        try
        {
            await service.FailRecognitionAsync(pending, reason, response, model);
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
