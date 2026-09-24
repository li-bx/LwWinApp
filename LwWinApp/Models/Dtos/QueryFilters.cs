using LwWinApp.Core;

namespace LwWinApp.Models.Dtos;

/// <summary>用户列表的筛选条件。</summary>
public sealed class UserQueryFilter : QueryFilter
{
    /// <summary>启用/停用。null 表示不限。</summary>
    public EnabledStatus? Status { get; set; }

    public long? DeptId { get; set; }

    public long? RoleId { get; set; }

    public Gender? Gender { get; set; }

    /// <summary>是否处于「有任何筛选条件」的状态，用于给「重置」按钮置灰。</summary>
    public bool HasAnyFilter
        => HasKeyword || Status is not null || DeptId is > 0 || RoleId is > 0 || Gender is not null;

    public void Clear()
    {
        Keyword = null;
        Status = null;
        DeptId = null;
        RoleId = null;
        Gender = null;
        Reset();
    }
}

/// <summary>角色列表的筛选条件。</summary>
public sealed class RoleQueryFilter : QueryFilter
{
    public EnabledStatus? Status { get; set; }

    public bool HasAnyFilter => HasKeyword || Status is not null;

    public void Clear()
    {
        Keyword = null;
        Status = null;
        Reset();
    }
}

/// <summary>操作日志的筛选条件。</summary>
public sealed class LogQueryFilter : QueryFilter
{
    public LogLevelKind? Level { get; set; }

    public string? Module { get; set; }

    /// <summary>起始时间（含）。</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>截止时间（含当天，查询时会换算成次日零点前）。</summary>
    public DateTimeOffset? To { get; set; }

    public bool HasAnyFilter
        => HasKeyword || Level is not null || !string.IsNullOrWhiteSpace(Module) || From is not null || To is not null;

    public void Clear()
    {
        Keyword = null;
        Level = null;
        Module = null;
        From = null;
        To = null;
        Reset();
    }
}
