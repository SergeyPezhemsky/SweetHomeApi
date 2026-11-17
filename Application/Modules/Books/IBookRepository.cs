namespace Application.Modules.Books;

public interface IBookRepository
{
    Task<BookListResult> GetListAsync(string userId, BookQuery query);
    Task<Book?> GetByIdAsync(string userId, string id);
    Task AddAsync(Book book);
    Task UpdateAsync(Book book);
    Task DeleteAsync(Book book);
}
