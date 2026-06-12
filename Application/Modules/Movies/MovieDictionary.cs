namespace Application.Modules.Movies;

public class MovieDictionary
{
    public List<MovieContentTypeItem> ContentTypes { get; set; } = [];
    public List<string> Genres { get; set; } = [];
    public List<string> Countries { get; set; } = [];
}
