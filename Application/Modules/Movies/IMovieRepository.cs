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
    Task<MovieFriendSearchResult> SearchFriendsAsync(string userId, string? query);
    Task<bool> GetShareMoviesAsync(string userId);
    Task SetShareMoviesAsync(string userId, bool shareMovies);
    Task<MovieFriendMoviesResult> GetFriendMoviesAsync(string userId, string friendUserId, int page, int pageSize);
    Task<Movie?> GetByIdAsync(string movieId);
    Task<Movie?> GetImportedMovieAsync(string userId, string sourceMovieId);
    Task<bool> UserExistsAsync(string userId);
    Task<bool> UserSharesMoviesAsync(string userId);
}
