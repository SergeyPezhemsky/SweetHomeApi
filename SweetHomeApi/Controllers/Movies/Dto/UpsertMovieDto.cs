using Application.Modules.Movies;

namespace SweetHomeApi.Controllers.Movies.Dto;

public class UpsertMovieDto
{
    public string? Title { get; set; }
    public MovieContentType? ContentType { get; set; }
    public decimal? Rating { get; set; }
    public List<string>? Genres { get; set; }
    public string? Country { get; set; }
    public string? Comment { get; set; }
}
