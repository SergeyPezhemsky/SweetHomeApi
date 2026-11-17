using Application.Modules.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Persistance;

namespace SweetHomeApi.Tests.Books;

public class BookServiceTests
{
    [Fact]
    public async Task CreateNormalizesTextAndAssignsOwnershipAndTimestamps()
    {
        var repository = new FakeBookRepository();
        var book = NewBook();
        book.Title = "  Дюна  ";
        book.Author = "  Фрэнк Герберт ";
        book.Genre = "  ";
        book.Notes = " заметка ";
        book.UserId = "forged-owner";
        book.Id = "forged-id";

        var result = await new BookService(repository).CreateAsync("owner", book);

        Assert.Equal("Дюна", result.Title);
        Assert.Equal("Фрэнк Герберт", result.Author);
        Assert.Null(result.Genre);
        Assert.Equal("заметка", result.Notes);
        Assert.Equal("owner", result.UserId);
        Assert.NotEqual("forged-id", result.Id);
        Assert.Equal(DateTimeKind.Utc, result.CreatedAt.Kind);
        Assert.Equal(result.CreatedAt, result.UpdatedAt);
        Assert.Same(result, Assert.Single(repository.Books));
    }

    [Theory]
    [InlineData(BookStatus.COMPLETED, 640)]
    [InlineData(BookStatus.READING, 12)]
    [InlineData(BookStatus.ON_HOLD, 12)]
    public async Task CompletionSetsAllKnownPagesOnlyForCompletedStatus(BookStatus status, int expectedPages)
    {
        var book = NewBook();
        book.Status = status;
        book.TotalPages = 640;
        book.PagesRead = 12;
        var result = await new BookService(new FakeBookRepository()).CreateAsync("owner", book);
        Assert.Equal(expectedPages, result.PagesRead);
        Assert.Equal(status, result.Status);
    }

    [Fact]
    public async Task CompletingWithoutKnownLengthPreservesReadPages()
    {
        var book = NewBook();
        book.Status = BookStatus.COMPLETED;
        book.PagesRead = 200;
        var result = await new BookService(new FakeBookRepository()).CreateAsync("owner", book);
        Assert.Null(result.TotalPages);
        Assert.Equal(200, result.PagesRead);
    }

    [Fact]
    public async Task ReachingLastPageDoesNotChangeUserSelectedStatus()
    {
        var book = NewBook();
        book.Status = BookStatus.READING;
        book.TotalPages = book.PagesRead = 100;
        var result = await new BookService(new FakeBookRepository()).CreateAsync("owner", book);
        Assert.Equal(BookStatus.READING, result.Status);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("author")]
    [InlineData("genre")]
    [InlineData("notes")]
    [InlineData("status")]
    [InlineData("totalPages")]
    [InlineData("pagesRead")]
    [InlineData("rating")]
    public async Task InvalidInputDoesNotWriteAndNamesInvalidField(string field)
    {
        var repository = new FakeBookRepository();
        var book = NewBook();
        switch (field)
        {
            case "title": book.Title = "  "; break;
            case "author": book.Author = new string('a', 201); break;
            case "genre": book.Genre = new string('a', 101); break;
            case "notes": book.Notes = new string('a', 5001); break;
            case "status": book.Status = (BookStatus)99; break;
            case "totalPages": book.TotalPages = 0; break;
            case "pagesRead": book.TotalPages = 10; book.PagesRead = 11; break;
            case "rating": book.Rating = 6; break;
        }
        var exception = await Assert.ThrowsAsync<BookValidationException>(() => new BookService(repository).CreateAsync("owner", book));
        Assert.Contains(field, exception.Errors.Keys);
        Assert.Empty(repository.Books);
    }

    [Fact]
    public async Task InvalidUpdateLeavesExistingRecordUnchanged()
    {
        var repository = new FakeBookRepository();
        var service = new BookService(repository);
        var original = await service.CreateAsync("owner", NewBook());
        var edited = NewBook();
        edited.Title = "Changed";
        edited.PagesRead = -1;
        await Assert.ThrowsAsync<BookValidationException>(() => service.UpdateAsync("owner", original.Id, edited));
        Assert.Equal("Дюна", original.Title);
        Assert.Equal(0, original.PagesRead);
        Assert.Equal(0, repository.Updates);
    }

    [Fact]
    public async Task OtherUserCannotReadUpdateOrDeleteBook()
    {
        var repository = new FakeBookRepository();
        var service = new BookService(repository);
        var book = await service.CreateAsync("owner", NewBook());
        Assert.Null(await service.GetByIdAsync("stranger", book.Id));
        Assert.Null(await service.UpdateAsync("stranger", book.Id, NewBook()));
        Assert.False(await service.DeleteAsync("stranger", book.Id));
        Assert.Same(book, Assert.Single(repository.Books));
        Assert.Equal(0, repository.Updates);
    }

    [Fact]
    public async Task UpdatePreservesIdentityOwnerAndCreationTime()
    {
        var repository = new FakeBookRepository();
        var service = new BookService(repository);
        var book = await service.CreateAsync("owner", NewBook());
        var created = book.CreatedAt;
        var input = NewBook();
        input.Title = "Мессия Дюны";
        input.UserId = "forged";
        input.Status = BookStatus.COMPLETED;
        input.TotalPages = 300;
        var updated = await service.UpdateAsync("owner", book.Id, input);
        Assert.Equal(book.Id, updated!.Id);
        Assert.Equal("owner", updated.UserId);
        Assert.Equal(created, updated.CreatedAt);
        Assert.True(updated.UpdatedAt >= created);
        Assert.Equal("Мессия Дюны", updated.Title);
        Assert.Equal(300, updated.PagesRead);
        Assert.Equal(1, repository.Updates);
        Assert.True(await service.DeleteAsync("owner", book.Id));
        Assert.Empty(repository.Books);
    }

    [Fact]
    public async Task QueryBoundsAvoidOverflowAndNormalizeSearch()
    {
        var repository = new FakeBookRepository();
        var query = new BookQuery { Query = "  Герберт ", Page = int.MaxValue, PageSize = int.MaxValue };
        await new BookService(repository).GetListAsync("owner", query);
        Assert.Equal("Герберт", query.Query);
        Assert.Equal(100, query.PageSize);
        Assert.InRange((long)(query.Page - 1) * query.PageSize, 0, int.MaxValue);
        Assert.Equal("owner", repository.LastListUser);
    }

    [Fact]
    public async Task UnknownQueryEnumValuesAreRejected()
    {
        var service = new BookService(new FakeBookRepository());
        var exception = await Assert.ThrowsAsync<BookValidationException>(() => service.GetListAsync("owner", new BookQuery
        {
            Status = (BookStatus)999, SortBy = (BookSortBy)999
        }));
        Assert.Contains("status", exception.Errors.Keys);
        Assert.Contains("sortBy", exception.Errors.Keys);
    }

    [Fact]
    public void PostgreSqlModelHasOwnerCascadeAndMigrationCreatesBooksTable()
    {
        using var context = new SweetHomeDbContext(new DbContextOptionsBuilder<SweetHomeDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var entity = context.Model.FindEntityType(typeof(Book))!;
        Assert.Equal("Books", entity.GetTableName());
        Assert.Equal(300, entity.FindProperty(nameof(Book.Title))!.GetMaxLength());
        Assert.Equal(DeleteBehavior.Cascade, Assert.Single(entity.GetForeignKeys()).DeleteBehavior);
        var sql = context.Books.Where(book => book.UserId == "owner" && book.Id == "book-id").ToQueryString();
        Assert.Contains("\"UserId\" = 'owner'", sql);
        Assert.Contains("\"Id\" = 'book-id'", sql);
        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE \"Books\"", script);
        Assert.Contains("FK_Books_AspNetUsers_UserId", script);
    }

    private static Book NewBook() => new() { Title = "Дюна", Author = "Фрэнк Герберт" };

    private sealed class FakeBookRepository : IBookRepository
    {
        public List<Book> Books { get; } = [];
        public int Updates { get; private set; }
        public string? LastListUser { get; private set; }
        public Task<BookListResult> GetListAsync(string userId, BookQuery query)
        {
            LastListUser = userId;
            return Task.FromResult(new BookListResult { Items = Books.Where(b => b.UserId == userId).ToList() });
        }
        public Task<Book?> GetByIdAsync(string userId, string id) => Task.FromResult(Books.FirstOrDefault(b => b.UserId == userId && b.Id == id));
        public Task AddAsync(Book book) { Books.Add(book); return Task.CompletedTask; }
        public Task UpdateAsync(Book book) { Updates++; return Task.CompletedTask; }
        public Task DeleteAsync(Book book) { Books.Remove(book); return Task.CompletedTask; }
    }
}
