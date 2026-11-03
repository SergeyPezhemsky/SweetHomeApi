using Application.Modules.Wardrobe;
using Application.Modules.Wardrobe.Seeds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SweetHomeApi.Controllers.Movies.Dto;
using SweetHomeApi.Controllers.Wardrobe.Dto;

namespace SweetHomeApi.Controllers.Wardrobe;

[ApiController]
[Authorize]
[Route("api/v1/wardrobe")]
public class WardrobeController(IWardrobeService wardrobeService, UserManager<IdentityUser> userManager) : ControllerBase
{
    public const int MaxImageBytes = 5 * 1024 * 1024;

    [HttpGet("dictionaries")]
    public IActionResult GetDictionaries()
    {
        return Ok(new WardrobeDictionariesDto
        {
            Categories = WardrobeDictionaries.Categories.Select(ToDto).ToList(),
            Seasons = WardrobeDictionaries.Seasons.Select(ToDto).ToList(),
            Styles = WardrobeDictionaries.Styles.Select(ToDto).ToList(),
            Colors = WardrobeDictionaries.Colors.Select(x => new WardrobeColorDto
            {
                Code = x.Code,
                Name = x.Name,
                Hex = x.Hex,
                Neutral = x.Neutral
            }).ToList(),
            WarmthLevels = WardrobeDictionaries.WarmthLevels.Select(x => new WardrobeWarmthLevelDto
            {
                Level = x.Level,
                Name = x.Name
            }).ToList()
        });
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems(
        [FromQuery] string? query,
        [FromQuery] string? category,
        [FromQuery] string? season,
        [FromQuery] string? style,
        [FromQuery] string? color,
        [FromQuery] bool? favorite)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await wardrobeService.GetItemsAsync(userId, new WardrobeItemQuery
        {
            Query = query,
            Category = ParseEnum<WardrobeCategory>(category),
            Season = NormalizeCode(season),
            Style = NormalizeCode(style),
            Color = NormalizeCode(color),
            Favorite = favorite
        });

        return Ok(new WardrobeItemListResponseDto
        {
            Items = result.Items.Select(ToDto).ToList(),
            Total = result.Total,
            Counts = result.Counts.ToDictionary(x => x.Key.ToString(), x => x.Value)
        });
    }

    [HttpGet("items/{itemId}")]
    public async Task<IActionResult> GetItem([FromRoute] string itemId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var item = await wardrobeService.GetItemAsync(userId, itemId);
        return item is null ? NotFound(ItemNotFound()) : Ok(ToDto(item));
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem([FromBody] UpsertWardrobeItemDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var validation = ValidateItem(dto);
        if (validation.Count > 0)
            return BadRequest(ValidationError(validation));

        var item = await wardrobeService.CreateItemAsync(userId, ToItem(dto));
        return CreatedAtAction(nameof(GetItem), new { itemId = item.Id }, ToDto(item));
    }

    [HttpPut("items/{itemId}")]
    public async Task<IActionResult> UpdateItem([FromRoute] string itemId, [FromBody] UpsertWardrobeItemDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var validation = ValidateItem(dto);
        if (validation.Count > 0)
            return BadRequest(ValidationError(validation));

        var item = await wardrobeService.UpdateItemAsync(userId, itemId, ToItem(dto));
        return item is null ? NotFound(ItemNotFound()) : Ok(ToDto(item));
    }

    [HttpDelete("items/{itemId}")]
    public async Task<IActionResult> DeleteItem([FromRoute] string itemId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        return await wardrobeService.DeleteItemAsync(userId, itemId) ? NoContent() : NotFound(ItemNotFound());
    }

    [HttpPost("items/{itemId}/wear")]
    public async Task<IActionResult> WearItem([FromRoute] string itemId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var item = await wardrobeService.WearItemAsync(userId, itemId);
        return item is null ? NotFound(ItemNotFound()) : Ok(ToDto(item));
    }

    [HttpGet("items/{itemId}/image")]
    public async Task<IActionResult> GetImage([FromRoute] string itemId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var image = await wardrobeService.GetImageAsync(userId, itemId);
        if (image is null)
            return NotFound(ImageNotFound());

        Response.Headers.CacheControl = "private, max-age=86400";
        return File(image.Data, image.ContentType);
    }

    [HttpPut("items/{itemId}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImageBytes + 512 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImageBytes + 512 * 1024)]
    public async Task<IActionResult> UploadImage([FromRoute] string itemId, IFormFile? file)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(ValidationError([new ErrorDetailDto { Field = "file", Message = "Выберите фото" }]));

        if (file.Length > MaxImageBytes)
            return BadRequest(ValidationError([new ErrorDetailDto { Field = "file", Message = "Фото должно быть не больше 5 МБ" }]));

        byte[] data;
        await using (var stream = file.OpenReadStream())
        using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory);
            data = memory.ToArray();
        }

        var contentType = DetectImageContentType(data);
        if (contentType is null)
        {
            return BadRequest(ValidationError(
                [new ErrorDetailDto { Field = "file", Message = "Поддерживаются только JPEG, PNG и WEBP" }]));
        }

        var item = await wardrobeService.SetImageAsync(userId, itemId, data, contentType);
        return item is null ? NotFound(ItemNotFound()) : Ok(ToDto(item));
    }

    [HttpDelete("items/{itemId}/image")]
    public async Task<IActionResult> DeleteImage([FromRoute] string itemId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var item = await wardrobeService.DeleteImageAsync(userId, itemId);
        return item is null ? NotFound(ItemNotFound()) : Ok(ToDto(item));
    }

    [HttpPost("classify")]
    public IActionResult Classify([FromBody] ClassifyWardrobeItemDto dto)
    {
        if (GetUserId() is null)
            return Unauthorized();

        var details = new List<ErrorDetailDto>();
        if ((dto.Name?.Length ?? 0) > 120)
            details.Add(new ErrorDetailDto { Field = "name", Message = "Название должно быть не длиннее 120 символов" });
        if ((dto.Notes?.Length ?? 0) > 1000)
            details.Add(new ErrorDetailDto { Field = "notes", Message = "Заметка должна быть не длиннее 1000 символов" });
        if (details.Count > 0)
            return BadRequest(ValidationError(details));

        var result = wardrobeService.Classify(dto.Name, dto.Notes, dto.ColorHex);
        return Ok(new WardrobeClassificationDto
        {
            Category = result.Category,
            Seasons = result.Seasons,
            Styles = result.Styles,
            Warmth = result.Warmth,
            Colors = result.Colors,
            Confidence = result.Confidence,
            MatchedKeywords = result.MatchedKeywords
        });
    }

    [HttpPost("outfits/suggest")]
    public async Task<IActionResult> SuggestOutfits([FromBody] SuggestOutfitsDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var details = new List<ErrorDetailDto>();
        var occasion = ParseEnum<WardrobeStyle>(dto.Occasion);
        if (occasion is null)
            details.Add(new ErrorDetailDto { Field = "occasion", Message = "Выберите повод" });
        if (dto.Temperature is < -60 or > 60)
            details.Add(new ErrorDetailDto { Field = "temperature", Message = "Температура должна быть от -60 до 60 °C" });
        if (dto.Count is < 1 or > 10)
            details.Add(new ErrorDetailDto { Field = "count", Message = "Количество образов — от 1 до 10" });
        if (details.Count > 0)
            return BadRequest(ValidationError(details));

        var result = await wardrobeService.SuggestOutfitsAsync(userId, new OutfitSuggestionRequest
        {
            Occasion = occasion!.Value,
            Temperature = dto.Temperature,
            Count = dto.Count ?? 3,
            Seed = dto.Seed
        });

        return Ok(new OutfitSuggestionsResponseDto
        {
            Outfits = result.Outfits.Select(x => new OutfitSuggestionDto
            {
                Items = x.Items.Select(ToDto).ToList(),
                Score = x.Score,
                Reasons = x.Reasons
            }).ToList(),
            TargetWarmth = result.TargetWarmth,
            Season = result.Season,
            Temperature = result.Temperature,
            TemperatureAssumed = result.TemperatureAssumed,
            Missing = result.Missing
        });
    }

    [HttpGet("outfits")]
    public async Task<IActionResult> GetOutfits()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var outfits = await wardrobeService.GetOutfitsAsync(userId);
        return Ok(outfits.Select(ToDto).ToList());
    }

    [HttpPost("outfits")]
    public async Task<IActionResult> CreateOutfit([FromBody] CreateWardrobeOutfitDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var details = new List<ErrorDetailDto>();
        if (string.IsNullOrWhiteSpace(dto.Name))
            details.Add(new ErrorDetailDto { Field = "name", Message = "Название обязательно" });
        else if (dto.Name.Trim().Length > 80)
            details.Add(new ErrorDetailDto { Field = "name", Message = "Название должно быть не длиннее 80 символов" });

        var itemIds = (dto.ItemIds ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (itemIds.Count is < 2 or > 8)
            details.Add(new ErrorDetailDto { Field = "itemIds", Message = "В образе должно быть от 2 до 8 вещей" });

        var occasion = ParseEnum<WardrobeStyle>(dto.Occasion);
        if (occasion is null)
            details.Add(new ErrorDetailDto { Field = "occasion", Message = "Выберите повод" });
        if (dto.Temperature is < -60 or > 60)
            details.Add(new ErrorDetailDto { Field = "temperature", Message = "Температура должна быть от -60 до 60 °C" });
        if (dto.Note?.Length > 500)
            details.Add(new ErrorDetailDto { Field = "note", Message = "Заметка должна быть не длиннее 500 символов" });

        if (details.Count > 0)
            return BadRequest(ValidationError(details));

        var result = await wardrobeService.CreateOutfitAsync(userId, new WardrobeOutfit
        {
            Name = dto.Name!,
            ItemIds = itemIds,
            Occasion = occasion!.Value,
            Temperature = dto.Temperature,
            Note = dto.Note
        });

        if (result.Status == WardrobeOperationStatus.InvalidItems)
        {
            return BadRequest(ValidationError(
                [new ErrorDetailDto { Field = "itemIds", Message = "Некоторые вещи не найдены в вашем гардеробе" }]));
        }

        return StatusCode(StatusCodes.Status201Created, ToDto(result.Value!));
    }

    [HttpDelete("outfits/{outfitId}")]
    public async Task<IActionResult> DeleteOutfit([FromRoute] string outfitId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        return await wardrobeService.DeleteOutfitAsync(userId, outfitId) ? NoContent() : NotFound(OutfitNotFound());
    }

    [HttpPost("outfits/{outfitId}/wear")]
    public async Task<IActionResult> WearOutfit([FromRoute] string outfitId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var outfit = await wardrobeService.WearOutfitAsync(userId, outfitId);
        return outfit is null ? NotFound(OutfitNotFound()) : Ok(ToDto(outfit));
    }

    /// <summary>Определяет тип изображения по сигнатуре файла, а не по заявленному Content-Type.</summary>
    public static string? DetectImageContentType(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";

        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
            && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
            return "image/png";

        if (data.Length >= 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
            && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
            return "image/webp";

        return null;
    }

    private string? GetUserId()
    {
        return userManager.GetUserId(User);
    }

    /// <summary>Разбирает код перечисления (без учёта регистра), числовые значения не принимаются.</summary>
    private static T? ParseEnum<T>(string? value) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Any(char.IsDigit))
            return null;

        return Enum.TryParse<T>(value.Trim(), true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
    }

    private static string? NormalizeCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private static List<ErrorDetailDto> ValidateItem(UpsertWardrobeItemDto dto)
    {
        var details = new List<ErrorDetailDto>();

        if (string.IsNullOrWhiteSpace(dto.Name))
            details.Add(new ErrorDetailDto { Field = "name", Message = "Название обязательно" });
        else if (dto.Name.Trim().Length > 120)
            details.Add(new ErrorDetailDto { Field = "name", Message = "Название должно быть не длиннее 120 символов" });

        if (ParseEnum<WardrobeCategory>(dto.Category) is null)
            details.Add(new ErrorDetailDto { Field = "category", Message = "Выберите категорию" });

        var colors = Codes(dto.Colors);
        if (colors.Count is < 1 or > 3)
            details.Add(new ErrorDetailDto { Field = "colors", Message = "Укажите от 1 до 3 цветов" });
        else if (colors.Any(x => !WardrobeDictionaries.IsKnownColor(x)))
            details.Add(new ErrorDetailDto { Field = "colors", Message = "Неизвестный цвет" });

        if (Codes(dto.Seasons).Any(x => !Enum.TryParse<WardrobeSeason>(x, false, out _)))
            details.Add(new ErrorDetailDto { Field = "seasons", Message = "Неизвестный сезон" });

        var styles = Codes(dto.Styles);
        if (styles.Count == 0)
            details.Add(new ErrorDetailDto { Field = "styles", Message = "Укажите хотя бы один стиль" });
        else if (styles.Any(x => !Enum.TryParse<WardrobeStyle>(x, false, out _)))
            details.Add(new ErrorDetailDto { Field = "styles", Message = "Неизвестный стиль" });

        if (dto.Warmth is null or < 1 or > 5)
            details.Add(new ErrorDetailDto { Field = "warmth", Message = "Теплота должна быть от 1 до 5" });

        if (dto.Brand?.Trim().Length > 80)
            details.Add(new ErrorDetailDto { Field = "brand", Message = "Бренд должен быть не длиннее 80 символов" });

        if (dto.Notes?.Trim().Length > 1000)
            details.Add(new ErrorDetailDto { Field = "notes", Message = "Заметка должна быть не длиннее 1000 символов" });

        return details;
    }

    private static List<string> Codes(List<string>? values)
    {
        return (values ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
    }

    private static WardrobeItem ToItem(UpsertWardrobeItemDto dto)
    {
        return new WardrobeItem
        {
            Name = dto.Name ?? string.Empty,
            Category = ParseEnum<WardrobeCategory>(dto.Category)!.Value,
            Colors = Codes(dto.Colors),
            Seasons = Codes(dto.Seasons),
            Styles = Codes(dto.Styles),
            Warmth = dto.Warmth!.Value,
            Brand = dto.Brand,
            Notes = dto.Notes,
            IsFavorite = dto.IsFavorite ?? false
        };
    }

    private static WardrobeDictionaryItemDto ToDto(WardrobeDictionaryItem item)
    {
        return new WardrobeDictionaryItemDto { Code = item.Code, Name = item.Name };
    }

    private static WardrobeItemDto ToDto(WardrobeItem item)
    {
        return new WardrobeItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Category = item.Category,
            Colors = item.Colors,
            Seasons = item.Seasons,
            Styles = item.Styles,
            Warmth = item.Warmth,
            Brand = item.Brand,
            Notes = item.Notes,
            IsFavorite = item.IsFavorite,
            HasImage = item.HasImage,
            ImageUpdatedAt = item.ImageUpdatedAt,
            WearCount = item.WearCount,
            LastWornAt = item.LastWornAt,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static WardrobeOutfitDto ToDto(WardrobeOutfitDetails details)
    {
        return new WardrobeOutfitDto
        {
            Id = details.Outfit.Id,
            Name = details.Outfit.Name,
            ItemIds = details.Outfit.ItemIds,
            Items = details.Items.Select(ToDto).ToList(),
            Occasion = details.Outfit.Occasion,
            Temperature = details.Outfit.Temperature,
            Note = details.Outfit.Note,
            WearCount = details.Outfit.WearCount,
            LastWornAt = details.Outfit.LastWornAt,
            CreatedAt = details.Outfit.CreatedAt,
            UpdatedAt = details.Outfit.UpdatedAt
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

    private ErrorResponseDto ItemNotFound() => Error("WARDROBE_ITEM_NOT_FOUND", "Вещь не найдена");

    private ErrorResponseDto ImageNotFound() => Error("WARDROBE_IMAGE_NOT_FOUND", "Фото не найдено");

    private ErrorResponseDto OutfitNotFound() => Error("WARDROBE_OUTFIT_NOT_FOUND", "Образ не найден");

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
