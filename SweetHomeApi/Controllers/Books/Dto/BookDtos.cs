using Application.Modules.Books;

namespace SweetHomeApi.Controllers.Books.Dto;

public class UpsertBookDto
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Genre { get; set; }
    public BookStatus Status { get; set; } = BookStatus.WANT_TO_READ;
    public int? TotalPages { get; set; }
    public int PagesRead { get; set; }
    public int? Rating { get; set; }
    public string? Notes { get; set; }

    public Book ToBook() => new()
    {
        Title = Title ?? string.Empty, Author = Author ?? string.Empty, Genre = Genre,
        Status = Status, TotalPages = TotalPages, PagesRead = PagesRead, Rating = Rating, Notes = Notes
    };
}

public record BookDto(string Id, string Title, string Author, string? Genre, BookStatus Status,
    int? TotalPages, int PagesRead, int? Rating, string? Notes, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static BookDto FromBook(Book book) => new(book.Id, book.Title, book.Author, book.Genre, book.Status,
        book.TotalPages, book.PagesRead, book.Rating, book.Notes, book.CreatedAt, book.UpdatedAt);
}
