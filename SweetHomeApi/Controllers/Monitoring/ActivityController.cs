using Application.Modules.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Persistance;
using SweetHomeApi.Infrastructure.Monitoring;

namespace SweetHomeApi.Controllers.Monitoring;

[ApiController]
[Authorize]
[Route("api/Activity")]
public sealed class ActivityController(SweetHomeDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    private static readonly HashSet<string> Sections =
        ["main", "home", "health", "movies", "wardrobe", "finance", "books", "coins", "profile"];

    [HttpPost("Visits")]
    [Consumes("application/json")]
    public async Task<IActionResult> RecordVisit(SectionVisit visit, CancellationToken cancellationToken)
    {
        if (visit.Section is null || !Sections.Contains(visit.Section))
            return BadRequest();
        if (users.GetUserId(User) is not { } userId)
            return Forbid();

        var now = DateTime.UtcNow;
        var day = MonitoringCalendar.Day(now);
        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DailySectionActivities" ("Day", "UserId", "Section", "Visits", "LastSeenAt")
            SELECT {day}, "Id", {visit.Section}, 1, {now} FROM "AspNetUsers"
            WHERE "Id" = {userId} AND "ApprovalStatus" = {nameof(ApprovalStatus.Approved)}
            ON CONFLICT ("Day", "UserId", "Section") DO UPDATE
            SET "Visits" = "DailySectionActivities"."Visits" + 1,
                "LastSeenAt" = GREATEST("DailySectionActivities"."LastSeenAt", EXCLUDED."LastSeenAt")
            """, cancellationToken);

        return updated == 1 ? NoContent() : Forbid();
    }

    public sealed record SectionVisit(string Section);
}
