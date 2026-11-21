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
[Route("api/Admin/Monitoring")]
public sealed class AdminMonitoringController(SweetHomeDbContext db, UserManager<ApplicationUser> users)
    : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetMonitoring(CancellationToken cancellationToken)
    {
        if (await users.GetUserAsync(User) is not { IsAdmin: true, ApprovalStatus: ApprovalStatus.Approved })
            return Forbid();

        var now = DateTime.UtcNow;
        var today = MonitoringCalendar.Day(now);
        var firstDay = today.AddDays(-29);
        var month = db.DailySectionActivities.Where(activity => activity.Day >= firstDay && activity.Day <= today);

        var activeToday = await (
            from activity in month
            where activity.Day == today
            join user in db.Users on activity.UserId equals user.Id
            group activity by new { user.Id, user.UserName } into userActivity
            orderby userActivity.Max(activity => activity.LastSeenAt) descending, userActivity.Key.Id
            select new
            {
                userActivity.Key.Id,
                Name = userActivity.Key.UserName,
                LastSeenAt = userActivity.Max(activity => activity.LastSeenAt)
            }).ToListAsync(cancellationToken);

        var mau = await month.Select(activity => activity.UserId).Distinct().CountAsync(cancellationToken);
        var sections = await month.GroupBy(activity => activity.Section)
            .Select(section => new
            {
                Section = section.Key,
                Users = section.Select(activity => activity.UserId).Distinct().Count(),
                Visits = section.Sum(activity => activity.Visits)
            })
            .OrderByDescending(section => section.Users)
            .ThenByDescending(section => section.Visits)
            .ThenBy(section => section.Section)
            .ToListAsync(cancellationToken);

        var errors = await db.ApiFailures.AsNoTracking()
            .OrderByDescending(failure => failure.OccurredAt)
            .ThenByDescending(failure => failure.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Ok(new { day = today, updatedAt = now, dau = activeToday.Count, mau, sections, activeToday, errors });
    }
}
