namespace Application.Modules.Wardrobe;

/// <summary>Очередь фото на удаление фона. Реализация — фоновый воркер в API.</summary>
public interface IWardrobeImageQueue
{
    /// <summary>Настроен ли сервис обработки (Flatlay:BaseUrl). Если нет — фото хранятся как есть.</summary>
    bool IsEnabled { get; }

    void Enqueue(string itemId);
}

/// <summary>Клиент сервиса flatlay.</summary>
public interface IFlatlayClient
{
    /// <summary>
    /// Возвращает JPEG без фона, либо null, если на фото не нашлось вещи.
    /// Сетевые ошибки и ошибки сервиса пробрасываются исключением.
    /// </summary>
    Task<byte[]?> ProcessAsync(byte[] image, string contentType, CancellationToken cancellationToken);
}
