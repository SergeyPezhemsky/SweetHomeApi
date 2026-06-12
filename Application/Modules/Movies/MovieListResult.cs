namespace Application.Modules.Movies;

public class MovieListResult
{
    public List<Movie> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public bool HasNext { get; set; }
}
