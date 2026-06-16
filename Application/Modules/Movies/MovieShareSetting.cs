using Microsoft.AspNetCore.Identity;

namespace Application.Modules.Movies;

public class MovieShareSetting
{
    public string UserId { get; set; } = string.Empty;
    public bool ShareMovies { get; set; }
    public IdentityUser User { get; set; } = null!;
}
