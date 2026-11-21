using Microsoft.AspNetCore.Identity;

namespace Application.Modules.Auth;

public sealed class ApplicationUser : IdentityUser
{
    public bool IsAdmin { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public DateTime? RegisteredAt { get; set; }
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}
