using System.Net;
using System.Text;
using System.Text.Json;
using Application.Modules.Finance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SweetHomeApi.Infrastructure.Finance;

namespace SweetHomeApi.Tests.Finance;

public class FinanceProvidersTests
{
    private const string UserId = "user-1";
    private static readonly DateOnly Today = new(2026, 11, 15);

    private const string Receipt = """{"isReceipt": true, "store": "Магнит", "total": 500, "items": [{"name": "Сыр", "amount": 500, "category": "GROCERIES"}]}""";

    [Fact]
    public async Task UnreadableAnswerFallsBackToNextProvider()
    {
        var (repository, receipt) = await UploadAsync();
        var yandex = new FakeRecognizer(_ => Task.FromResult("не могу прочитать"), "openai", "gpt://f/qwen");
        var claude = new FakeRecognizer(_ => Task.FromResult(Receipt), "anthropic", "claude-haiku-5-5");

        await CreateWorker(repository, [yandex, claude]).ProcessAsync(receipt.Id, CancellationToken.None);

        var stored = repository.Receipts[receipt.Id];
        Assert.Equal(FinanceReceiptStatus.READY, stored.Status);
        Assert.Equal(500m, stored.Total);
        Assert.Equal("claude-haiku-5-5", stored.RecognitionModel);
        Assert.Equal(1, yandex.Calls);
        Assert.Equal(1, claude.Calls);
    }

    [Fact]
    public async Task ProviderErrorFallsBackWithoutRetrying()
    {
        var (repository, receipt) = await UploadAsync();
        var yandex = new FakeRecognizer(_ => throw new ReceiptRecognizerException("401"), "openai");
        var claude = new FakeRecognizer(_ => Task.FromResult(Receipt), "anthropic", "claude-haiku-5-5");

        await CreateWorker(repository, [yandex, claude], retries: 3).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(FinanceReceiptStatus.READY, repository.Receipts[receipt.Id].Status);
        Assert.Equal(1, yandex.Calls);
    }

    [Fact]
    public async Task UnavailablePrimaryIsRetriedBeforeFallback()
    {
        var (repository, receipt) = await UploadAsync();
        var yandex = new FakeRecognizer(_ => throw new ReceiptRecognizerUnavailableException("503"), "openai");
        var claude = new FakeRecognizer(_ => Task.FromResult(Receipt), "anthropic");

        await CreateWorker(repository, [yandex, claude], retries: 2).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(3, yandex.Calls);
        Assert.Equal(1, claude.Calls);
        Assert.Equal(FinanceReceiptStatus.READY, repository.Receipts[receipt.Id].Status);
    }

    [Fact]
    public async Task WhenAllFailTheLastAnswerDecidesTheReason()
    {
        var (repository, receipt) = await UploadAsync();
        var yandex = new FakeRecognizer(_ => Task.FromResult("""{"isReceipt": false}"""), "openai", "qwen");
        var claude = new FakeRecognizer(_ => throw new ReceiptRecognizerException("403 region"), "anthropic", "claude");

        await CreateWorker(repository, [yandex, claude]).ProcessAsync(receipt.Id, CancellationToken.None);

        var stored = repository.Receipts[receipt.Id];
        Assert.Equal(FinanceFailureReason.NOT_A_RECEIPT, stored.FailureReason);
        Assert.Equal("qwen", stored.RecognitionModel);
        Assert.Contains("isReceipt", stored.RecognitionJson);
    }

    [Fact]
    public async Task NoProviderAnsweredIsProviderError()
    {
        var (repository, receipt) = await UploadAsync();
        var yandex = new FakeRecognizer(_ => throw new ReceiptRecognizerException("401"), "openai");
        var claude = new FakeRecognizer(_ => throw new ReceiptRecognizerException("403"), "anthropic");

        await CreateWorker(repository, [yandex, claude]).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(FinanceFailureReason.PROVIDER_ERROR, repository.Receipts[receipt.Id].FailureReason);
    }

    [Fact]
    public async Task OrderDecidesPrimaryAndExcludesUnlisted()
    {
        var (repository, receipt) = await UploadAsync();
        var yandex = new FakeRecognizer(_ => Task.FromResult(Receipt), "openai");
        var claude = new FakeRecognizer(_ => Task.FromResult(Receipt), "anthropic", "claude-sonnet-5-5");

        await CreateWorker(repository, [yandex, claude], order: "anthropic").ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(0, yandex.Calls);
        Assert.Equal("claude-sonnet-5-5", repository.Receipts[receipt.Id].RecognitionModel);
    }

    [Fact]
    public async Task EditBetweenProvidersStopsTheChain()
    {
        var (repository, receipt) = await UploadAsync();
        var service = new FinanceService(repository, new FakeRecognitionQueue(enabled: true));
        var yandex = new FakeRecognizer(async _ =>
        {
            await service.UpdateReceiptAsync(UserId, receipt.Id, new FinanceReceiptUpdate { PurchasedOn = Today, Total = 7, Category = FinanceCategory.CAFE });
            throw new ReceiptRecognizerException("500");
        }, "openai");
        var claude = new FakeRecognizer(_ => Task.FromResult(Receipt), "anthropic");

        await CreateWorker(repository, [yandex, claude]).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(0, claude.Calls);
        Assert.Equal(7m, repository.Receipts[receipt.Id].Total);
    }

    [Fact]
    public async Task DeletedReceiptIsNotSentToNextProvider()
    {
        var (repository, receipt) = await UploadAsync();
        var service = new FinanceService(repository, new FakeRecognitionQueue(enabled: true));
        var yandex = new FakeRecognizer(async _ =>
        {
            await service.DeleteReceiptAsync(UserId, receipt.Id);
            return "не могу прочитать";
        }, "openai");
        var claude = new FakeRecognizer(_ => Task.FromResult(Receipt), "anthropic");

        await CreateWorker(repository, [yandex, claude]).ProcessAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(0, claude.Calls);
    }

    [Theory]
    [InlineData("gpt://b1gfolder/qwen3-235b-a22b-fp8/latest", "qwen3-235b-a22b-fp8/latest")]
    [InlineData("gpt://b1gfolder/qwen3.6-35b-a3b", "qwen3.6-35b-a3b")]
    [InlineData("qwen3.6-35b-a3b", "qwen3.6-35b-a3b")]
    public void ModelIsStoredWithoutYandexFolder(string configured, string expected)
    {
        Assert.Equal(expected, OpenAiCompatibleReceiptRecognizer.StripFolder(configured));
    }

    [Fact]
    public void QueueIsEnabledByAnyListedProvider()
    {
        var anthropicOnly = new FinanceRecognitionQueue(Options.Create(new ReceiptRecognitionOptions()),
            Options.Create(new AnthropicRecognitionOptions { ApiKey = "k" }));
        var anthropicUnlisted = new FinanceRecognitionQueue(Options.Create(new ReceiptRecognitionOptions { Order = "openai" }),
            Options.Create(new AnthropicRecognitionOptions { ApiKey = "k" }));
        var none = new FinanceRecognitionQueue(Options.Create(new ReceiptRecognitionOptions()),
            Options.Create(new AnthropicRecognitionOptions()));

        Assert.True(anthropicOnly.IsEnabled);
        Assert.False(anthropicUnlisted.IsEnabled);
        Assert.False(none.IsEnabled);
    }

    [Fact]
    public void ProxyUrlParsing()
    {
        Assert.True(AnthropicRecognitionOptions.TryCreateProxy("http://user:p%40ss@10.0.0.1:3128", out var proxy));
        var web = Assert.IsType<WebProxy>(proxy);
        Assert.Equal(new Uri("http://10.0.0.1:3128/"), web.Address);
        var credentials = (NetworkCredential)web.Credentials!;
        Assert.Equal("user", credentials.UserName);
        Assert.Equal("p@ss", credentials.Password);
        Assert.True(AnthropicRecognitionOptions.TryCreateProxy("socks5://127.0.0.1:1080", out _));
        Assert.False(AnthropicRecognitionOptions.TryCreateProxy("ftp://x", out _));
        Assert.False(new AnthropicRecognitionOptions { ApiKey = "k", ProxyUrl = "not a url" }.IsEnabled);
    }

    [Fact]
    public async Task AnthropicRequestFollowsMessagesApi()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var handler = new StubHandler(async request =>
        {
            sent = request;
            body = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, """{"content":[{"type":"text","text":"{\"total\": 1"},{"type":"text","text":"}"}],"stop_reason":"end_turn"}""");
        });
        var recognizer = CreateAnthropic(handler);

        var text = await recognizer.RecognizeAsync([1, 2, 3], "image/jpeg", CancellationToken.None);

        Assert.Equal("""{"total": 1}""", text);
        Assert.Equal("https://api.anthropic.com/v1/messages", sent!.RequestUri!.ToString());
        Assert.Equal("secret", sent.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", sent.Headers.GetValues("anthropic-version").Single());
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("claude-haiku-5-5", json.RootElement.GetProperty("model").GetString());
        var image = json.RootElement.GetProperty("messages")[0].GetProperty("content")[0];
        Assert.Equal("image", image.GetProperty("type").GetString());
        Assert.Equal("base64", image.GetProperty("source").GetProperty("type").GetString());
        Assert.Equal("image/jpeg", image.GetProperty("source").GetProperty("media_type").GetString());
        Assert.Equal("AQID", image.GetProperty("source").GetProperty("data").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("system").GetString()));
        // Haiku 5.5 отклоняет temperature/top_p/top_k; рассуждения выключены.
        Assert.False(json.RootElement.TryGetProperty("temperature", out _));
        Assert.Equal("disabled", json.RootElement.GetProperty("thinking").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(529, true)]
    [InlineData(429, true)]
    [InlineData(500, true)]
    [InlineData(403, false)]
    [InlineData(400, false)]
    public async Task AnthropicErrorsAreClassified(int status, bool transient)
    {
        var recognizer = CreateAnthropic(new StubHandler(_ => Task.FromResult(Json((HttpStatusCode)status, """{"type":"error"}"""))));

        var error = await Assert.ThrowsAnyAsync<ReceiptRecognizerException>(() => recognizer.RecognizeAsync([1], "image/png", CancellationToken.None));

        Assert.Equal(transient, error is ReceiptRecognizerUnavailableException);
    }

    private static AnthropicReceiptRecognizer CreateAnthropic(HttpMessageHandler handler)
    {
        return new AnthropicReceiptRecognizer(new HttpClient(handler),
            Options.Create(new AnthropicRecognitionOptions { ApiKey = "secret" }));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private static async Task<(FakeFinanceRepository Repository, FinanceReceipt Receipt)> UploadAsync()
    {
        var repository = new FakeFinanceRepository();
        var service = new FinanceService(repository, new FakeRecognitionQueue(enabled: true));
        var receipt = await service.UploadReceiptAsync(UserId, [1, 2, 3], "image/jpeg", Today);
        return (repository, receipt);
    }

    private static FinanceRecognitionWorker CreateWorker(FakeFinanceRepository repository, IReceiptRecognizer[] recognizers,
        int retries = 0, string order = "openai,anthropic")
    {
        var options = Options.Create(new ReceiptRecognitionOptions
        {
            ApiKey = "key", Project = "folder", Retries = retries, RetryDelaySeconds = 0, Order = order
        });
        var services = new ServiceCollection()
            .AddSingleton<IFinanceRepository>(repository)
            .AddScoped<IFinanceService>(sp => new FinanceService(sp.GetRequiredService<IFinanceRepository>()));
        foreach (var recognizer in recognizers)
            services.AddSingleton(recognizer);

        return new FinanceRecognitionWorker(new FinanceRecognitionQueue(options),
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), options,
            NullLogger<FinanceRecognitionWorker>.Instance);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request);
    }
}
