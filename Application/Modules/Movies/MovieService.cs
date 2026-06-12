using Application.Modules.Movies.Seeds;

namespace Application.Modules.Movies;

public class MovieService(IMovieRepository movieRepository) : IMovieService
{
    public Task<MovieListResult> GetListAsync(string userId, MovieQuery query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, 100);
        return movieRepository.GetListAsync(userId, query);
    }

    public Task<Movie?> GetByIdAsync(string userId, string movieId)
    {
        return movieRepository.GetByIdAsync(userId, movieId);
    }

    public async Task<Movie> CreateAsync(string userId, Movie movie)
    {
        var now = DateTime.UtcNow;
        movie.MovieId = Guid.NewGuid().ToString();
        movie.UserId = userId;
        movie.CreatedAt = now;
        movie.UpdatedAt = now;
        NormalizeMovie(movie);

        await movieRepository.AddAsync(movie);
        return movie;
    }

    public async Task<Movie?> UpdateAsync(string userId, string movieId, Movie movie)
    {
        var existing = await movieRepository.GetByIdAsync(userId, movieId);
        if (existing is null)
            return null;

        existing.Title = movie.Title;
        existing.ContentType = movie.ContentType;
        existing.Rating = movie.Rating;
        existing.Genres = movie.Genres;
        existing.Country = movie.Country;
        existing.Comment = movie.Comment;
        existing.UpdatedAt = DateTime.UtcNow;
        NormalizeMovie(existing);

        await movieRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task<bool> DeleteAsync(string userId, string movieId)
    {
        var existing = await movieRepository.GetByIdAsync(userId, movieId);
        if (existing is null)
            return false;

        await movieRepository.DeleteAsync(existing);
        return true;
    }

    public async Task<MovieDictionary> GetDictionariesAsync(string userId)
    {
        var genres = MovieDictionaries.Genres
            .Union(await movieRepository.GetExistingGenresAsync(userId))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var countries = MovieDictionaries.Countries
            .Union(await movieRepository.GetExistingCountriesAsync(userId))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        return new MovieDictionary
        {
            ContentTypes = MovieDictionaries.ContentTypes,
            Genres = genres,
            Countries = countries
        };
    }

    private static void NormalizeMovie(Movie movie)
    {
        movie.Title = movie.Title.Trim();
        movie.Genres = movie.Genres
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        movie.Country = string.IsNullOrWhiteSpace(movie.Country) ? null : movie.Country.Trim();
        movie.Comment = string.IsNullOrWhiteSpace(movie.Comment) ? null : movie.Comment.Trim();
    }
}
