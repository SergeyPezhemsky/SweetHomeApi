namespace Application.Modules.Books;

public interface IBookService
{
    Task<BookListResult> GetListAsync(string userId, BookQuery query);
    Task<Book?> GetByIdAsync(string userId, string id);
    Task<Book> CreateAsync(string userId, Book book);
    Task<Book?> UpdateAsync(string userId, string id, Book book);
    Task<bool> DeleteAsync(string userId, string id);
}
