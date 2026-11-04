using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Services;

public interface IActivityLogger
{
    /// <summary>Writes one activity-log row. Never throws — auditing must not
    /// be able to break the page or action it is recording.</summary>
    /// <param name="userId">The acting account's id (null when it cannot be attributed).</param>
    /// <param name="actorName">The acting account's name, kept for readability after deletion.</param>
    /// <param name="role">The acting account's role ("Admin" or "User").</param>
    /// <param name="targetUserId">For an admin acting on another account, that account's id.</param>
    void Log(HttpContext context, int? userId, string actorName, string role, string action, string detail, int? targetUserId = null);
}

/// <summary>Database-backed audit trail for the Admin Console's Activity Logs page.</summary>
public class ActivityLogger : IActivityLogger
{
    private readonly ApplicationDbContext _db;

    public ActivityLogger(ApplicationDbContext db) => _db = db;

    public void Log(HttpContext context, int? userId, string actorName, string role, string action, string detail, int? targetUserId = null)
    {
        try
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            if (ip == "::1") ip = "127.0.0.1";

            _db.ActivityLogs.Add(new ActivityLog
            {
                UserId = userId,
                ActorName = actorName,
                Role = role,
                TargetUserId = targetUserId,
                Action = action,
                Detail = detail.Length > 500 ? detail[..500] : detail,
                IpAddress = ip,
                CreatedAt = DateTime.Now
            });
            _db.SaveChanges();
        }
        catch
        {
            // A logging failure must never break the actual operation.
        }
    }
}
