using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;
using LwWinApp.Services;

namespace LwWinApp.ViewModels;

/// <summary>仪表盘上的一根横向统计条。</summary>
public sealed class DashboardBar
{
    public DashboardBar(string name, long count, double percent)
    {
        Name = name;
        Count = count;
        Percent = percent;
    }

    public string Name { get; }

    public long Count { get; }

    /// <summary>占最大值的百分比，0~100。进度条用，所以按最大值归一而不是按总数。</summary>
    public double Percent { get; }
}

/// <summary>
/// 仪表盘。
///
/// 统计项很多，但只用一次查询批次取回（<see cref="IDashboardService"/> 内部并发发出），
/// 所以页面上没有「几个区块各自转圈」的观感——只有一个整体的忙碌态。
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardService _dashboard;

    public DashboardViewModel(IDashboardService dashboard)
    {
        _dashboard = dashboard;
        Title = "仪表盘";
    }

    [ObservableProperty]
    private long _totalUsers;

    [ObservableProperty]
    private long _enabledUsers;

    [ObservableProperty]
    private long _disabledUsers;

    [ObservableProperty]
    private long _totalRoles;

    [ObservableProperty]
    private long _totalDepts;

    [ObservableProperty]
    private long _logsToday;

    [ObservableProperty]
    private double _enabledRatio;

    [ObservableProperty]
    private string _serverVersion = "未连接";

    [ObservableProperty]
    private string _lastUpdated = "尚未刷新";

    [ObservableProperty]
    private IReadOnlyList<DashboardBar> _deptBars = [];

    [ObservableProperty]
    private IReadOnlyList<DashboardBar> _roleBars = [];

    [ObservableProperty]
    private IReadOnlyList<SysLog> _recentLogs = [];

    // 下面这几个都是给界面看的字符串。
    // x:Bind 不会把数值隐式转成 TextBlock.Text，所以在 VM 里先算好。

    public string EnabledText => $"启用 {EnabledUsers}";

    public string DisabledText => $"停用 {DisabledUsers}";

    public string EnabledRatioText => TotalUsers > 0
        ? $"{EnabledRatio:0.#}%"
        : "—";

    partial void OnEnabledUsersChanged(long value) => OnPropertyChanged(nameof(EnabledText));

    partial void OnDisabledUsersChanged(long value) => OnPropertyChanged(nameof(DisabledText));

    partial void OnEnabledRatioChanged(double value) => OnPropertyChanged(nameof(EnabledRatioText));

    partial void OnTotalUsersChanged(long value) => OnPropertyChanged(nameof(EnabledRatioText));

    public override Task OnNavigatedToAsync(object? parameter)
    {
        // 首次进入就必须加载；之后每次切回来也刷新一次，
        // 因为用户很可能刚在别的标签里改过数据。
        return RefreshAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => RunBusyAsync(async () =>
    {
        var stats = await _dashboard.GetStatsAsync();

        TotalUsers = stats.TotalUsers;
        EnabledUsers = stats.EnabledUsers;
        DisabledUsers = stats.DisabledUsers;
        TotalRoles = stats.TotalRoles;
        TotalDepts = stats.TotalDepts;
        LogsToday = stats.LogsToday;
        EnabledRatio = stats.EnabledRatio;
        ServerVersion = stats.ServerVersion ?? "连接不可用";
        LastUpdated = $"更新于 {stats.GeneratedAt:HH:mm:ss}";

        DeptBars = BuildBars(stats.UsersByDept);
        RoleBars = BuildBars(stats.UsersByRole);
        RecentLogs = stats.RecentLogs;
    }, "加载仪表盘失败");

    /// <summary>把「名字 + 数量」转成带百分比的条形数据，按最大值归一。</summary>
    private static IReadOnlyList<DashboardBar> BuildBars(IReadOnlyList<NameCount> source)
    {
        if (source.Count == 0) return [];

        var max = source.Max(item => item.Count);
        if (max <= 0) return [];

        return source
            .Select(item => new DashboardBar(item.Name, item.Count, item.Count * 100d / max))
            .ToList();
    }
}
