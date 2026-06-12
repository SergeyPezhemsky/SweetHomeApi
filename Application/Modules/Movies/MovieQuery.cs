namespace Application.Modules.Movies;

public class MovieQuery
{
    public string? Query { get; set; }
    public List<MovieContentType> ContentTypes { get; set; } = [];
    public List<string> Genres { get; set; } = [];
    public List<string> Countries { get; set; } = [];
    public decimal? RatingFrom { get; set; }
    public decimal? RatingTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public MovieSortBy SortBy { get; set; } = MovieSortBy.UPDATED_AT;
    public SortDirection SortDirection { get; set; } = SortDirection.DESC;
}
