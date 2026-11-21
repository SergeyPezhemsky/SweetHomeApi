using Application.Modules.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SweetHomeApi.Controllers.Auth;

[ApiController]
[Authorize]
[Route("api/Account/RegistrationRequests")]
public sealed class RegistrationRequestsController(UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        if (!await IsAdminAsync())
            return Forbid();

        var requests = await users.Users.AsNoTracking()
            .Where(user => user.ApprovalStatus == ApprovalStatus.Pending)
            .OrderBy(user => user.RegisteredAt)
            .ThenBy(user => user.Id)
            .Select(user => new { user.Id, Name = user.UserName, user.Email, user.RegisteredAt })
            .ToListAsync(cancellationToken);

        return Ok(requests);
    }

    [HttpPut("{userId}")]
    [Consumes("application/json")]
    public async Task<IActionResult> Review(string userId, ReviewRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAdminAsync())
            return Forbid();

        if (request.Status is not (ApprovalStatus.Approved or ApprovalStatus.Rejected))
            return BadRequest(new { message = "Выберите одобрение или отклонение заявки." });

        var updated = await users.Users
            .Where(user => user.Id == userId && user.ApprovalStatus == ApprovalStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.ApprovalStatus, request.Status)
                .SetProperty(user => user.ConcurrencyStamp, Guid.NewGuid().ToString()), cancellationToken);

        if (updated == 1)
            return NoContent();

        var status = await users.Users
            .Where(user => user.Id == userId && user.RegisteredAt != null)
            .Select(user => (ApprovalStatus?)user.ApprovalStatus)
            .SingleOrDefaultAsync(cancellationToken);

        if (status is null)
            return NotFound(new { message = "Заявка не найдена." });

        return status == request.Status
            ? NoContent()
            : Conflict(new { message = "По этой заявке уже принято другое решение." });
    }

    private async Task<bool> IsAdminAsync() =>
        await users.GetUserAsync(User) is { IsAdmin: true, ApprovalStatus: ApprovalStatus.Approved };

    public sealed record ReviewRequest(ApprovalStatus Status);
}
