using Microsoft.AspNetCore.Identity;

namespace Application.Modules.Wardrobe;

public class WardrobeOutfit
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public List<string> ItemIds { get; set; } = [];
    public WardrobeStyle Occasion { get; set; }
    public int? Temperature { get; set; }
    public string? Note { get; set; }
    public int WearCount { get; set; }
    public DateTime? LastWornAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public IdentityUser User { get; set; } = null!;
}
