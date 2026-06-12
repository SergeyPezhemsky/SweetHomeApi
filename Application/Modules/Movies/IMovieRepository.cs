namespace Application.Modules.Movies;

public interface IMovieRepository
{
    Task<MovieListResult> GetListAsync(string userId, MovieQuery query);
    Task<Movie?> GetByIdAsync(string userId, string movieId);
    Task AddAsync(Movie movie);
    Task UpdateAsync(Movie movie);
    Task DeleteAsync(Movie movie);
    Task<List<string>> GetExistingGenresAsync(string userId);
    Task<List<string>> GetExistingCountriesAsync(string userId);
}
