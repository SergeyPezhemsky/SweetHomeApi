using Application.Modules.Books;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SweetHomeApi.Controllers.Books.Dto;

namespace SweetHomeApi.Controllers.Books;

[ApiController]
[Authorize]
[Route("api/v1/books")]
public class BooksController(IBookService service, UserManager<IdentityUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] BookQuery request)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        try
        {
            var result = await service.GetListAsync(userId, request);
            return Ok(new { Items = result.Items.Select(BookDto.FromBook), result.Page, result.PageSize, result.Total, result.HasNext, result.Stats });
        }
        catch (BookValidationException ex) { return InvalidBook(ex); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var book = await service.GetByIdAsync(userId, id);
        return book is null ? NotFound() : Ok(BookDto.FromBook(book));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertBookDto dto)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        try
        {
            var book = await service.CreateAsync(userId, dto.ToBook());
            return CreatedAtAction(nameof(GetById), new { id = book.Id }, BookDto.FromBook(book));
        }
        catch (BookValidationException ex) { return InvalidBook(ex); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpsertBookDto dto)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        try
        {
            var book = await service.UpdateAsync(userId, id, dto.ToBook());
            return book is null ? NotFound() : Ok(BookDto.FromBook(book));
        }
        catch (BookValidationException ex) { return InvalidBook(ex); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        return await service.DeleteAsync(userId, id) ? NoContent() : NotFound();
    }

    private ActionResult InvalidBook(BookValidationException exception)
    {
        foreach (var (field, messages) in exception.Errors)
            foreach (var message in messages) ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}
