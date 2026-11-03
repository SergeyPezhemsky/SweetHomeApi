using Application.Modules.Wardrobe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SweetHomeApi.Infrastructure.Flatlay;

namespace SweetHomeApi.Tests.Wardrobe;

public class WardrobeFlatlayTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task UploadQueuesProcessingWhenEnabled()
    {
        var repository = new FakeWardrobeRepository();
        var item = repository.AddItem("item-1");
        var queue = new FakeQueue(enabled: true);
        var service = new WardrobeService(repository, imageQueue: queue);

        var result = await service.SetImageAsync(UserId, item.Id, [1, 2, 3], "image/jpeg");

        Assert.NotNull(result);
        Assert.Equal(WardrobeImageStatus.PENDING, result!.ImageStatus);
        Assert.Equal(WardrobeImageVariant.ORIGINAL, result.ImageVariant);
        Assert.Equal([item.Id], queue.Items);
    }

    [Fact]
    public async Task UploadWithoutFlatlayKeepsStatusNone()
    {
        var repository = new FakeWardrobeRepository();
        var item = repository.AddItem("item-1");
        var queue = new FakeQueue(enabled: false);
        var service = new WardrobeService(repository, imageQueue: queue);

        var result = await service.SetImageAsync(UserId, item.Id, [1, 2, 3], "image/jpeg");

        Assert.Equal(WardrobeImageStatus.NONE, result!.ImageStatus);
        Assert.Empty(queue.Items);
    }

    [Fact]
    public async Task ImageFallsBackToOriginalWhileProcessing()
    {
        var repository = new FakeWardrobeRepository();
        var item = repository.AddItem("item-1");
        var service = new WardrobeService(repository, imageQueue: new FakeQueue(enabled: true));
        await service.SetImageAsync(UserId, item.Id, [1], "image/png");
        item.ImageVariant = WardrobeImageVariant.PROCESSED; // выбранная версия ещё не готова

        var selected = await service.GetImageAsync(UserId, item.Id, null);
        var explicitProcessed = await service.GetImageAsync(UserId, item.Id, WardrobeImageVariant.PROCESSED);

        Assert.Equal(B(1), selected.Value!.Data);
        Assert.Null(explicitProcessed.Value);
    }

    [Fact]
    public async Task WorkerStoresResultAndSelectsProcessedVariant()
    {
        var repository = new FakeWardrobeRepository();
        var item = repository.AddItem("item-1");
        var service = new WardrobeService(repository, imageQueue: new FakeQueue(enabled: true));
        await service.SetImageAsync(UserId, item.Id, [1], "image/png");
        var worker = CreateWorker(repository, new FakeFlatlayClient(_ => Task.FromResult<byte[]?>([9, 9])));

        await worker.ProcessAsync(item.Id, CancellationToken.None);

        Assert.Equal(WardrobeImageStatus.READY, item.ImageStatus);
        Assert.Equal(WardrobeImageVariant.PROCESSED, item.ImageVariant);
        Assert.Equal(B(9, 9), (await service.GetImageAsync(UserId, item.Id, null)).Value!.Data);
        Assert.Equal(B(1), (await service.GetImageAsync(UserId, item.Id, WardrobeImageVariant.ORIGINAL)).Value!.Data);

        var original = await service.SetImageVariantAsync(UserId, item.Id, WardrobeImageVariant.ORIGINAL);
        Assert.Equal(WardrobeOperationStatus.Ok, original.Status);
        Assert.Equal(B(1), (await service.GetImageAsync(UserId, item.Id, null)).Value!.Data);
    }

    [Fact]
    public async Task ResultForReplacedPhotoIsDiscarded()
    {
        var repository = new FakeWardrobeRepository();
        var item = repository.AddItem("item-1");
        var service = new WardrobeService(repository, imageQueue: new FakeQueue(enabled: true));
        await service.SetImageAsync(UserId, item.Id, [1], "image/png");

        var worker = CreateWorker(repository, new FakeFlatlayClient(async _ =>
        {
            // Пока сервис обрабатывает старое фото, пользователь загружает новое.
            await service.SetImageAsync(UserId, item.Id, [2], "image/png");
            return [7];
        }));

        await worker.ProcessAsync(item.Id, CancellationToken.None);

        Assert.Equal(WardrobeImageStatus.PENDING, item.ImageStatus);
        Assert.Null((await service.GetImageAsync(UserId, item.Id, WardrobeImageVariant.PROCESSED)).Value);
        Assert.Equal(B(2), (await service.GetImageAsync(UserId, item.Id, null)).Value!.Data);
    }

    [Fact]
    public async Task ResultForDeletedAndReuploadedPhotoIsDiscarded()
    {
        var repository = new FakeWardrobeRepository();
        var item = repository.AddItem("item-1");
        var service = new WardrobeService(repository, imageQueue: new FakeQueue(enabled: true));
        await service.SetImageAsync(UserId, item.Id, B(1), "image/png");

        var worker = CreateWorker(repository, new FakeFlatlayClient(async _ =>
        {
            // Пока обрабатывается фото A, его удаляют и загружают B: строка фото создаётся заново.
            await service.DeleteImageAsync(UserId, item.Id);
            await service.SetImageAsync(UserId, item.Id, B(2), "image/png");
            return B(7);
        }));

        await worker.ProcessAsync(item.Id, CancellationToken.None);

        Assert.Equal(WardrobeImageStatus.PENDING, item.ImageStatus);
        Assert.Null((await service.GetImageAsync(UserId, item.Id, WardrobeImageVariant.PROCESSED)).Value);
        Assert.Equal(B(2), (await service.GetImageAsync(UserId, item.Id, null)).Value!.Data);
    }

    [Fact]
    public async Task ServerErrorIsNotRetried()
    {
        var repository = new FakeWardrobeRepository();
        var item = repository.AddItem("item-1");
        var service = new WardrobeService(repository, imageQueue: new FakeQueue(enabled: true));
        await service.SetImageAsync(UserId, item.Id, B(1), "image/png");
        var calls = 0;
        var client = new FakeFlatlayClient(_ =>
        {
            calls++;
            throw new HttpRequestException("boom", null, System.Net.HttpStatusCode.InternalServerError);
        });

        await CreateWorker(repository, client, retries: 3).ProcessAsync(item.Id, CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.Equal(WardrobeImageStatus.FAILED, item.ImageStatus);
    }

    [Fact]
    public async Task VariantWithoutPhotoReportsNoImage()
    {
        var repository = new FakeWardrobeRepository();
        var item = repository.AddItem("item-1");
        var service = new WardrobeService(repository, imageQueue: new FakeQueue(enabled: true));

        Assert.Equal(WardrobeOperationStatus.NoImage,
            (await service.SetImageVariantAsync(UserId, item.Id, WardrobeImageVariant.ORIGINAL)).Status);
    }

    [Fact]
    public async Task NothingFoundOrErrorMarksFailedAndKeepsOriginal()
    {
        var repository = new FakeWardrobeRepository();
        var empty = repository.AddItem("empty");
        var broken = repository.AddItem("broken");
        var service = new WardrobeService(repository, imageQueue: new FakeQueue(enabled: true));
        await service.SetImageAsync(UserId, empty.Id, [1], "image/png");
        await service.SetImageAsync(UserId, broken.Id, [2], "image/png");

        await CreateWorker(repository, new FakeFlatlayClient(_ => Task.FromResult<byte[]?>(null)))
            .ProcessAsync(empty.Id, CancellationToken.None);
        await CreateWorker(repository, new FakeFlatlayClient(_ => throw new HttpRequestException("boom", null, System.Net.HttpStatusCode.BadRequest)))
            .ProcessAsync(broken.Id, CancellationToken.None);

        Assert.Equal(WardrobeImageStatus.FAILED, empty.ImageStatus);
        Assert.Equal(WardrobeImageStatus.FAILED, broken.ImageStatus);
        Assert.Equal(WardrobeImageVariant.ORIGINAL, broken.ImageVariant);
        Assert.Equal(B(2), (await service.GetImageAsync(UserId, broken.Id, null)).Value!.Data);
        Assert.Equal(WardrobeOperationStatus.NotReady,
            (await service.SetImageVariantAsync(UserId, broken.Id, WardrobeImageVariant.PROCESSED)).Status);
    }

    [Fact]
    public async Task UnreachableServiceIsRetriedThenFails()
    {
        var repository = new FakeWardrobeRepository();
        var item = repository.AddItem("item-1");
        var service = new WardrobeService(repository, imageQueue: new FakeQueue(enabled: true));
        await service.SetImageAsync(UserId, item.Id, [1], "image/png");
        var calls = 0;
        var client = new FakeFlatlayClient(_ =>
        {
            calls++;
            throw new HttpRequestException("connection refused");
        });

        await CreateWorker(repository, client, retries: 2).ProcessAsync(item.Id, CancellationToken.None);

        Assert.Equal(3, calls);
        Assert.Equal(WardrobeImageStatus.FAILED, item.ImageStatus);
    }

    [Fact]
    public async Task ReprocessRequiresPhotoAndEnabledService()
    {
        var repository = new FakeWardrobeRepository();
        var item = repository.AddItem("item-1");
        var enabledQueue = new FakeQueue(enabled: true);

        var noPhoto = await new WardrobeService(repository, imageQueue: enabledQueue).ReprocessImageAsync(UserId, item.Id);
        Assert.Equal(WardrobeOperationStatus.NoImage, noPhoto.Status);

        await new WardrobeService(repository, imageQueue: enabledQueue).SetImageAsync(UserId, item.Id, [1], "image/png");
        await repository.MarkImageProcessingFailedAsync(item.Id, repository.Images[item.Id].Revision);

        var disabled = await new WardrobeService(repository, imageQueue: new FakeQueue(enabled: false)).ReprocessImageAsync(UserId, item.Id);
        Assert.Equal(WardrobeOperationStatus.Disabled, disabled.Status);

        var retried = await new WardrobeService(repository, imageQueue: enabledQueue).ReprocessImageAsync(UserId, item.Id);
        Assert.Equal(WardrobeOperationStatus.Ok, retried.Status);
        Assert.Equal(WardrobeImageStatus.PENDING, item.ImageStatus);

        var missing = await new WardrobeService(repository, imageQueue: enabledQueue).ReprocessImageAsync(UserId, "nope");
        Assert.Equal(WardrobeOperationStatus.NotFound, missing.Status);
    }

    private static byte[] B(params byte[] bytes) => bytes;

    private static WardrobeImageProcessingWorker CreateWorker(FakeWardrobeRepository repository, IFlatlayClient client, int retries = 0)
    {
        var options = Options.Create(new FlatlayOptions { BaseUrl = "http://flatlay", ConnectRetries = retries, RetryDelaySeconds = 0 });
        var services = new ServiceCollection()
            .AddSingleton<IWardrobeRepository>(repository)
            .AddSingleton(client)
            .BuildServiceProvider();

        return new WardrobeImageProcessingWorker(
            new WardrobeImageQueue(options),
            services.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<WardrobeImageProcessingWorker>.Instance);
    }

    private sealed class FakeQueue(bool enabled) : IWardrobeImageQueue
    {
        public List<string> Items { get; } = [];
        public bool IsEnabled => enabled;
        public void Enqueue(string itemId) => Items.Add(itemId);
    }

    private sealed class FakeFlatlayClient(Func<byte[], Task<byte[]?>> process) : IFlatlayClient
    {
        public Task<byte[]?> ProcessAsync(byte[] image, string contentType, CancellationToken ct) => process(image);
    }

    /// <summary>Хранилище в памяти, повторяющее правила ревизий репозитория на PostgreSQL.</summary>
    private sealed class FakeWardrobeRepository : IWardrobeRepository
    {
        public Dictionary<string, WardrobeItem> Items { get; } = [];
        public Dictionary<string, WardrobeItemImage> Images { get; } = [];
        private int _revisionSequence;

        public WardrobeItem AddItem(string id)
        {
            var item = new WardrobeItem { Id = id, UserId = UserId, Name = id };
            Items[id] = item;
            return item;
        }

        public Task<WardrobeItem?> GetItemAsync(string userId, string itemId) =>
            Task.FromResult(Items.TryGetValue(itemId, out var item) && item.UserId == userId ? item : null);

        public Task<WardrobeItem?> GetItemAsNoTrackingAsync(string userId, string itemId) => GetItemAsync(userId, itemId);

        public Task<WardrobeImageContent?> GetImageAsync(string itemId, WardrobeImageVariant variant)
        {
            if (!Images.TryGetValue(itemId, out var image))
                return Task.FromResult<WardrobeImageContent?>(null);

            return Task.FromResult(variant == WardrobeImageVariant.ORIGINAL
                ? new WardrobeImageContent(image.Data, image.ContentType)
                : image.ProcessedData is null ? null : new WardrobeImageContent(image.ProcessedData, image.ProcessedContentType!));
        }

        public Task<bool> SetImageAsync(WardrobeItem item, byte[] data, string contentType, bool queueProcessing)
        {
            var status = queueProcessing ? WardrobeImageStatus.PENDING : WardrobeImageStatus.NONE;
            var revision = ++_revisionSequence;
            Images[item.Id] = new WardrobeItemImage
            {
                ItemId = item.Id,
                Data = data,
                ContentType = contentType,
                ProcessingStatus = status,
                Revision = revision
            };
            item.HasImage = true;
            item.ImageStatus = status;
            item.ImageVariant = WardrobeImageVariant.ORIGINAL;
            return Task.FromResult(true);
        }

        public Task DeleteImageAsync(WardrobeItem item)
        {
            Images.Remove(item.Id);
            item.HasImage = false;
            item.ImageStatus = WardrobeImageStatus.NONE;
            item.ImageVariant = WardrobeImageVariant.ORIGINAL;
            return Task.CompletedTask;
        }

        public Task<bool> SetImageVariantAsync(WardrobeItem item, WardrobeImageVariant variant)
        {
            if (!Images.TryGetValue(item.Id, out var image) ||
                (variant == WardrobeImageVariant.PROCESSED && image.ProcessedData is null))
                return Task.FromResult(false);

            item.ImageVariant = variant;
            return Task.FromResult(true);
        }

        public Task<bool> MarkImagePendingAsync(WardrobeItem item)
        {
            if (!Images.TryGetValue(item.Id, out var image))
                return Task.FromResult(false);
            if (image.ProcessingStatus == WardrobeImageStatus.PENDING)
                return Task.FromResult(true);

            image.Revision = ++_revisionSequence;
            image.ProcessingStatus = WardrobeImageStatus.PENDING;
            item.ImageStatus = WardrobeImageStatus.PENDING;
            return Task.FromResult(true);
        }

        public Task<List<string>> GetPendingImageItemIdsAsync() =>
            Task.FromResult(Images.Values.Where(x => x.ProcessingStatus == WardrobeImageStatus.PENDING).Select(x => x.ItemId).ToList());

        public Task<WardrobePendingImage?> GetPendingImageAsync(string itemId) =>
            Task.FromResult(Images.TryGetValue(itemId, out var image) && image.ProcessingStatus == WardrobeImageStatus.PENDING
                ? new WardrobePendingImage(itemId, image.Data, image.ContentType, image.Revision)
                : null);

        public Task<bool> SaveProcessedImageAsync(string itemId, int revision, byte[] data, string contentType)
        {
            if (!Images.TryGetValue(itemId, out var image) || image.Revision != revision ||
                image.ProcessingStatus != WardrobeImageStatus.PENDING)
                return Task.FromResult(false);

            image.ProcessedData = data;
            image.ProcessedContentType = contentType;
            image.ProcessingStatus = WardrobeImageStatus.READY;
            Items[itemId].ImageStatus = WardrobeImageStatus.READY;
            Items[itemId].ImageVariant = WardrobeImageVariant.PROCESSED;
            return Task.FromResult(true);
        }

        public Task MarkImageProcessingFailedAsync(string itemId, int revision)
        {
            if (Images.TryGetValue(itemId, out var image) && image.Revision == revision &&
                image.ProcessingStatus == WardrobeImageStatus.PENDING)
            {
                image.ProcessingStatus = WardrobeImageStatus.FAILED;
                Items[itemId].ImageStatus = WardrobeImageStatus.FAILED;
            }

            return Task.CompletedTask;
        }

        public Task<List<WardrobeItem>> GetItemsAsync(string userId, WardrobeItemQuery query) => throw new NotSupportedException();
        public Task<Dictionary<WardrobeCategory, int>> GetCategoryCountsAsync(string userId) => throw new NotSupportedException();
        public Task<List<WardrobeItem>> GetAllItemsAsync(string userId) => throw new NotSupportedException();
        public Task<List<WardrobeItem>> GetItemsByIdsAsync(string userId, IReadOnlyCollection<string> itemIds) => throw new NotSupportedException();
        public Task<List<WardrobeItem>> GetItemsByIdsAsNoTrackingAsync(string userId, IReadOnlyCollection<string> itemIds) => throw new NotSupportedException();
        public Task<WardrobeOutfit?> GetOutfitAsNoTrackingAsync(string userId, string outfitId) => throw new NotSupportedException();
        public Task AddItemAsync(WardrobeItem item) => throw new NotSupportedException();
        public Task<bool> UpdateItemAsync(WardrobeItem item) => throw new NotSupportedException();
        public Task DeleteItemAsync(WardrobeItem item) => throw new NotSupportedException();
        public Task<List<WardrobeOutfit>> GetOutfitsAsync(string userId) => throw new NotSupportedException();
        public Task<WardrobeOutfit?> GetOutfitAsync(string userId, string outfitId) => throw new NotSupportedException();
        public Task<bool> AddOutfitAsync(WardrobeOutfit outfit) => throw new NotSupportedException();
        public Task DeleteOutfitAsync(WardrobeOutfit outfit) => throw new NotSupportedException();
        public Task<WardrobeOperationStatus> UpdateOutfitAsync(WardrobeOutfit outfit) => throw new NotSupportedException();
        public Task MarkItemsWornAsync(string userId, IReadOnlyCollection<string> itemIds, DateTime wornAt) => throw new NotSupportedException();
        public Task MarkOutfitWornAsync(string userId, string outfitId, DateTime wornAt) => throw new NotSupportedException();
    }
}
