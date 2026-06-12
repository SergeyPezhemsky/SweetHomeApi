using Application.Modules.Movies;

namespace SweetHomeApi.Controllers.Movies.Dto;

public class MovieDto
{
    public string MovieId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public MovieContentType ContentType { get; set; }
    public decimal? Rating { get; set; }
    public List<string> Genres { get; set; } = [];
    public string? Country { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
