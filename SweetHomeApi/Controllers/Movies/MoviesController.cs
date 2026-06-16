using Application.Modules.Movies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SweetHomeApi.Controllers.Movies.Dto;

namespace SweetHomeApi.Controllers.Movies;

[ApiController]
[Authorize]
[Route("api/v1/movies")]
public class MoviesController(IMovieService movieService, UserManager<IdentityUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? query,
        [FromQuery] string? contentTypes,
        [FromQuery] string? genres,
        [FromQuery] string? countries,
        [FromQuery] decimal? ratingFrom,
        [FromQuery] decimal? ratingTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] MovieSortBy sortBy = MovieSortBy.UPDATED_AT,
        [FromQuery] SortDirection sortDirection = SortDirection.DESC)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var movieQuery = new MovieQuery
        {
            Query = query,
            ContentTypes = ParseEnums<MovieContentType>(contentTypes),
            Genres = ParseCsv(genres),
            Countries = ParseCsv(countries),
            RatingFrom = ratingFrom,
            RatingTo = ratingTo,
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        };

        var movies = await movieService.GetListAsync(userId, movieQuery);
        return Ok(new MovieListResponseDto
        {
            Items = movies.Items.Select(ToDto).ToList(),
            Page = movies.Page,
            PageSize = movies.PageSize,
            Total = movies.Total,
            HasNext = movies.HasNext
        });
    }

    [HttpGet("dictionaries")]
    public async Task<IActionResult> GetDictionaries()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var dictionaries = await movieService.GetDictionariesAsync(userId);
        return Ok(new MovieDictionaryDto
        {
            ContentTypes = dictionaries.ContentTypes.Select(x => new MovieContentTypeDto
            {
                Code = x.Code,
                Name = x.Name
            }).ToList(),
            Genres = dictionaries.Genres,
            Countries = dictionaries.Countries
        });
    }

    [HttpGet("friends")]
    public async Task<IActionResult> SearchFriends([FromQuery] string? query)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var friends = await movieService.SearchFriendsAsync(userId, query);
        return Ok(new MovieFriendSearchResponseDto
        {
            Items = friends.Items.Select(x => new MovieFriendSummaryDto
            {
                UserId = x.UserId,
                Nickname = x.Nickname,
                MoviesCount = x.MoviesCount
            }).ToList(),
            Total = friends.Total
        });
    }

    [HttpGet("friends/share-settings")]
    public async Task<IActionResult> GetShareSettings()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        return Ok(new MovieShareSettingsDto
        {
            ShareMovies = await movieService.GetShareMoviesAsync(userId)
        });
    }

    [HttpPut("friends/share-settings")]
    public async Task<IActionResult> UpdateShareSettings([FromBody] MovieShareSettingsDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        return Ok(new MovieShareSettingsDto
        {
            ShareMovies = await movieService.UpdateShareMoviesAsync(userId, dto.ShareMovies)
        });
    }

    [HttpGet("friends/{friendUserId}")]
    public async Task<IActionResult> GetFriendMovies(
        [FromRoute] string friendUserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var movies = await movieService.GetFriendMoviesAsync(userId, friendUserId, page, pageSize);
        return movies.Status switch
        {
            MovieFriendAccessStatus.NotFound => NotFound(UserNotFound()),
            MovieFriendAccessStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, MovieListForbidden()),
            _ => Ok(new FriendMovieListResponseDto
            {
                OwnerUserId = movies.OwnerUserId,
                OwnerNickname = movies.OwnerNickname,
                Items = movies.Items.Select(ToSharedDto).ToList(),
                Page = movies.Page,
                PageSize = movies.PageSize,
                Total = movies.Total,
                HasNext = movies.HasNext
            })
        };
    }

    [HttpPost("friends/import")]
    public async Task<IActionResult> ImportFriendMovie([FromBody] ImportFriendMovieDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.SourceMovieId))
        {
            return BadRequest(ValidationError(
            [
                new ErrorDetailDto { Field = "sourceMovieId", Message = "sourceMovieId is required" }
            ]));
        }

        var result = await movieService.ImportFriendMovieAsync(userId, dto.SourceMovieId);
        return result.Status switch
        {
            MovieFriendAccessStatus.NotFound => NotFound(SourceMovieNotFound()),
            MovieFriendAccessStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, MovieListForbidden()),
            _ => Ok(new ImportFriendMovieResponseDto { MovieId = result.MovieId! })
        };
    }

    [HttpGet("{movieId}")]
    public async Task<IActionResult> GetById([FromRoute] string movieId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var movie = await movieService.GetByIdAsync(userId, movieId);
        return movie is null ? NotFound(MovieNotFound()) : Ok(ToDto(movie));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertMovieDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var validation = ValidateMovie(dto);
        if (validation.Count > 0)
            return BadRequest(ValidationError(validation));

        var movie = await movieService.CreateAsync(userId, ToMovie(dto));
        return CreatedAtAction(nameof(GetById), new { movieId = movie.MovieId }, new CreateMovieResponseDto
        {
            MovieId = movie.MovieId
        });
    }

    [HttpPut("{movieId}")]
    public async Task<IActionResult> Update([FromRoute] string movieId, [FromBody] UpsertMovieDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var validation = ValidateMovie(dto);
        if (validation.Count > 0)
            return BadRequest(ValidationError(validation));

        var movie = await movieService.UpdateAsync(userId, movieId, ToMovie(dto));
        return movie is null
            ? NotFound(MovieNotFound())
            : Ok(new UpdateMovieResponseDto { MovieId = movie.MovieId, UpdatedAt = movie.UpdatedAt });
    }

    [HttpDelete("{movieId}")]
    public async Task<IActionResult> Delete([FromRoute] string movieId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var deleted = await movieService.DeleteAsync(userId, movieId);
        return deleted ? NoContent() : NotFound(MovieNotFound());
    }

    private string? GetUserId()
    {
        return userManager.GetUserId(User);
    }

    private static MovieDto ToDto(Movie movie)
    {
        return new MovieDto
        {
            MovieId = movie.MovieId,
            Title = movie.Title,
            ContentType = movie.ContentType,
            Rating = movie.Rating,
            Genres = movie.Genres,
            Country = movie.Country,
            Comment = movie.Comment,
            CreatedAt = movie.CreatedAt,
            UpdatedAt = movie.UpdatedAt
        };
    }

    private static SharedMovieListItemDto ToSharedDto(SharedMovieListItem movie)
    {
        return new SharedMovieListItemDto
        {
            MovieId = movie.MovieId,
            Title = movie.Title,
            ContentType = movie.ContentType,
            Rating = movie.Rating,
            Genres = movie.Genres,
            Country = movie.Country,
            Comment = movie.Comment,
            CreatedAt = movie.CreatedAt,
            UpdatedAt = movie.UpdatedAt,
            OwnerUserId = movie.OwnerUserId,
            OwnerNickname = movie.OwnerNickname,
            IsInMyList = movie.IsInMyList
        };
    }

    private static Movie ToMovie(UpsertMovieDto dto)
    {
        return new Movie
        {
            Title = dto.Title ?? string.Empty,
            ContentType = dto.ContentType!.Value,
            Rating = dto.Rating,
            Genres = dto.Genres ?? [],
            Country = dto.Country,
            Comment = dto.Comment
        };
    }

    private ErrorResponseDto ValidationError(List<ErrorDetailDto> details)
    {
        return new ErrorResponseDto
        {
            ErrorCode = "VALIDATION_ERROR",
            Message = "Ошибка валидации",
            Details = details,
            TraceId = HttpContext.TraceIdentifier
        };
    }

    private ErrorResponseDto MovieNotFound()
    {
        return new ErrorResponseDto
        {
            ErrorCode = "MOVIE_NOT_FOUND",
            Message = "Фильм не найден",
            TraceId = HttpContext.TraceIdentifier
        };
    }

    private ErrorResponseDto UserNotFound()
    {
        return new ErrorResponseDto
        {
            ErrorCode = "USER_NOT_FOUND",
            Message = "Пользователь не найден",
            TraceId = HttpContext.TraceIdentifier
        };
    }

    private ErrorResponseDto MovieListForbidden()
    {
        return new ErrorResponseDto
        {
            ErrorCode = "MOVIE_LIST_FORBIDDEN",
            Message = "Доступ к списку фильмов закрыт",
            TraceId = HttpContext.TraceIdentifier
        };
    }

    private ErrorResponseDto SourceMovieNotFound()
    {
        return new ErrorResponseDto
        {
            ErrorCode = "SOURCE_MOVIE_NOT_FOUND",
            Message = "Исходный фильм не найден",
            TraceId = HttpContext.TraceIdentifier
        };
    }

    private static List<ErrorDetailDto> ValidateMovie(UpsertMovieDto dto)
    {
        var details = new List<ErrorDetailDto>();

        if (string.IsNullOrWhiteSpace(dto.Title))
            details.Add(new ErrorDetailDto { Field = "title", Message = "Название обязательно" });
        else if (dto.Title.Trim().Length > 120)
            details.Add(new ErrorDetailDto { Field = "title", Message = "Название должно быть не длиннее 120 символов" });

        if (dto.ContentType is null || !Enum.IsDefined(dto.ContentType.Value))
            details.Add(new ErrorDetailDto { Field = "contentType", Message = "Некорректный тип контента" });

        if (dto.Rating is < 0 or > 10)
            details.Add(new ErrorDetailDto { Field = "rating", Message = "Рейтинг должен быть от 0 до 10" });

        if (dto.Genres is null || dto.Genres.Count == 0 || dto.Genres.All(string.IsNullOrWhiteSpace))
            details.Add(new ErrorDetailDto { Field = "genres", Message = "Укажите хотя бы один жанр" });

        if (dto.Country?.Length > 80)
            details.Add(new ErrorDetailDto { Field = "country", Message = "Страна должна быть не длиннее 80 символов" });

        if (dto.Comment?.Length > 5000)
            details.Add(new ErrorDetailDto { Field = "comment", Message = "Комментарий должен быть не длиннее 5000 символов" });

        return details;
    }

    private static List<string> ParseCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static List<T> ParseEnums<T>(string? value) where T : struct
    {
        return ParseCsv(value)
            .Select(x => Enum.TryParse<T>(x, true, out var parsed) ? parsed : (T?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
    }
}
