using Application.Modules.Movies;
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
