using Microsoft.AspNetCore.Identity;

namespace Application.Modules.Books;

public enum BookStatus { WANT_TO_READ, READING, COMPLETED, ON_HOLD, DROPPED }
public enum BookSortBy { UPDATED_AT, TITLE, AUTHOR, RATING }

public class Book
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? Genre { get; set; }
    public BookStatus Status { get; set; } = BookStatus.WANT_TO_READ;
    public int? TotalPages { get; set; }
    public int PagesRead { get; set; }
    public int? Rating { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public IdentityUser User { get; set; } = null!;
}

public class BookQuery
{
    public string? Query { get; set; }
    public BookStatus? Status { get; set; }
    public BookSortBy SortBy { get; set; } = BookSortBy.UPDATED_AT;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public class BookStats
{
    public int Total { get; set; }
    public int WantToRead { get; set; }
    public int Reading { get; set; }
    public int Completed { get; set; }
    public int OnHold { get; set; }
    public int Dropped { get; set; }
    public long PagesRead { get; set; }
}

public class BookListResult
{
    public List<Book> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public bool HasNext { get; set; }
    public BookStats Stats { get; set; } = new();
}
