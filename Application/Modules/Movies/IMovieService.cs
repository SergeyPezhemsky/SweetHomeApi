namespace Application.Modules.Movies;

public interface IMovieService
{
    Task<MovieListResult> GetListAsync(string userId, MovieQuery query);
    Task<Movie?> GetByIdAsync(string userId, string movieId);
    Task<Movie> CreateAsync(string userId, Movie movie);
    Task<Movie?> UpdateAsync(string userId, string movieId, Movie movie);
    Task<bool> DeleteAsync(string userId, string movieId);
    Task<MovieDictionary> GetDictionariesAsync(string userId);
    Task<MovieFriendSearchResult> SearchFriendsAsync(string userId, string? query);
    Task<bool> GetShareMoviesAsync(string userId);
    Task<bool> UpdateShareMoviesAsync(string userId, bool shareMovies);
    Task<MovieFriendMoviesResult> GetFriendMoviesAsync(string userId, string friendUserId, int page, int pageSize);
    Task<MovieImportResult> ImportFriendMovieAsync(string userId, string sourceMovieId);
}
