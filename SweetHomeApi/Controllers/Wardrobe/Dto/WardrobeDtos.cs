using Application.Modules.Wardrobe;

namespace SweetHomeApi.Controllers.Wardrobe.Dto;

public class WardrobeItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public WardrobeCategory Category { get; set; }
    public List<string> Colors { get; set; } = [];
    public List<string> Seasons { get; set; } = [];
    public List<string> Styles { get; set; } = [];
    public int Warmth { get; set; }
    public string? Brand { get; set; }
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public bool HasImage { get; set; }
    public DateTime? ImageUpdatedAt { get; set; }
    public WardrobeImageStatus ImageStatus { get; set; }
    public WardrobeImageVariant ImageVariant { get; set; }
    public int WearCount { get; set; }
    public DateTime? LastWornAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WardrobeItemListResponseDto
{
    public List<WardrobeItemDto> Items { get; set; } = [];
    public int Total { get; set; }
    public Dictionary<string, int> Counts { get; set; } = [];
}

public class UpsertWardrobeItemDto
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public List<string>? Colors { get; set; }
    public List<string>? Seasons { get; set; }
    public List<string>? Styles { get; set; }
    public int? Warmth { get; set; }
    public string? Brand { get; set; }
    public string? Notes { get; set; }
    public bool? IsFavorite { get; set; }
}

public class WardrobeDictionaryItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class WardrobeColorDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
    public bool Neutral { get; set; }
}

public class WardrobeWarmthLevelDto
{
    public int Level { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class WardrobeDictionariesDto
{
    public List<WardrobeDictionaryItemDto> Categories { get; set; } = [];
    public List<WardrobeDictionaryItemDto> Seasons { get; set; } = [];
    public List<WardrobeDictionaryItemDto> Styles { get; set; } = [];
    public List<WardrobeColorDto> Colors { get; set; } = [];
    public List<WardrobeWarmthLevelDto> WarmthLevels { get; set; } = [];
    /// <summary>Включено ли автоматическое удаление фона у фото.</summary>
    public bool BackgroundRemoval { get; set; }
}

public class ClassifyWardrobeItemDto
{
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public string? ColorHex { get; set; }
}

public class WardrobeClassificationDto
{
    public WardrobeCategory? Category { get; set; }
    public List<string> Seasons { get; set; } = [];
    public List<string> Styles { get; set; } = [];
    public int? Warmth { get; set; }
    public List<string> Colors { get; set; } = [];
    public double Confidence { get; set; }
    public List<string> MatchedKeywords { get; set; } = [];
}

public class SuggestOutfitsDto
{
    public string? Occasion { get; set; }
    public int? Temperature { get; set; }
    public int? Count { get; set; }
    public int? Seed { get; set; }
}

public class OutfitSuggestionDto
{
    public List<WardrobeItemDto> Items { get; set; } = [];
    public int Score { get; set; }
    public List<string> Reasons { get; set; } = [];
}

public class OutfitSuggestionsResponseDto
{
    public List<OutfitSuggestionDto> Outfits { get; set; } = [];
    public int TargetWarmth { get; set; }
    public WardrobeSeason Season { get; set; }
    public int Temperature { get; set; }
    public bool TemperatureAssumed { get; set; }
    public List<string> Missing { get; set; } = [];
}

public class CreateWardrobeOutfitDto
{
    public string? Name { get; set; }
    public List<string>? ItemIds { get; set; }
    public string? Occasion { get; set; }
    public int? Temperature { get; set; }
    public string? Note { get; set; }
}

public class WardrobeOutfitDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> ItemIds { get; set; } = [];
    public List<WardrobeItemDto> Items { get; set; } = [];
    public WardrobeStyle Occasion { get; set; }
    public int? Temperature { get; set; }
    public string? Note { get; set; }
    public int WearCount { get; set; }
    public DateTime? LastWornAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SetWardrobeImageVariantDto
{
    public string? Variant { get; set; }
}
