namespace Application.Modules.Finance;

/// <summary>Очередь чеков на распознавание. Реализация — фоновый воркер в API.</summary>
public interface IFinanceRecognitionQueue
{
    /// <summary>Настроено ли распознавание (ключ и модель провайдера).</summary>
    bool IsEnabled { get; }

    void Enqueue(string receiptId);
}

/// <summary>Vision-модель, читающая чек. Возвращает сырой текст ответа модели.</summary>
public interface IReceiptRecognizer
{
    bool IsEnabled { get; }

    /// <summary>
    /// Сетевые сбои, 429 и 5xx — <see cref="ReceiptRecognizerUnavailableException"/> (имеет смысл повторить);
    /// остальные ошибки провайдера — <see cref="ReceiptRecognizerException"/>.
    /// </summary>
    Task<string> RecognizeAsync(byte[] image, string contentType, CancellationToken cancellationToken);
}

public class ReceiptRecognizerException(string message, Exception? inner = null) : Exception(message, inner);

public class ReceiptRecognizerUnavailableException(string message, Exception? inner = null)
    : ReceiptRecognizerException(message, inner);
