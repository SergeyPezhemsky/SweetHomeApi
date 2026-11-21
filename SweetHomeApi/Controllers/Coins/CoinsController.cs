using Application.Modules.Auth;
using Application.Modules.Coins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Persistance;
using SweetHomeApi.Controllers.Wardrobe;

namespace SweetHomeApi.Controllers.Coins;

[ApiController]
[Authorize]
[Route("api/v1/coins")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CoinsController(SweetHomeDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    private const int MaxPhotoBytes = 2 * 1024 * 1024;
    private const int MaxRequestBytes = 6 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken)
    {
        if (users.GetUserId(User) is not { } userId) return Unauthorized();
        var result = await Owned(userId).OrderByDescending(coin => coin.CreatedAt).ThenBy(coin => coin.Id)
            .Select(CoinDto.Projection).ToListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (users.GetUserId(User) is not { } userId) return Unauthorized();
        var coin = await FindAsync(userId, id, cancellationToken);
        return coin is null ? NotFound() : Ok(coin);
    }

    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Create(UpsertCoinDto request, CancellationToken cancellationToken)
    {
        if (users.GetUserId(User) is not { } userId) return Unauthorized();
        Validate(request);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var coin = PrepareCoin(request);
        coin.UserId = userId;
        coin.CreatedAt = coin.UpdatedAt;
        db.Coins.Add(coin);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = coin.Id }, await FindAsync(userId, coin.Id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Update(Guid id, UpsertCoinDto request, CancellationToken cancellationToken)
    {
        if (users.GetUserId(User) is not { } userId) return Unauthorized();
        Validate(request);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var updated = PrepareCoin(request);
        var changeObverse = request.Obverse is not null;
        var changeReverse = request.Reverse is not null;
        var rows = await Owned(userId).Where(coin => coin.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(coin => coin.Name, updated.Name)
            .SetProperty(coin => coin.Country, updated.Country)
            .SetProperty(coin => coin.Year, updated.Year)
            .SetProperty(coin => coin.Value, updated.Value)
            .SetProperty(coin => coin.Grade, updated.Grade)
            .SetProperty(coin => coin.Quantity, updated.Quantity)
            .SetProperty(coin => coin.Location, updated.Location)
            .SetProperty(coin => coin.Notes, updated.Notes)
            .SetProperty(coin => coin.Obverse, coin => changeObverse ? updated.Obverse : coin.Obverse)
            .SetProperty(coin => coin.Reverse, coin => changeReverse ? updated.Reverse : coin.Reverse)
            .SetProperty(coin => coin.UpdatedAt, updated.UpdatedAt), cancellationToken);

        var coin = rows == 0 ? null : await FindAsync(userId, id, cancellationToken);
        return coin is null ? NotFound() : Ok(coin);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (users.GetUserId(User) is not { } userId) return Unauthorized();
        var rows = await Owned(userId).Where(coin => coin.Id == id).ExecuteDeleteAsync(cancellationToken);
        return rows == 0 ? NotFound() : NoContent();
    }

    [HttpGet("{id:guid}/photos/{side}")]
    public async Task<IActionResult> GetPhoto(Guid id, string side, CancellationToken cancellationToken)
    {
        if (users.GetUserId(User) is not { } userId) return Unauthorized();
        if (side is not ("obverse" or "reverse")) return NotFound();

        var data = await Owned(userId).Where(coin => coin.Id == id)
            .Select(coin => side == "obverse" ? coin.Obverse : coin.Reverse)
            .SingleOrDefaultAsync(cancellationToken);

        if (data is null || WardrobeController.DetectImageContentType(data) is not { } contentType)
            return NotFound();

        Response.Headers.XContentTypeOptions = "nosniff";
        return File(data, contentType);
    }

    private IQueryable<Coin> Owned(string userId) => db.Coins.AsNoTracking().Where(coin => coin.UserId == userId);

    private Task<CoinDto?> FindAsync(string userId, Guid id, CancellationToken cancellationToken)
        => Owned(userId).Where(coin => coin.Id == id).Select(CoinDto.Projection).SingleOrDefaultAsync(cancellationToken);

    private static Coin PrepareCoin(UpsertCoinDto request) => new()
    {
        Name = request.Name.Trim(),
        Country = OptionalText(request.Country),
        Year = request.Year,
        Value = OptionalText(request.Value),
        Grade = OptionalText(request.Grade),
        Quantity = request.Quantity,
        Location = OptionalText(request.Location),
        Notes = OptionalText(request.Notes),
        Obverse = request.Obverse?.Data,
        Reverse = request.Reverse?.Data,
        UpdatedAt = DateTime.UtcNow
    };

    private static string? OptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Validate(UpsertCoinDto request)
    {
        ValidatePhoto(nameof(request.Obverse), request.Obverse);
        ValidatePhoto(nameof(request.Reverse), request.Reverse);
        ValidateText(nameof(request.Name), request.Name);
        ValidateText(nameof(request.Country), request.Country);
        ValidateText(nameof(request.Value), request.Value);
        ValidateText(nameof(request.Location), request.Location);
        ValidateText(nameof(request.Notes), request.Notes, multiline: true);
    }

    private void ValidateText(string field, string? value, bool multiline = false)
    {
        if (value?.Any(character => char.IsControl(character) && !(multiline && character is '\r' or '\n' or '\t')) == true)
            ModelState.AddModelError(field, "Поле содержит недопустимые символы.");
    }

    private void ValidatePhoto(string field, CoinPhotoChange? change)
    {
        if (change is null) return;
        if (change.Remove && change.Data is null) return;
        if (change.Remove || change.Data is null || change.Data.Length > MaxPhotoBytes ||
            WardrobeController.DetectImageContentType(change.Data) is null)
            ModelState.AddModelError(field, "Загрузите фото в формате JPG, PNG или WebP размером до 2 МБ.");
    }
}
