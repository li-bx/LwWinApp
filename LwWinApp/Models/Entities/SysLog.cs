using LwWinApp.Core;
using LwWinApp.Data;

namespace LwWinApp.Models.Entities;

/// <summary>操作日志表。数据量给得比较多，用来演示大表格分页。</summary>
[Table("sys_log")]
public sealed class SysLog
{
    [Key]
    public long Id { get; set; }

    public LogLevelKind Level { get; set; } = LogLevelKind.Info;

    /// <summary>所属模块，如「用户管理」。</summary>
    public string Module { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? Ip { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.Now;

    [Ignore]
    public string LevelText => Level switch
    {
        LogLevelKind.Debug => "调试",
        LogLevelKind.Info => "信息",
        LogLevelKind.Warn => "警告",
        LogLevelKind.Error => "错误",
        LogLevelKind.Fatal => "致命",
        _ => "信息"
    };

    // x:Bind 不会把 DateTime 隐式转成字符串，表格里要显示就先转好。

    [Ignore]
    public string CreateTimeText => CreateTime.ToString("yyyy-MM-dd HH:mm:ss");

    [Ignore]
    public string UserNameDisplay => string.IsNullOrWhiteSpace(UserName) ? "—" : UserName;

    [Ignore]
    public string IpDisplay => string.IsNullOrWhiteSpace(Ip) ? "—" : Ip;

    /// <summary>错误和致命级别用红色标出来，扫一眼就能找到出问题的行。</summary>
    [Ignore]
    public bool IsProblem => Level is LogLevelKind.Error or LogLevelKind.Fatal;

    /// <summary>警告用橙色。两个属性分开写是因为 x:Bind 只认 bool → Visibility。</summary>
    [Ignore]
    public bool IsWarning => Level == LogLevelKind.Warn;

    /// <summary>调试和信息级别用中性配色。三个属性互斥，界面上对应三种徽标。</summary>
    [Ignore]
    public bool IsNormal => Level is LogLevelKind.Debug or LogLevelKind.Info;
}
