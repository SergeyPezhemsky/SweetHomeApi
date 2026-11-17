namespace Application.Modules.Books;

public sealed class BookValidationException(Dictionary<string, string[]> errors) : ArgumentException("Проверьте данные книги.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}

public class BookService(IBookRepository repository) : IBookService
{
    public Task<BookListResult> GetListAsync(string userId, BookQuery query)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            errors["status"] = ["Неизвестный статус чтения."];
        if (!Enum.IsDefined(query.SortBy))
            errors["sortBy"] = ["Неизвестный порядок сортировки."];
        if (errors.Count > 0) throw new BookValidationException(errors);
        query.Query = query.Query?.Trim();
        query.PageSize = Math.Clamp(query.PageSize, 1, 100);
        query.Page = Math.Clamp(query.Page, 1, int.MaxValue / query.PageSize);
        return repository.GetListAsync(userId, query);
    }

    public Task<Book?> GetByIdAsync(string userId, string id) => repository.GetByIdAsync(userId, id);

    public async Task<Book> CreateAsync(string userId, Book book)
    {
        NormalizeAndValidate(book);
        book.Id = Guid.NewGuid().ToString();
        book.UserId = userId;
        book.CreatedAt = book.UpdatedAt = DateTime.UtcNow;
        await repository.AddAsync(book);
        return book;
    }

    public async Task<Book?> UpdateAsync(string userId, string id, Book book)
    {
        var existing = await repository.GetByIdAsync(userId, id);
        if (existing is null) return null;
        NormalizeAndValidate(book);
        existing.Title = book.Title;
        existing.Author = book.Author;
        existing.Genre = book.Genre;
        existing.Status = book.Status;
        existing.TotalPages = book.TotalPages;
        existing.PagesRead = book.PagesRead;
        existing.Rating = book.Rating;
        existing.Notes = book.Notes;
        existing.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(existing);
        return existing;
    }

    public async Task<bool> DeleteAsync(string userId, string id)
    {
        var existing = await repository.GetByIdAsync(userId, id);
        if (existing is null) return false;
        await repository.DeleteAsync(existing);
        return true;
    }

    private static void NormalizeAndValidate(Book book)
    {
        book.Title = book.Title?.Trim() ?? string.Empty;
        book.Author = book.Author?.Trim() ?? string.Empty;
        book.Genre = BlankToNull(book.Genre);
        book.Notes = BlankToNull(book.Notes);
        var errors = new Dictionary<string, string[]>();
        if (book.Title.Length is < 1 or > 300) errors["title"] = ["Название: от 1 до 300 символов."];
        if (book.Author.Length is < 1 or > 200) errors["author"] = ["Автор: от 1 до 200 символов."];
        if (book.Genre?.Length > 100) errors["genre"] = ["Жанр: не более 100 символов."];
        if (book.Notes?.Length > 5000) errors["notes"] = ["Заметки: не более 5000 символов."];
        if (!Enum.IsDefined(book.Status)) errors["status"] = ["Неизвестный статус чтения."];
        if (book.TotalPages is < 1 or > 100000) errors["totalPages"] = ["Объём: от 1 до 100000 страниц."];
        if (book.PagesRead < 0 || book.PagesRead > 100000 || (book.TotalPages.HasValue && book.PagesRead > book.TotalPages))
            errors["pagesRead"] = ["Прочитанные страницы: от 0 до объёма книги (не более 100000)."];
        if (book.Rating is < 1 or > 5) errors["rating"] = ["Оценка: от 1 до 5."];
        if (errors.Count > 0) throw new BookValidationException(errors);
        if (book.Status == BookStatus.COMPLETED && book.TotalPages.HasValue) book.PagesRead = book.TotalPages.Value;
    }

    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
