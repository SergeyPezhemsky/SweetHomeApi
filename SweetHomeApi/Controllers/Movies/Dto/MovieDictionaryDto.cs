namespace SweetHomeApi.Controllers.Movies.Dto;

public class MovieDictionaryDto
{
    public List<MovieContentTypeDto> ContentTypes { get; set; } = [];
    public List<string> Genres { get; set; } = [];
    public List<string> Countries { get; set; } = [];
}
