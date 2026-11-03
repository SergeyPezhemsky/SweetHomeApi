using Microsoft.AspNetCore.Identity;

namespace Application.Modules.Wardrobe;

public class WardrobeItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public WardrobeCategory Category { get; set; }

    /// <summary>Коды цветов из словаря (<see cref="Seeds.WardrobeDictionaries.Colors"/>).</summary>
    public List<string> Colors { get; set; } = [];

    /// <summary>Коды сезонов. Пустой список означает «всесезонная вещь».</summary>
    public List<string> Seasons { get; set; } = [];

    /// <summary>Коды стилей, хотя бы один.</summary>
    public List<string> Styles { get; set; } = [];

    /// <summary>Теплота 1..5.</summary>
    public int Warmth { get; set; } = 2;

    public string? Brand { get; set; }
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public bool HasImage { get; set; }
    public DateTime? ImageUpdatedAt { get; set; }
    public WardrobeImageStatus ImageStatus { get; set; } = WardrobeImageStatus.NONE;
    public WardrobeImageVariant ImageVariant { get; set; } = WardrobeImageVariant.ORIGINAL;
    public int WearCount { get; set; }
    public DateTime? LastWornAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public IdentityUser User { get; set; } = null!;
}
