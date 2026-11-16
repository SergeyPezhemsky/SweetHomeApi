using Application.Modules.Finance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SweetHomeApi.Infrastructure.Finance;

namespace SweetHomeApi.Tests.Finance;

public class FinanceServiceTests
{
    private const string UserId = "user-1";
    private static readonly DateOnly Today = new(2026, 11, 15);

    private const string GroceryReceipt = """
        {"isReceipt": true, "store": "Пятёрочка", "date": "2026-11-14", "currency": "RUB", "total": 900,
         "items": [
           {"name": "Корм Purina Pro Plan 1,5 кг", "amount": 600, "category": "GROCERIES"},
           {"name": "Губки для посуды", "amount": 400, "category": "HOME"}
         ]}
        """;

    private const string BankScreenshot = """
        {"purchases": [
          {"store":"Кофейня","date":"01.11 12:30","total":"250,50","category":"CAFE"},
          {"store":"Кофейня","date":"01.11 12:30","total":"250,50","category":"CAFE"},
          {"store":"Магазин","date":"31.10 21:43","total":800,"category":"GROCERIES"}
        ]}
        """;

    [Fact]
    public async Task UploadQueuesRecognitionWhenEnabled()
    {
        var repository = new FakeFinanceRepository();
        var queue = new FakeRecognitionQueue(enabled: true);
        var service = new FinanceService(repository, queue);

        var receipt = await service.UploadReceiptAsync(UserId, [1, 2], "image/jpeg", Today);

        Assert.Equal(FinanceReceiptStatus.PENDING, receipt.Status);
        Assert.Equal(FinanceReceiptSource.PHOTO, receipt.Source);
        Assert.Equal([receipt.Id], queue.Items);
        Assert.True(repository.Images.ContainsKey(receipt.Id));
    }

    [Fact]
    public async Task UploadWithoutRecognitionKeepsPhotoAsFailedDisabled()
    {
        var repository = new FakeFinanceRepository();
        var queue = new FakeRecognitionQueue(enabled: false);
        var service = new FinanceService(repository, queue);

        var receipt = await service.UploadReceiptAsync(UserId, [1], "image/png", Today);

        Assert.Equal(FinanceReceiptStatus.FAILED, receipt.Status);
        Assert.Equal(FinanceFailureReason.DISABLED, receipt.FailureReason);
        Assert.Empty(queue.Items);
        Assert.True(repository.Images.ContainsKey(receipt.Id));
    }

    [Fact]
    public async Task WorkerStoresRecognizedReceipt()
    {
        var (repository, service, receipt) = await UploadAsync();
        var worker = CreateWorker(repository, new FakeRecognizer(_ => Task.FromResult(GroceryReceipt)));

        await worker.ProcessAsync(receipt.Id, CancellationToken.None);

        var stored = repository.Receipts[receipt.Id];
        Assert.Equal(FinanceReceiptStatus.READY, stored.Status);
        Assert.Equal("Пятёрочка", stored.Store);
        Assert.Equal(new DateOnly(2026, 11, 14), stored.PurchasedOn);
        Assert.Equal(900m, stored.Total);
        Assert.Equal(FinanceCategory.GROCERIES, stored.Category);
        Assert.Equal(2, stored.Items.Count);
        Assert.All(stored.Items, x => Assert.Equal(FinanceCategorySource.AI, x.CategorySource));
        Assert.NotNull(stored.RecognitionJson);

        var summary = await service.GetSummaryAsync(UserId, new FinanceMonth(2026, 11));
        Assert.Equal(900m, summary.Total);
        Assert.Equal(540m, summary.Categories.Single(x => x.Category == FinanceCategory.GROCERIES).Amount);
        Assert.Equal(360m, summary.Categories.Single(x => x.Category == FinanceCategory.HOME).Amount);
    }

    [Fact]
    public async Task WorkerStoresBankPurchasesSeparatelyInTheirMonthsWithoutDuplicatingBatch()
    {
        var (repository, service, upload) = await UploadAsync();
        var recognizer = new FakeRecognizer(_ => Task.FromResult(BankScreenshot));
        var worker = CreateWorker(repository, recognizer);

        await worker.ProcessAsync(upload.Id, CancellationToken.None);
        await worker.ProcessAsync(upload.Id, CancellationToken.None);
        var retry = await service.RecognizeAgainAsync(UserId, upload.Id);

        Assert.Equal(1, recognizer.Calls);
        Assert.Equal(FinanceOperationStatus.AlreadyImported, retry.Status);
        Assert.Equal(3, repository.Receipts.Count);
        Assert.Single(repository.Images);
        Assert.True(repository.Receipts[upload.Id].HasImage);
        Assert.All(repository.Receipts.Values, purchase =>
        {
            Assert.Equal(UserId, purchase.UserId);
            Assert.Equal(FinanceReceiptSource.BANK_SCREENSHOT, purchase.Source);
            Assert.Equal(FinanceReceiptStatus.READY, purchase.Status);
            Assert.Equal(recognizer.Model, purchase.RecognitionModel);
            Assert.Empty(purchase.Items);
        });

        var november = await service.GetSummaryAsync(UserId, new FinanceMonth(2026, 11));
        Assert.Equal(2, november.ReceiptCount);
        Assert.Equal(501m, november.Total);
        Assert.Equal(FinanceCategory.CAFE, Assert.Single(november.Categories).Category);
        var october = await service.GetSummaryAsync(UserId, new FinanceMonth(2026, 10));
        Assert.Equal(1, october.ReceiptCount);
        Assert.Equal(800m, october.Total);
        Assert.Equal(FinanceCategory.GROCERIES, Assert.Single(october.Categories).Category);
    }

    [Theory]
    [InlineData("""{"purchases":[{"store":"Кофейня","total":250},{"store":"Магазин","total":""")]
    [InlineData("""{"purchases":[{"store":"Кофейня","total":250},{"store":"Магазин"}]}""")]
    public async Task WorkerDoesNotImportPartialBankScreenshot(string response)
    {
        var (repository, _, upload) = await UploadAsync();
        var worker = CreateWorker(repository, new FakeRecognizer(_ => Task.FromResult(response)));

        await worker.ProcessAsync(upload.Id, CancellationToken.None);

        var stored = Assert.Single(repository.Receipts.Values);
        Assert.Equal(FinanceReceiptStatus.FAILED, stored.Status);
        Assert.Equal(FinanceFailureReason.UNREADABLE, stored.FailureReason);
        Assert.Equal(0m, stored.Total);
    }

    [Fact]
    public async Task EditDuringBankRecognitionDiscardsAllPurchases()
    {
        var (repository, service, upload) = await UploadAsync();
        var worker = CreateWorker(repository, new FakeRecognizer(async _ =>
        {
            await service.UpdateReceiptAsync(UserId, upload.Id, new FinanceReceiptUpdate
            {
                PurchasedOn = Today,
                Total = 42m,
                Category = FinanceCategory.CAFE
            });
            return BankScreenshot;
        }));

        await worker.ProcessAsync(upload.Id, CancellationToken.None);

        Assert.Equal(42m, Assert.Single(repository.Receipts.Values).Total);
    }

    [Fact]
    public async Task CategoryCorrectionIsLearnedAndAppliedToNextReceipt()
    {
        var (repository, service, receipt) = await UploadAsync();
        var worker = CreateWorker(repository, new FakeRecognizer(_ => Task.FromResult(GroceryReceipt)));
        await worker.ProcessAsync(receipt.Id, CancellationToken.None);

        var loaded = (await service.GetReceiptAsync(UserId, receipt.Id))!;
        var updated = await service.UpdateReceiptAsync(UserId, receipt.Id, ToUpdate(loaded, items =>
        {
            items[0].Category = FinanceCategory.PETS;
        }));

        Assert.Equal(FinanceCategorySource.USER, updated!.Items[0].CategorySource);
        Assert.Equal(FinanceCategorySource.AI, updated.Items[1].CategorySource);
        Assert.Equal(FinanceCategory.PETS, repository.Rules[(UserId, "корм purina")]);

        var second = await service.UploadReceiptAsync(UserId, [3], "image/jpeg", Today);
        await CreateWorker(repository, new FakeRecognizer(_ => Task.FromResult("""
            {"total": 500, "items": [{"name": "КОРМ PURINA для щенков 3кг", "amount": 500, "category": "GROCERIES"}]}
            """))).ProcessAsync(second.Id, CancellationToken.None);

        var item = repository.Receipts[second.Id].Items.Single();
        Assert.Equal(FinanceCategory.PETS, item.Category);
        Assert.Equal(FinanceCategorySource.RULE, item.CategorySource);
        Assert.Equal(FinanceCategory.PETS, repository.Receipts[second.Id].Category);
    }

    [Fact]
    public async Task RulesAreIsolatedPerUser()
    {
        var repository = new FakeFinanceRepository();
        repository.Rules[("other-user", "корм purina")] = FinanceCategory.PETS;
        var service = new FinanceService(repository, new FakeRecognitionQueue(enabled: true));
        var receipt = await service.UploadReceiptAsync(UserId, [1], "image/jpeg", Today);

        await CreateWorker(repository, new FakeRecognizer(_ => Task.FromResult(GroceryReceipt))).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(FinanceCategory.GROCERIES, repository.Receipts[receipt.Id].Items[0].Category);
    }

    [Fact]
    public async Task EditDuringRecognitionDiscardsResult()
    {
        var (repository, service, receipt) = await UploadAsync();
        var worker = CreateWorker(repository, new FakeRecognizer(async _ =>
        {
            // Пока модель думает, пользователь сам вводит сумму.
            await service.UpdateReceiptAsync(UserId, receipt.Id, new FinanceReceiptUpdate
            {
                PurchasedOn = Today,
                Total = 42m,
                Category = FinanceCategory.CAFE
            });
            return GroceryReceipt;
        }));

        await worker.ProcessAsync(receipt.Id, CancellationToken.None);

        var stored = repository.Receipts[receipt.Id];
        Assert.Equal(FinanceReceiptStatus.READY, stored.Status);
        Assert.Equal(42m, stored.Total);
        Assert.Equal(FinanceCategory.CAFE, stored.Category);
        Assert.Empty(stored.Items);
    }

    [Theory]
    [InlineData("""{"isReceipt": false}""", FinanceFailureReason.NOT_A_RECEIPT)]
    [InlineData("не могу прочитать", FinanceFailureReason.UNREADABLE)]
    [InlineData("""{"store": "Без суммы", "items": []}""", FinanceFailureReason.UNREADABLE)]
    public async Task BadAnswersFailWithReason(string answer, FinanceFailureReason reason)
    {
        var (repository, _, receipt) = await UploadAsync();

        await CreateWorker(repository, new FakeRecognizer(_ => Task.FromResult(answer))).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(FinanceReceiptStatus.FAILED, repository.Receipts[receipt.Id].Status);
        Assert.Equal(reason, repository.Receipts[receipt.Id].FailureReason);
    }

    [Fact]
    public async Task UnavailableProviderIsRetriedThenSucceeds()
    {
        var (repository, _, receipt) = await UploadAsync();
        var recognizer = new FakeRecognizer(call => call < 3
            ? throw new ReceiptRecognizerUnavailableException("503")
            : Task.FromResult(GroceryReceipt));

        await CreateWorker(repository, recognizer, retries: 3).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(3, recognizer.Calls);
        Assert.Equal(FinanceReceiptStatus.READY, repository.Receipts[receipt.Id].Status);
    }

    [Fact]
    public async Task ProviderErrorIsNotRetried()
    {
        var (repository, _, receipt) = await UploadAsync();
        var recognizer = new FakeRecognizer(_ => throw new ReceiptRecognizerException("401"));

        await CreateWorker(repository, recognizer, retries: 3).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(1, recognizer.Calls);
        Assert.Equal(FinanceFailureReason.PROVIDER_ERROR, repository.Receipts[receipt.Id].FailureReason);
    }

    [Fact]
    public async Task ExhaustedRetriesFailWithProviderError()
    {
        var (repository, _, receipt) = await UploadAsync();
        var recognizer = new FakeRecognizer(_ => throw new ReceiptRecognizerUnavailableException("timeout"));

        await CreateWorker(repository, recognizer, retries: 2).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(3, recognizer.Calls);
        Assert.Equal(FinanceFailureReason.PROVIDER_ERROR, repository.Receipts[receipt.Id].FailureReason);
    }

    [Fact]
    public void ImplausibleDateFallsBackToUploadDate()
    {
        var future = FinanceService.BuildRecognitionResult(
            new RecognizedReceipt { IsReceipt = true, Total = 10, Date = Today.AddDays(5) }, Today, new Dictionary<string, FinanceCategory>());
        var ancient = FinanceService.BuildRecognitionResult(
            new RecognizedReceipt { IsReceipt = true, Total = 10, Date = new DateOnly(2019, 1, 1) }, Today, new Dictionary<string, FinanceCategory>());
        var tomorrow = FinanceService.BuildRecognitionResult(
            new RecognizedReceipt { IsReceipt = true, Total = 10, Date = Today.AddDays(1) }, Today, new Dictionary<string, FinanceCategory>());

        Assert.Equal(Today, future.Result!.PurchasedOn);
        Assert.Equal(Today, ancient.Result!.PurchasedOn);
        Assert.Equal(Today.AddDays(1), tomorrow.Result!.PurchasedOn);
    }

    [Fact]
    public void RecognizedDateNeverExceedsTomorrowUtc()
    {
        var (result, _) = FinanceService.BuildRecognitionResult(
            new RecognizedReceipt { IsReceipt = true, Total = 10, Date = Today.AddDays(2) }, Today.AddDays(1),
            new Dictionary<string, FinanceCategory>(), latestDate: Today.AddDays(1));

        Assert.Equal(Today.AddDays(1), result!.PurchasedOn);
    }

    [Fact]
    public void MissingDateKeepsCurrentReceiptDate()
    {
        var edited = new DateOnly(2026, 11, 2);
        var (result, _) = FinanceService.BuildRecognitionResult(
            new RecognizedReceipt { IsReceipt = true, Total = 10 }, Today, new Dictionary<string, FinanceCategory>(), currentDate: edited);

        Assert.Equal(edited, result!.PurchasedOn);
    }

    [Fact]
    public void RepairedAnswerWithoutTotalIsUnreadable()
    {
        var parsed = ReceiptRecognitionParser.Parse("""{"isReceipt":true,"items":[{"name":"A","amount":89.9},{"name":"B","amount":45},{"name":"C","am""");
        var (result, failure) = FinanceService.BuildRecognitionResult(parsed, Today, new Dictionary<string, FinanceCategory>());

        Assert.Null(result);
        Assert.Equal(FinanceFailureReason.UNREADABLE, failure);
    }

    [Fact]
    public async Task DatabaseErrorBetweenRetriesRequeuesReceipt()
    {
        var (repository, _, receipt) = await UploadAsync();
        repository.FailPendingLookupAfter = 1;
        var options = Options.Create(new ReceiptRecognitionOptions
        {
            ApiKey = "key", Project = "folder", Retries = 2, RetryDelaySeconds = 0, InternalRetryDelaySeconds = 1
        });
        var queue = new FinanceRecognitionQueue(options);
        var worker = new FinanceRecognitionWorker(queue, CreateScopeFactory(repository,
                new FakeRecognizer(_ => throw new ReceiptRecognizerUnavailableException("503"))), options,
            NullLogger<FinanceRecognitionWorker>.Instance);

        await worker.ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(FinanceReceiptStatus.PENDING, repository.Receipts[receipt.Id].Status);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(receipt.Id, await queue.Reader.ReadAsync(timeout.Token));
    }

    [Fact]
    public void TotalFallsBackToItemsSum()
    {
        var (result, _) = FinanceService.BuildRecognitionResult(new RecognizedReceipt
        {
            IsReceipt = true,
            Items = [new RecognizedItem("Кофе", 1, 250, 250, FinanceCategory.CAFE), new RecognizedItem("Круассан", 1, 150, 150, FinanceCategory.CAFE)]
        }, Today, new Dictionary<string, FinanceCategory>());

        Assert.Equal(400m, result!.Total);
        Assert.Equal(FinanceCategory.CAFE, result.Category);
    }

    [Fact]
    public async Task SummaryCountsOnlyReadyRublesAndComparesWithPreviousMonth()
    {
        var repository = new FakeFinanceRepository();
        var service = new FinanceService(repository, new FakeRecognitionQueue(enabled: true));
        await Manual(service, 1000m, FinanceCategory.CAFE, new DateOnly(2026, 11, 3));
        await Manual(service, 500m, FinanceCategory.TRANSPORT, new DateOnly(2026, 11, 30));
        await Manual(service, 300m, FinanceCategory.TRAVEL, new DateOnly(2026, 11, 10), currency: "EUR");
        await Manual(service, 700m, FinanceCategory.CAFE, new DateOnly(2026, 10, 31));
        await Manual(service, 999m, FinanceCategory.CAFE, new DateOnly(2026, 12, 1));
        await service.UploadReceiptAsync(UserId, [1], "image/jpeg", new DateOnly(2026, 11, 5));
        await Manual(service, 50m, FinanceCategory.CAFE, new DateOnly(2026, 11, 3), userId: "someone-else");

        var summary = await service.GetSummaryAsync(UserId, new FinanceMonth(2026, 11));

        Assert.Equal(1500m, summary.Total);
        Assert.Equal(2, summary.ReceiptCount);
        Assert.Equal(1, summary.PendingCount);
        Assert.Equal(1, summary.OtherCurrencyCount);
        Assert.Equal(700m, summary.PreviousTotal);
        Assert.Equal([FinanceCategory.CAFE, FinanceCategory.TRANSPORT], summary.Categories.Select(x => x.Category));
        Assert.Equal(summary.Total, summary.Categories.Sum(x => x.Amount));
    }

    [Fact]
    public async Task ReceiptListFiltersByAllocatedCategory()
    {
        var (repository, service, receipt) = await UploadAsync();
        await CreateWorker(repository, new FakeRecognizer(_ => Task.FromResult(GroceryReceipt))).ProcessAsync(receipt.Id, CancellationToken.None);
        await Manual(service, 100m, FinanceCategory.CAFE, Today);

        var month = new FinanceMonth(2026, 11);
        var all = await service.GetReceiptsAsync(UserId, month, null);
        var home = await service.GetReceiptsAsync(UserId, month, FinanceCategory.HOME);

        Assert.Equal(2, all.Count);
        Assert.Equal(receipt.Id, Assert.Single(home).Receipt.Id);
        Assert.Equal(360m, home[0].CategoryAmount);
        Assert.Equal([FinanceCategory.GROCERIES, FinanceCategory.HOME], all.Single(x => x.Receipt.Id == receipt.Id).TopCategories);
    }

    [Fact]
    public async Task RecognizeAgainRequiresPhotoAndEnabledRecognition()
    {
        var repository = new FakeFinanceRepository();
        var queue = new FakeRecognitionQueue(enabled: true);
        var service = new FinanceService(repository, queue);
        var manual = await Manual(service, 10m, FinanceCategory.OTHER, Today);
        var photo = await service.UploadReceiptAsync(UserId, [1], "image/jpeg", Today);

        Assert.Equal(FinanceOperationStatus.NoImage, (await service.RecognizeAgainAsync(UserId, manual.Id)).Status);
        Assert.Equal(FinanceOperationStatus.NotFound, (await service.RecognizeAgainAsync("intruder", photo.Id)).Status);

        var again = await service.RecognizeAgainAsync(UserId, photo.Id);
        Assert.Equal(FinanceOperationStatus.Ok, again.Status);
        Assert.Equal(FinanceReceiptStatus.PENDING, again.Value!.Status);
        Assert.NotEqual(photo.Revision, again.Value.Revision);

        var disabled = new FinanceService(repository, new FakeRecognitionQueue(enabled: false));
        Assert.Equal(FinanceOperationStatus.Disabled, (await disabled.RecognizeAgainAsync(UserId, photo.Id)).Status);
    }

    [Fact]
    public async Task ForeignReceiptIsInvisible()
    {
        var (_, service, receipt) = await UploadAsync();

        Assert.Null(await service.GetReceiptAsync("intruder", receipt.Id));
        Assert.Null(await service.GetImageAsync("intruder", receipt.Id));
        Assert.Null(await service.UpdateReceiptAsync("intruder", receipt.Id, new FinanceReceiptUpdate { Total = 1, PurchasedOn = Today }));
        Assert.False(await service.DeleteReceiptAsync("intruder", receipt.Id));
        Assert.NotNull(await service.GetReceiptAsync(UserId, receipt.Id));
    }

    [Fact]
    public async Task DisabledWorkerFailsLeftoverPendingReceipts()
    {
        var (repository, _, receipt) = await UploadAsync();
        var options = Options.Create(new ReceiptRecognitionOptions { ApiKey = "" });
        var worker = new FinanceRecognitionWorker(new FinanceRecognitionQueue(options), CreateScopeFactory(repository, null), options,
            NullLogger<FinanceRecognitionWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        Assert.Equal(FinanceFailureReason.DISABLED, repository.Receipts[receipt.Id].FailureReason);
    }

    [Fact]
    public async Task RecognizeAgainCanFixDateMisreadIntoThePast()
    {
        var (repository, service, receipt) = await UploadAsync();
        await CreateWorker(repository, new FakeRecognizer(_ => Task.FromResult("""{"total": 100, "date": "2025-11-14"}""")))
            .ProcessAsync(receipt.Id, CancellationToken.None);
        Assert.Equal(new DateOnly(2025, 11, 14), repository.Receipts[receipt.Id].PurchasedOn);

        await service.RecognizeAgainAsync(UserId, receipt.Id);
        await CreateWorker(repository, new FakeRecognizer(_ => Task.FromResult("""{"total": 100, "date": "2026-11-14"}""")))
            .ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 14), repository.Receipts[receipt.Id].PurchasedOn);
    }

    [Fact]
    public async Task CategoryListMatchesSummaryAndSkipsOtherCurrencies()
    {
        var repository = new FakeFinanceRepository();
        var service = new FinanceService(repository, new FakeRecognitionQueue(enabled: true));
        await Manual(service, 50m, FinanceCategory.TRAVEL, Today, currency: "USD");
        await Manual(service, 700m, FinanceCategory.TRAVEL, Today);

        var list = await service.GetReceiptsAsync(UserId, new FinanceMonth(2026, 11), FinanceCategory.TRAVEL);
        var summary = await service.GetSummaryAsync(UserId, new FinanceMonth(2026, 11));

        Assert.Equal(700m, Assert.Single(list).CategoryAmount);
        Assert.Equal(700m, summary.Categories.Single(x => x.Category == FinanceCategory.TRAVEL).Amount);
    }

    [Fact]
    public async Task SaveErrorIsReportedAsInternalNotProvider()
    {
        var (repository, _, receipt) = await UploadAsync();
        repository.FailNextComplete = true;

        await CreateWorker(repository, new FakeRecognizer(_ => Task.FromResult(GroceryReceipt))).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(FinanceFailureReason.INTERNAL_ERROR, repository.Receipts[receipt.Id].FailureReason);
        Assert.NotNull(repository.Receipts[receipt.Id].RecognitionJson);
    }

    private static async Task<(FakeFinanceRepository Repository, FinanceService Service, FinanceReceipt Receipt)> UploadAsync()
    {
        var repository = new FakeFinanceRepository();
        var service = new FinanceService(repository, new FakeRecognitionQueue(enabled: true));
        var receipt = await service.UploadReceiptAsync(UserId, [1, 2, 3], "image/jpeg", Today);
        return (repository, service, receipt);
    }

    private static Task<FinanceReceipt> Manual(FinanceService service, decimal total, FinanceCategory category, DateOnly date,
        string currency = "RUB", string userId = UserId)
    {
        return service.CreateManualReceiptAsync(userId, new FinanceReceiptUpdate
        {
            PurchasedOn = date,
            Total = total,
            Category = category,
            Currency = currency
        });
    }

    private static FinanceReceiptUpdate ToUpdate(FinanceReceipt receipt, Action<List<FinanceReceiptItemUpdate>> change)
    {
        var items = receipt.Items.Select(x => new FinanceReceiptItemUpdate
        {
            Id = x.Id,
            Name = x.Name,
            Quantity = x.Quantity,
            Price = x.Price,
            Amount = x.Amount,
            Category = x.Category
        }).ToList();
        change(items);

        return new FinanceReceiptUpdate
        {
            PurchasedOn = receipt.PurchasedOn,
            Store = receipt.Store,
            Total = receipt.Total,
            Currency = receipt.Currency,
            Category = receipt.Category,
            Note = receipt.Note,
            Items = items
        };
    }

    private static FinanceRecognitionWorker CreateWorker(FakeFinanceRepository repository, IReceiptRecognizer recognizer, int retries = 0)
    {
        var options = Options.Create(new ReceiptRecognitionOptions { ApiKey = "key", Project = "folder", Retries = retries, RetryDelaySeconds = 0 });
        return new FinanceRecognitionWorker(
            new FinanceRecognitionQueue(options),
            CreateScopeFactory(repository, recognizer),
            options,
            NullLogger<FinanceRecognitionWorker>.Instance);
    }

    private static IServiceScopeFactory CreateScopeFactory(FakeFinanceRepository repository, IReceiptRecognizer? recognizer)
    {
        var services = new ServiceCollection()
            .AddSingleton<IFinanceRepository>(repository)
            .AddScoped<IFinanceService>(sp => new FinanceService(sp.GetRequiredService<IFinanceRepository>()));
        if (recognizer is not null)
            services.AddSingleton(recognizer);

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
