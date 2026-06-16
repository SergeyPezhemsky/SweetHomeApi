using Application.Modules.Movies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Persistance.Repositories;

public class MovieRepository(SweetHomeDbContext context) : IMovieRepository
{
    public async Task<MovieListResult> GetListAsync(string userId, MovieQuery query)
    {
        var movies = context.Set<Movie>()
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var search = query.Query.Trim();
            movies = movies.Where(x =>
                EF.Functions.ILike(x.Title, $"%{search}%")
                || (x.Comment != null && EF.Functions.ILike(x.Comment, $"%{search}%")));
        }

        if (query.ContentTypes.Count > 0)
            movies = movies.Where(x => query.ContentTypes.Contains(x.ContentType));

        if (query.Genres.Count > 0)
            movies = movies.Where(x => x.Genres.Any(genre => query.Genres.Contains(genre)));

        if (query.Countries.Count > 0)
            movies = movies.Where(x => x.Country != null && query.Countries.Contains(x.Country));

        if (query.RatingFrom.HasValue)
            movies = movies.Where(x => x.Rating.HasValue && x.Rating >= query.RatingFrom.Value);

        if (query.RatingTo.HasValue)
            movies = movies.Where(x => x.Rating.HasValue && x.Rating <= query.RatingTo.Value);

        movies = ApplySorting(movies, query);

        var total = await movies.CountAsync();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var items = await movies
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new MovieListResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total,
            HasNext = page * pageSize < total
        };
    }

    public Task<Movie?> GetByIdAsync(string userId, string movieId)
    {
        return context.Set<Movie>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == movieId);
    }

    public Task<Movie?> GetByIdAsync(string movieId)
    {
        return context.Set<Movie>()
            .FirstOrDefaultAsync(x => x.MovieId == movieId);
    }

    public async Task AddAsync(Movie movie)
    {
        await context.Set<Movie>().AddAsync(movie);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Movie movie)
    {
        context.Set<Movie>().Update(movie);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Movie movie)
    {
        context.Set<Movie>().Remove(movie);
        await context.SaveChangesAsync();
    }

    public async Task<List<string>> GetExistingGenresAsync(string userId)
    {
        return await context.Set<Movie>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .SelectMany(x => x.Genres)
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<string>> GetExistingCountriesAsync(string userId)
    {
        return await context.Set<Movie>()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Country != null)
            .Select(x => x.Country!)
            .Distinct()
            .ToListAsync();
    }

    public async Task<MovieFriendSearchResult> SearchFriendsAsync(string userId, string? query)
    {
        var friends = context.Set<MovieShareSetting>()
            .AsNoTracking()
            .Where(x => x.ShareMovies && x.UserId != userId)
            .Join(
                context.Set<IdentityUser>().AsNoTracking(),
                setting => setting.UserId,
                user => user.Id,
                (setting, user) => user);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim();
            friends = friends.Where(x => x.UserName != null && EF.Functions.ILike(x.UserName, $"%{search}%"));
        }

        var total = await friends.CountAsync();
        var items = await friends
            .OrderBy(x => x.UserName)
            .Select(x => new MovieFriendSummary
            {
                UserId = x.Id,
                Nickname = x.UserName ?? string.Empty,
                MoviesCount = context.Set<Movie>().Count(movie => movie.UserId == x.Id)
            })
            .ToListAsync();

        return new MovieFriendSearchResult
        {
            Items = items,
            Total = total
        };
    }

    public async Task<bool> GetShareMoviesAsync(string userId)
    {
        return await context.Set<MovieShareSetting>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ShareMovies)
            .FirstOrDefaultAsync();
    }

    public async Task SetShareMoviesAsync(string userId, bool shareMovies)
    {
        var setting = await context.Set<MovieShareSetting>()
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (setting is null)
        {
            setting = new MovieShareSetting
            {
                UserId = userId,
                ShareMovies = shareMovies
            };
            await context.Set<MovieShareSetting>().AddAsync(setting);
        }
        else
        {
            setting.ShareMovies = shareMovies;
        }

        await context.SaveChangesAsync();
    }

    public async Task<MovieFriendMoviesResult> GetFriendMoviesAsync(string userId, string friendUserId, int page, int pageSize)
    {
        var owner = await context.Set<IdentityUser>()
            .AsNoTracking()
            .Where(x => x.Id == friendUserId)
            .Select(x => new { x.Id, x.UserName })
            .FirstOrDefaultAsync();

        if (owner is null)
            return new MovieFriendMoviesResult { Status = MovieFriendAccessStatus.NotFound };

        if (!await UserSharesMoviesAsync(friendUserId))
            return new MovieFriendMoviesResult { Status = MovieFriendAccessStatus.Forbidden };

        var movies = context.Set<Movie>()
            .AsNoTracking()
            .Where(x => x.UserId == friendUserId)
            .OrderByDescending(x => x.UpdatedAt);

        var total = await movies.CountAsync();
        var importedMovieIds = context.Set<Movie>()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.ImportedFromMovieId != null)
            .Select(x => x.ImportedFromMovieId!);

        var items = await movies
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SharedMovieListItem
            {
                MovieId = x.MovieId,
                Title = x.Title,
                ContentType = x.ContentType,
                Rating = x.Rating,
                Genres = x.Genres,
                Country = x.Country,
                Comment = x.Comment,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                OwnerUserId = owner.Id,
                OwnerNickname = owner.UserName ?? string.Empty,
                IsInMyList = importedMovieIds.Contains(x.MovieId)
            })
            .ToListAsync();

        return new MovieFriendMoviesResult
        {
            Status = MovieFriendAccessStatus.Ok,
            OwnerUserId = owner.Id,
            OwnerNickname = owner.UserName ?? string.Empty,
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total,
            HasNext = page * pageSize < total
        };
    }

    public Task<Movie?> GetImportedMovieAsync(string userId, string sourceMovieId)
    {
        return context.Set<Movie>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ImportedFromMovieId == sourceMovieId);
    }

    public Task<bool> UserExistsAsync(string userId)
    {
        return context.Set<IdentityUser>()
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId);
    }

    public Task<bool> UserSharesMoviesAsync(string userId)
    {
        return context.Set<MovieShareSetting>()
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.ShareMovies);
    }

    private static IQueryable<Movie> ApplySorting(IQueryable<Movie> movies, MovieQuery query)
    {
        return (query.SortBy, query.SortDirection) switch
        {
            (MovieSortBy.TITLE, SortDirection.ASC) => movies.OrderBy(x => x.Title),
            (MovieSortBy.TITLE, SortDirection.DESC) => movies.OrderByDescending(x => x.Title),
            (MovieSortBy.RATING, SortDirection.ASC) => movies.OrderBy(x => x.Rating == null).ThenBy(x => x.Rating),
            (MovieSortBy.RATING, SortDirection.DESC) => movies.OrderBy(x => x.Rating == null).ThenByDescending(x => x.Rating),
            (MovieSortBy.CREATED_AT, SortDirection.ASC) => movies.OrderBy(x => x.CreatedAt),
            (MovieSortBy.CREATED_AT, SortDirection.DESC) => movies.OrderByDescending(x => x.CreatedAt),
            (MovieSortBy.UPDATED_AT, SortDirection.ASC) => movies.OrderBy(x => x.UpdatedAt),
            _ => movies.OrderByDescending(x => x.UpdatedAt)
        };
    }
}
