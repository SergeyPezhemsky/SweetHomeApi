namespace Application.Modules.Movies;

public interface IMovieService
{
    Task<MovieListResult> GetListAsync(string userId, MovieQuery query);
    Task<Movie?> GetByIdAsync(string userId, string movieId);
    Task<Movie> CreateAsync(string userId, Movie movie);
    Task<Movie?> UpdateAsync(string userId, string movieId, Movie movie);
    Task<bool> DeleteAsync(string userId, string movieId);
    Task<MovieDictionary> GetDictionariesAsync(string userId);
}
