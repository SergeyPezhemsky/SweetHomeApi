using System.Threading.Channels;
using Application.Modules.Finance;
using Microsoft.Extensions.Options;

namespace SweetHomeApi.Infrastructure.Finance;

/// <summary>Очередь чеков на распознавание в памяти процесса; после рестарта восстанавливается из БД (статус PENDING).</summary>
public class FinanceRecognitionQueue(IOptions<ReceiptRecognitionOptions> options) : IFinanceRecognitionQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    public bool IsEnabled => options.Value.IsEnabled;

    public ChannelReader<string> Reader => _channel.Reader;

    public void Enqueue(string receiptId)
    {
        if (IsEnabled)
            _channel.Writer.TryWrite(receiptId);
    }
}
