using Application.Modules.Auth;

namespace Application.Modules.Movies;

public class MovieShareSetting
{
    public string UserId { get; set; } = string.Empty;
    public bool ShareMovies { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
