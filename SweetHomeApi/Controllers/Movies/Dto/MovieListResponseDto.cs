namespace SweetHomeApi.Controllers.Movies.Dto;

public class MovieListResponseDto
{
    public List<MovieDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public bool HasNext { get; set; }
}
