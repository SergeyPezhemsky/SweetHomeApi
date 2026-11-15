using System.Threading.Channels;
using Application.Modules.Finance;
using Microsoft.Extensions.Options;

namespace SweetHomeApi.Infrastructure.Finance;

/// <summary>Очередь чеков на распознавание в памяти процесса; после рестарта восстанавливается из БД (статус PENDING).</summary>
public class FinanceRecognitionQueue(
    IOptions<ReceiptRecognitionOptions> options,
    IOptions<AnthropicRecognitionOptions>? anthropic = null) : IFinanceRecognitionQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    /// <summary>Включён хотя бы один провайдер из порядка.</summary>
    public bool IsEnabled =>
        (options.Value.IsEnabled && RecognizerOrder.Includes(options.Value.Order, OpenAiCompatibleReceiptRecognizer.ProviderName))
        || (anthropic?.Value.IsEnabled == true && RecognizerOrder.Includes(options.Value.Order, AnthropicReceiptRecognizer.ProviderName));

    public ChannelReader<string> Reader => _channel.Reader;

    public void Enqueue(string receiptId)
    {
        if (IsEnabled)
            _channel.Writer.TryWrite(receiptId);
    }
}
