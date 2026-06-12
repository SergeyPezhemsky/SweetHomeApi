using Application.Modules.Movies;
using Microsoft.AspNetCore.Authorization;
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
