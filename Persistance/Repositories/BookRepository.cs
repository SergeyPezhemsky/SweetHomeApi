using Application.Modules.Books;
using Microsoft.EntityFrameworkCore;

namespace Persistance.Repositories;

public class BookRepository(SweetHomeDbContext context) : IBookRepository
{
    public async Task<BookListResult> GetListAsync(string userId, BookQuery query)
    {
        var owned = context.Books.AsNoTracking().Where(x => x.UserId == userId);
        var stats = await owned.GroupBy(x => 1).Select(group => new BookStats
        {
            Total = group.Count(),
            WantToRead = group.Count(x => x.Status == BookStatus.WANT_TO_READ),
            Reading = group.Count(x => x.Status == BookStatus.READING),
            Completed = group.Count(x => x.Status == BookStatus.COMPLETED),
            OnHold = group.Count(x => x.Status == BookStatus.ON_HOLD),
            Dropped = group.Count(x => x.Status == BookStatus.DROPPED),
            PagesRead = group.Sum(x => (long)x.PagesRead)
        }).SingleOrDefaultAsync() ?? new BookStats();

        var books = owned;
        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var search = query.Query.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{search}%";
            books = books.Where(x => EF.Functions.ILike(x.Title, pattern, "\\") || EF.Functions.ILike(x.Author, pattern, "\\"));
        }
        if (query.Status.HasValue) books = books.Where(x => x.Status == query.Status.Value);
        var sorted = query.SortBy switch
        {
            BookSortBy.TITLE => books.OrderBy(x => x.Title),
            BookSortBy.AUTHOR => books.OrderBy(x => x.Author).ThenBy(x => x.Title),
            BookSortBy.RATING => books.OrderByDescending(x => x.Rating.HasValue).ThenByDescending(x => x.Rating),
            _ => books.OrderByDescending(x => x.UpdatedAt)
        };
        var total = await books.CountAsync();
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Clamp(query.Page, 1, int.MaxValue / pageSize);
        return new BookListResult
        {
            Items = await sorted.ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
            Page = page,
            PageSize = pageSize,
            Total = total,
            HasNext = (long)page * pageSize < total,
            Stats = stats
        };
    }

    public Task<Book?> GetByIdAsync(string userId, string id) => context.Books.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
    public async Task AddAsync(Book book) { context.Books.Add(book); await context.SaveChangesAsync(); }
    public async Task UpdateAsync(Book book) { context.Books.Update(book); await context.SaveChangesAsync(); }
    public async Task DeleteAsync(Book book) { context.Books.Remove(book); await context.SaveChangesAsync(); }
}
