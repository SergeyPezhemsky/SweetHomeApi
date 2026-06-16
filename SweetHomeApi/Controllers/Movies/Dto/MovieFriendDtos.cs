using Application.Modules.Movies;

namespace SweetHomeApi.Controllers.Movies.Dto;

public class MovieFriendSearchResponseDto
{
    public List<MovieFriendSummaryDto> Items { get; set; } = [];
    public int Total { get; set; }
}

public class MovieFriendSummaryDto
{
    public string UserId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public int MoviesCount { get; set; }
}

public class MovieShareSettingsDto
{
    public bool ShareMovies { get; set; }
}

public class FriendMovieListResponseDto
{
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerNickname { get; set; } = string.Empty;
    public List<SharedMovieListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public bool HasNext { get; set; }
}

public class SharedMovieListItemDto
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
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerNickname { get; set; } = string.Empty;
    public bool IsInMyList { get; set; }
}

public class ImportFriendMovieDto
{
    public string? SourceMovieId { get; set; }
}

public class ImportFriendMovieResponseDto
{
    public string MovieId { get; set; } = string.Empty;
}
