using Microsoft.AspNetCore.Identity;

namespace Application.Modules.Movies;

public class Movie
{
    public string MovieId { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public MovieContentType ContentType { get; set; }
    public decimal? Rating { get; set; }
    public List<string> Genres { get; set; } = [];
    public string? Country { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public IdentityUser User { get; set; } = null!;
}
