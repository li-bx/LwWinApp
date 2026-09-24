using LwWinApp.Data;
using LwWinApp.Data.Repositories;
using LwWinApp.Models.Dtos;

namespace LwWinApp.Services;

/// <summary>仪表盘的数据装配。</summary>
public interface IDashboardService
{
    Task<DashboardStats> GetStatsAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class DashboardService : IDashboardService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IDeptRepository _depts;
    private readonly ILogRepository _logs;
    private readonly IDatabaseFactory _db;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IUserRepository users,
        IRoleRepository roles,
        IDeptRepository depts,
        ILogRepository logs,
        IDatabaseFactory db,
        ILogger<DashboardService> logger)
    {
        _users = users;
        _roles = roles;
        _depts = depts;
        _logs = logs;
        _db = db;
        _logger = logger;
    }

    public async Task<DashboardStats> GetStatsAsync(CancellationToken ct = default)
    {
        var startedAt = DateTime.Now;

        // 这些查询互不依赖，全部并发发出。串行的话每个来回都要等一次网络，
        // 仪表盘一次刷新能差出好几倍时间。
        var totalUsers = _users.CountAsync(null, null, ct);
        var enabledUsers = _users.CountAsync("u.`status` = 1", null, ct);
        var totalRoles = _roles.CountAsync(null, null, ct);
        var totalDepts = _depts.CountAsync(null, null, ct);
        var logsToday = _logs.CountSinceAsync(DateTime.Today, ct);
        var usersByDept = _users.GetCountByDeptAsync(8, ct);
        var usersByRole = _users.GetCountByRoleAsync(8, ct);
        var recentLogs = _logs.GetRecentAsync(10, ct);
        var probe = _db.TestConnectionAsync(ct);

        await Task.WhenAll(totalUsers, enabledUsers, totalRoles, totalDepts,
                           logsToday, usersByDept, usersByRole, recentLogs, probe);

        var stats = new DashboardStats
        {
            TotalUsers = totalUsers.Result,
            EnabledUsers = enabledUsers.Result,
            TotalRoles = totalRoles.Result,
            TotalDepts = totalDepts.Result,
            LogsToday = logsToday.Result,
            UsersByDept = usersByDept.Result,
            UsersByRole = usersByRole.Result,
            RecentLogs = recentLogs.Result,
            ServerVersion = probe.Result.Ok ? probe.Result.Message : null,
            GeneratedAt = DateTime.Now
        };

        _logger.LogDebug("仪表盘数据已刷新，耗时 {Elapsed} ms",
            (int)(stats.GeneratedAt - startedAt).TotalMilliseconds);

        return stats;
    }
}
