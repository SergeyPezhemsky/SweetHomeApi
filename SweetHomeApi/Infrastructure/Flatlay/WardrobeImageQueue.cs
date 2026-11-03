using System.Threading.Channels;
using Application.Modules.Wardrobe;
using Microsoft.Extensions.Options;

namespace SweetHomeApi.Infrastructure.Flatlay;

/// <summary>Очередь фото на удаление фона в памяти процесса; после рестарта восстанавливается из БД (статус PENDING).</summary>
public class WardrobeImageQueue(IOptions<FlatlayOptions> options) : IWardrobeImageQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.BaseUrl);

    public ChannelReader<string> Reader => _channel.Reader;

    public void Enqueue(string itemId)
    {
        if (IsEnabled)
            _channel.Writer.TryWrite(itemId);
    }
}
