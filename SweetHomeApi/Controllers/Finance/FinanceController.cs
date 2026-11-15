using System.Globalization;
using Application.Modules.Finance;
using Application.Modules.Finance.Seeds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SweetHomeApi.Controllers.Finance.Dto;
using SweetHomeApi.Controllers.Movies.Dto;
using SweetHomeApi.Controllers.Wardrobe;

namespace SweetHomeApi.Controllers.Finance;

[ApiController]
[Authorize]
[Route("api/v1/finance")]
[WardrobeModelStateErrors]
public class FinanceController(
    IFinanceService financeService,
    UserManager<IdentityUser> userManager) : ControllerBase
{
    public const int MaxImageBytes = 5 * 1024 * 1024;
    private const int MaxRequestBytes = MaxImageBytes + 512 * 1024;
    private const string ImageTooLarge = "Фото должно быть не больше 5 МБ";

    [HttpGet("dictionaries")]
    public IActionResult GetDictionaries()
    {
        if (GetUserId() is null)
            return Unauthorized();

        return Ok(new FinanceDictionariesDto
        {
            Categories = FinanceDictionaries.Categories.Select(x => new FinanceCategoryDto
            {
                Code = x.Code.ToString(),
                Name = x.Name,
                Icon = x.Icon,
                Color = x.Color
            }).ToList(),
            Recognition = financeService.RecognitionEnabled
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] string? month)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        if (!FinanceMonth.TryParse(month, out var parsed))
            return BadRequest(ValidationError([new ErrorDetailDto { Field = "month", Message = "Укажите месяц в формате ГГГГ-ММ" }]));

        var summary = await financeService.GetSummaryAsync(userId, parsed);
        return Ok(new FinanceSummaryDto
        {
            Month = summary.Month.ToString(),
            Total = summary.Total,
            ReceiptCount = summary.ReceiptCount,
            PendingCount = summary.PendingCount,
            FailedCount = summary.FailedCount,
            OtherCurrencyCount = summary.OtherCurrencyCount,
            PreviousTotal = summary.PreviousTotal,
            Categories = summary.Categories.Select(x => new FinanceCategoryAmountDto
            {
                Category = x.Category,
                Amount = x.Amount,
                ReceiptCount = x.ReceiptCount
            }).ToList()
        });
    }

    [HttpGet("receipts")]
    public async Task<IActionResult> GetReceipts([FromQuery] string? month, [FromQuery] string? category)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var details = new List<ErrorDetailDto>();
        if (!FinanceMonth.TryParse(month, out var parsedMonth))
            details.Add(new ErrorDetailDto { Field = "month", Message = "Укажите месяц в формате ГГГГ-ММ" });

        FinanceCategory? parsedCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            parsedCategory = ReceiptRecognitionParser.ParseCategory(category);
            if (parsedCategory is null)
                details.Add(new ErrorDetailDto { Field = "category", Message = "Неизвестная категория" });
        }

        if (details.Count > 0)
            return BadRequest(ValidationError(details));

        var receipts = await financeService.GetReceiptsAsync(userId, parsedMonth, parsedCategory);
        return Ok(new FinanceReceiptListDto
        {
            Month = parsedMonth.ToString(),
            Receipts = receipts.Select(x => ToDto(x.Receipt, includeItems: false, x.CategoryAmount, x.TopCategories)).ToList()
        });
    }

    [HttpGet("receipts/{receiptId}")]
    public async Task<IActionResult> GetReceipt([FromRoute] string receiptId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var receipt = await financeService.GetReceiptAsync(userId, receiptId);
        return receipt is null ? NotFound(ReceiptNotFound()) : Ok(ToDto(receipt, includeItems: true));
    }

    [HttpPost("receipts")]
    [DisableFormValueModelBinding]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> UploadReceipt()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        // Форму читаем вручную: так анонимный запрос получает 401, а не 415, и все ошибки идут в общем формате.
        if (!Request.HasFormContentType)
            return BadRequest(ValidationError([new ErrorDetailDto { Field = "file", Message = "Отправьте фото как multipart/form-data в поле file" }]));

        if (Request.ContentLength > MaxRequestBytes)
            return BadRequest(ValidationError([new ErrorDetailDto { Field = "file", Message = ImageTooLarge }]));

        IFormCollection form;
        try
        {
            form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or BadHttpRequestException)
        {
            var tooLarge = ex is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge }
                           || ex.Message.Contains("length limit", StringComparison.OrdinalIgnoreCase);
            var message = tooLarge ? ImageTooLarge : "Не удалось прочитать файл. Попробуйте загрузить фото ещё раз";
            return BadRequest(ValidationError([new ErrorDetailDto { Field = "file", Message = message }]));
        }

        var details = new List<ErrorDetailDto>();
        var purchasedOn = TodayUtc();
        var purchasedOnText = form["purchasedOn"].ToString();
        if (!string.IsNullOrWhiteSpace(purchasedOnText))
        {
            if (TryParseDate(purchasedOnText, out var parsed))
                purchasedOn = parsed;
            else
                details.Add(new ErrorDetailDto { Field = "purchasedOn", Message = "Дата должна быть в формате ГГГГ-ММ-ДД" });
        }

        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
            details.Add(new ErrorDetailDto { Field = "file", Message = "Выберите фото чека" });
        else if (file.Length > MaxImageBytes)
            details.Add(new ErrorDetailDto { Field = "file", Message = ImageTooLarge });

        if (details.Count > 0)
            return BadRequest(ValidationError(details));

        byte[] data;
        await using (var stream = file!.OpenReadStream())
        using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory, HttpContext.RequestAborted);
            data = memory.ToArray();
        }

        var contentType = WardrobeController.DetectImageContentType(data);
        if (contentType is null)
            return BadRequest(ValidationError([new ErrorDetailDto { Field = "file", Message = "Поддерживаются только JPEG, PNG и WEBP" }]));

        var receipt = await financeService.UploadReceiptAsync(userId, data, contentType, purchasedOn);
        return CreatedAtAction(nameof(GetReceipt), new { receiptId = receipt.Id }, ToDto(receipt, includeItems: true));
    }

    [HttpPost("receipts/manual")]
    public async Task<IActionResult> CreateManualReceipt([FromBody] CreateManualReceiptDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var details = new List<ErrorDetailDto>();
        var purchasedOn = TodayUtc();
        if (!string.IsNullOrWhiteSpace(dto.PurchasedOn) && !TryParseDate(dto.PurchasedOn, out purchasedOn))
            details.Add(new ErrorDetailDto { Field = "purchasedOn", Message = "Дата должна быть в формате ГГГГ-ММ-ДД" });

        ValidateTotal(dto.Total, details);
        var category = ValidateCategory(dto.Category, "category", details);
        var currency = ValidateCurrency(dto.Currency, details);
        ValidateText(dto.Store, "store", FinanceDefaults.MaxStoreLength, "Магазин", details);
        ValidateText(dto.Note, "note", FinanceDefaults.MaxNoteLength, "Заметка", details, allowLineBreaks: true);

        if (details.Count > 0)
            return BadRequest(ValidationError(details));

        var receipt = await financeService.CreateManualReceiptAsync(userId, new FinanceReceiptUpdate
        {
            PurchasedOn = purchasedOn,
            Store = dto.Store,
            Total = dto.Total!.Value,
            Currency = currency,
            Category = category!.Value,
            Note = dto.Note
        });
        return CreatedAtAction(nameof(GetReceipt), new { receiptId = receipt.Id }, ToDto(receipt, includeItems: true));
    }

    [HttpPut("receipts/{receiptId}")]
    public async Task<IActionResult> UpdateReceipt([FromRoute] string receiptId, [FromBody] UpdateReceiptDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var details = new List<ErrorDetailDto>();
        var purchasedOn = default(DateOnly);
        if (string.IsNullOrWhiteSpace(dto.PurchasedOn) || !TryParseDate(dto.PurchasedOn, out purchasedOn))
            details.Add(new ErrorDetailDto { Field = "purchasedOn", Message = "Укажите дату в формате ГГГГ-ММ-ДД" });

        ValidateTotal(dto.Total, details);
        var category = ValidateCategory(dto.Category, "category", details);
        var currency = ValidateCurrency(dto.Currency, details);
        ValidateText(dto.Store, "store", FinanceDefaults.MaxStoreLength, "Магазин", details);
        ValidateText(dto.Note, "note", FinanceDefaults.MaxNoteLength, "Заметка", details, allowLineBreaks: true);

        var items = new List<FinanceReceiptItemUpdate>();
        var inputItems = dto.Items ?? [];
        if (inputItems.Count > FinanceDefaults.MaxItems)
            details.Add(new ErrorDetailDto { Field = "items", Message = $"В чеке может быть не больше {FinanceDefaults.MaxItems} позиций" });
        else
        {
            for (var i = 0; i < inputItems.Count; i++)
            {
                var item = inputItems[i];
                var field = $"items[{i}]";
                if (item is null)
                {
                    details.Add(new ErrorDetailDto { Field = field, Message = "Пустая позиция" });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.Name))
                    details.Add(new ErrorDetailDto { Field = $"{field}.name", Message = "Название позиции обязательно" });
                else
                    ValidateText(item.Name, $"{field}.name", FinanceDefaults.MaxItemNameLength, "Название позиции", details);

                if (item.Amount is null || item.Amount == 0 || Math.Abs(item.Amount.Value) > FinanceDefaults.MaxAmount)
                    details.Add(new ErrorDetailDto { Field = $"{field}.amount", Message = "Сумма позиции — ненулевое число не больше 10 млн по модулю" });
                if (item.Quantity is < 0 or >= 1_000_000)
                    details.Add(new ErrorDetailDto { Field = $"{field}.quantity", Message = "Количество — от 0 до 1 000 000" });
                if (item.Price is < 0 || item.Price > FinanceDefaults.MaxAmount)
                    details.Add(new ErrorDetailDto { Field = $"{field}.price", Message = "Цена — от 0 до 10 млн" });
                var itemCategory = ValidateCategory(item.Category, $"{field}.category", details);

                if (itemCategory is not null && item.Amount is not null && !string.IsNullOrWhiteSpace(item.Name))
                {
                    items.Add(new FinanceReceiptItemUpdate
                    {
                        Id = string.IsNullOrWhiteSpace(item.Id) ? null : item.Id,
                        Name = item.Name,
                        Quantity = item.Quantity is null ? null : Math.Round(item.Quantity.Value, 3, MidpointRounding.AwayFromZero),
                        Price = item.Price is null ? null : Math.Round(item.Price.Value, 2, MidpointRounding.AwayFromZero),
                        Amount = item.Amount.Value,
                        Category = itemCategory.Value
                    });
                }
            }
        }

        if (details.Count > 0)
            return BadRequest(ValidationError(details));

        var receipt = await financeService.UpdateReceiptAsync(userId, receiptId, new FinanceReceiptUpdate
        {
            PurchasedOn = purchasedOn,
            Store = dto.Store,
            Total = dto.Total!.Value,
            Currency = currency,
            Category = category!.Value,
            Note = dto.Note,
            Items = items
        });
        return receipt is null ? NotFound(ReceiptNotFound()) : Ok(ToDto(receipt, includeItems: true));
    }

    [HttpDelete("receipts/{receiptId}")]
    public async Task<IActionResult> DeleteReceipt([FromRoute] string receiptId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        return await financeService.DeleteReceiptAsync(userId, receiptId) ? NoContent() : NotFound(ReceiptNotFound());
    }

    [HttpPost("receipts/{receiptId}/recognize")]
    public async Task<IActionResult> RecognizeAgain([FromRoute] string receiptId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await financeService.RecognizeAgainAsync(userId, receiptId);
        return result.Status switch
        {
            FinanceOperationStatus.NotFound => NotFound(ReceiptNotFound()),
            FinanceOperationStatus.NoImage => NotFound(Error("FINANCE_IMAGE_NOT_FOUND", "У чека нет фото")),
            FinanceOperationStatus.Disabled => StatusCode(StatusCodes.Status503ServiceUnavailable,
                Error("FINANCE_RECOGNITION_DISABLED", "Распознавание чеков выключено")),
            _ => Ok(ToDto(result.Value!, includeItems: true))
        };
    }

    [HttpGet("receipts/{receiptId}/image")]
    public async Task<IActionResult> GetImage([FromRoute] string receiptId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var image = await financeService.GetImageAsync(userId, receiptId);
        if (image is null)
            return NotFound(Error("FINANCE_IMAGE_NOT_FOUND", "Фото не найдено"));

        // Фото чека после загрузки не меняется.
        Response.Headers.CacheControl = "private, max-age=86400";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Data, image.ContentType);
    }

    private static DateOnly TodayUtc() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static bool TryParseDate(string value, out DateOnly date)
    {
        return DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
               && date.Year is >= 2000 and <= 2100;
    }

    private static void ValidateTotal(decimal? total, List<ErrorDetailDto> details)
    {
        if (total is null || total < 0.01m || total > FinanceDefaults.MaxAmount)
            details.Add(new ErrorDetailDto { Field = "total", Message = "Сумма — от 0,01 до 10 млн" });
    }

    private static FinanceCategory? ValidateCategory(string? value, string field, List<ErrorDetailDto> details)
    {
        var category = ReceiptRecognitionParser.ParseCategory(value);
        if (category is null)
            details.Add(new ErrorDetailDto { Field = field, Message = "Выберите категорию" });
        return category;
    }

    private static string ValidateCurrency(string? value, List<ErrorDetailDto> details)
    {
        if (string.IsNullOrWhiteSpace(value))
            return FinanceDefaults.Currency;

        var code = value.Trim().ToUpperInvariant();
        if (code.Length != 3 || code.Any(c => c is < 'A' or > 'Z'))
        {
            details.Add(new ErrorDetailDto { Field = "currency", Message = "Валюта — трёхбуквенный код, например RUB" });
            return FinanceDefaults.Currency;
        }

        return code;
    }

    private static void ValidateText(string? value, string field, int maxLength, string label, List<ErrorDetailDto> details,
        bool allowLineBreaks = false)
    {
        if (value is null)
            return;

        if (value.Trim().Length > maxLength)
            details.Add(new ErrorDetailDto { Field = field, Message = $"{label}: не длиннее {maxLength} символов" });
        else if (value.Any(c => char.IsControl(c) && !(allowLineBreaks && c is '\n' or '\r' or '\t')))
            details.Add(new ErrorDetailDto { Field = field, Message = $"{label}: недопустимые символы" });
    }

    private string? GetUserId() => userManager.GetUserId(User);

    private static FinanceReceiptDto ToDto(FinanceReceipt receipt, bool includeItems, decimal? categoryAmount = null,
        List<FinanceCategory>? topCategories = null)
    {
        return new FinanceReceiptDto
        {
            Id = receipt.Id,
            Source = receipt.Source,
            Status = receipt.Status,
            FailureReason = receipt.FailureReason,
            PurchasedOn = receipt.PurchasedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Store = receipt.Store,
            Total = receipt.Total,
            Currency = receipt.Currency,
            Category = receipt.Category,
            Note = receipt.Note,
            HasImage = receipt.HasImage,
            ItemCount = receipt.Items.Count,
            ItemsTotal = receipt.Items.Sum(x => x.Amount),
            CategoryAmount = categoryAmount,
            TopCategories = topCategories ?? (receipt.Status == FinanceReceiptStatus.READY
                ? FinanceAllocation.Allocate(receipt).OrderByDescending(x => x.Value).ThenBy(x => x.Key).Take(3).Select(x => x.Key).ToList()
                : []),
            Items = includeItems
                ? receipt.Items.Select(x => new FinanceReceiptItemDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Quantity = x.Quantity,
                    Price = x.Price,
                    Amount = x.Amount,
                    Category = x.Category,
                    CategorySource = x.CategorySource
                }).ToList()
                : null,
            CreatedAt = receipt.CreatedAt,
            UpdatedAt = receipt.UpdatedAt
        };
    }

    private ErrorResponseDto ValidationError(List<ErrorDetailDto> details)
    {
        return new ErrorResponseDto
        {
            ErrorCode = "VALIDATION_ERROR",
            Message = "Ошибка валидации",
            Details = details,
            TraceId = HttpContext.TraceIdentifier
        };
    }

    private ErrorResponseDto ReceiptNotFound() => Error("FINANCE_RECEIPT_NOT_FOUND", "Чек не найден");

    private ErrorResponseDto Error(string code, string message)
    {
        return new ErrorResponseDto
        {
            ErrorCode = code,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        };
    }
}
