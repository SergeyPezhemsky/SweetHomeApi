namespace Application.Modules.Movies;

public class MovieFriendSearchResult
{
    public List<MovieFriendSummary> Items { get; set; } = [];
    public int Total { get; set; }
}

public class MovieFriendSummary
{
    public string UserId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public int MoviesCount { get; set; }
}

public class MovieFriendMoviesResult
{
    public MovieFriendAccessStatus Status { get; set; } = MovieFriendAccessStatus.Ok;
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerNickname { get; set; } = string.Empty;
    public List<SharedMovieListItem> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public bool HasNext { get; set; }
}

public class SharedMovieListItem
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

public enum MovieFriendAccessStatus
{
    Ok,
    NotFound,
    Forbidden
}

public class MovieImportResult
{
    public MovieFriendAccessStatus Status { get; set; } = MovieFriendAccessStatus.Ok;
    public string? MovieId { get; set; }
}
