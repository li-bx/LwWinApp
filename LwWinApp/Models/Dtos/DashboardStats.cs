using LwWinApp.Models.Entities;

namespace LwWinApp.Models.Dtos;

/// <summary>仪表盘一次刷新拿到的全部数据。一次查询批次里取齐，避免页面上一堆各自加载的区块。</summary>
public sealed class DashboardStats
{
    public long TotalUsers { get; set; }

    public long EnabledUsers { get; set; }

    public long DisabledUsers => Math.Max(0, TotalUsers - EnabledUsers);

    public long TotalRoles { get; set; }

    public long TotalDepts { get; set; }

    public long LogsToday { get; set; }

    /// <summary>启用率，0~100，给进度条用。</summary>
    public double EnabledRatio => TotalUsers <= 0 ? 0 : EnabledUsers * 100d / TotalUsers;

    /// <summary>各部门人数，已按人数倒序。</summary>
    public IReadOnlyList<NameCount> UsersByDept { get; set; } = [];

    /// <summary>各角色人数，已按人数倒序。</summary>
    public IReadOnlyList<NameCount> UsersByRole { get; set; } = [];

    /// <summary>最近的操作日志。</summary>
    public IReadOnlyList<SysLog> RecentLogs { get; set; } = [];

    /// <summary>数据库服务器版本，取不到时为空。</summary>
    public string? ServerVersion { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}
