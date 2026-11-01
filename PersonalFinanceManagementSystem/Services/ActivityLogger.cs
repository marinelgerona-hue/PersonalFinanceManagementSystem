using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Services;

public interface IActivityLogger
{
    /// <summary>Writes one activity-log row. Never throws — auditing must not
    /// be able to break the page or action it is recording.</summary>
    void Log(HttpContext context, int? userId, string actorName, string action, string detail);
}

/// <summary>Database-backed audit trail for the Admin Console's Activity Logs page.</summary>
public class ActivityLogger : IActivityLogger
{
    private readonly ApplicationDbContext _db;

    public ActivityLogger(ApplicationDbContext db) => _db = db;

    public void Log(HttpContext context, int? userId, string actorName, string action, string detail)
    {
        try
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            if (ip == "::1") ip = "127.0.0.1";

            _db.ActivityLogs.Add(new ActivityLog
            {
                UserId = userId,
                ActorName = actorName,
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
