using LwWinApp.Core;
using LwWinApp.Data.Repositories;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;
using LwWinApp.Services;
using LwWinApp.Utils;
using Microsoft.Extensions.Options;

namespace LwWinApp.ViewModels;

/// <summary>
/// 操作日志。只读列表，重点在「多条件筛选 + 大表格分页」。
/// 种子数据有两百多条，正好能把分页翻起来。
/// </summary>
public sealed partial class OperationLogViewModel : ViewModelBase
{
    private readonly ILogRepository _logs;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notify;
    private readonly AppOptions _options;

    public OperationLogViewModel(
        ILogRepository logs,
        IDialogService dialogs,
        INotificationService notify,
        IOptions<AppOptions> options)
    {
        _logs = logs;
        _dialogs = dialogs;
        _notify = notify;
        _options = options.Value;

        Title = "操作日志";
        _pageSize = _options.PageSize;

        LevelFilterOptions =
        [
            SelectOption.All("全部等级"),
            new SelectOption("调试", LogLevelKind.Debug),
            new SelectOption("信息", LogLevelKind.Info),
            new SelectOption("警告", LogLevelKind.Warn),
            new SelectOption("错误", LogLevelKind.Error),
            new SelectOption("致命", LogLevelKind.Fatal)
        ];

        _selectedLevel = LevelFilterOptions[0];
    }

    [ObservableProperty]
    private string? _keyword;

    [ObservableProperty]
    private SelectOption? _selectedLevel;

    [ObservableProperty]
    private SelectOption? _selectedModule;

    [ObservableProperty]
    private DateTimeOffset? _fromDate;

    [ObservableProperty]
    private DateTimeOffset? _toDate;

    [ObservableProperty]
    private IReadOnlyList<SysLog> _items = [];

    [ObservableProperty]
    private SysLog? _selectedLog;

    [ObservableProperty]
    private int _pageIndex = 1;

    [ObservableProperty]
    private int _pageSize;

    [ObservableProperty]
    private long _total;

    public IReadOnlyList<SelectOption> LevelFilterOptions { get; }

    [ObservableProperty]
    private IReadOnlyList<SelectOption> _moduleFilterOptions = [SelectOption.All("全部模块")];

    public IReadOnlyList<int> PageSizeOptions => _options.GetValidPageSizeOptions();

    public bool HasFilter
        => !string.IsNullOrWhiteSpace(Keyword)
        || SelectedLevel?.Value is not null
        || SelectedModule?.Value is not null
        || FromDate is not null
        || ToDate is not null;

    /// <summary>日期区间填反了会给用户一个看不懂的空列表，所以提前拦一道。</summary>
    public string? DateRangeError
        => FromDate is { } from && ToDate is { } to && from > to
            ? "起始日期不能晚于截止日期。"
            : null;

    /// <summary>InfoBar.IsOpen 是 bool，绑不了可空字符串，所以单独给一个标记。</summary>
    public bool HasDateRangeError => DateRangeError is not null;

    public string EmptyHint => HasFilter
        ? "没有符合当前条件的日志记录。"
        : "日志表里还没有数据。";

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        if (!IsInitialized)
        {
            await RunBusyAsync(async () =>
            {
                var modules = await _logs.GetModulesAsync();
                ModuleFilterOptions = [SelectOption.All("全部模块"), .. modules.Select(m => new SelectOption(m, m))];
                SelectedModule = ModuleFilterOptions[0];
                IsInitialized = true;
            }, "加载筛选项失败");
        }

        await LoadAsync();
    }

    partial void OnPageIndexChanged(int value) => _ = LoadAsync();

    partial void OnPageSizeChanged(int value)
    {
        if (PageIndex != 1)
            PageIndex = 1;
        else
            _ = LoadAsync();
    }

    partial void OnItemsChanged(IReadOnlyList<SysLog> value) => OnPropertyChanged(nameof(EmptyHint));

    partial void OnKeywordChanged(string? value) => RaiseFilterChanged();

    partial void OnSelectedLevelChanged(SelectOption? value) => RaiseFilterChanged();

    partial void OnSelectedModuleChanged(SelectOption? value) => RaiseFilterChanged();

    partial void OnFromDateChanged(DateTimeOffset? value) => RaiseFilterChanged();

    partial void OnToDateChanged(DateTimeOffset? value) => RaiseFilterChanged();

    private void RaiseFilterChanged()
    {
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(EmptyHint));
        OnPropertyChanged(nameof(DateRangeError));
        OnPropertyChanged(nameof(HasDateRangeError));
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (DateRangeError is { } error)
        {
            await _dialogs.ShowWarningAsync(error, "日期区间不正确");
            return;
        }

        if (PageIndex != 1)
        {
            PageIndex = 1;
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task ResetFilterAsync()
    {
        Keyword = null;
        SelectedLevel = LevelFilterOptions[0];
        SelectedModule = ModuleFilterOptions.Count > 0 ? ModuleFilterOptions[0] : null;
        FromDate = null;
        ToDate = null;

        if (PageIndex != 1)
        {
            PageIndex = 1;
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    /// <summary>把当前筛选结果导出成 CSV。日志行数可能很多，所以按上限截断并如实告知。</summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        var exported = await RunBusyAsync(async () =>
        {
            var filter = BuildFilter();
            filter.PageIndex = 1;
            filter.PageSize = QueryFilter.MaxPageSize;

            var result = await _logs.QueryPagedAsync(filter);

            var path = await CsvExporter.ExportAsync(
                result.Items,
                [
                    ("ID", l => l.Id),
                    ("时间", l => l.CreateTime),
                    ("等级", l => l.LevelText),
                    ("模块", l => l.Module),
                    ("内容", l => l.Message),
                    ("操作人", l => l.UserName),
                    ("IP", l => l.Ip)
                ],
                $"操作日志_{DateTimeHelper.ForFileName()}");

            return (Count: result.Items.Count, Total: result.Total, Path: path);
        }, "导出失败");

        if (!exported.HasValue) return;

        if (exported.Value.Path is null)
        {
            _notify.Warning("导出失败：无法写入导出目录。");
            return;
        }

        // 超过单次导出上限时要说清楚，不能让人以为导全了。
        var suffix = exported.Value.Total > exported.Value.Count
            ? $"（共 {exported.Value.Total} 条，已导出前 {exported.Value.Count} 条）"
            : string.Empty;

        _notify.Success($"导出完成{suffix}：{exported.Value.Path}");
        FileHelper.RevealInExplorer(exported.Value.Path);
    }

    /// <summary>清理 30 天前的日志。演示批量删除 + 危险操作确认。</summary>
    [RelayCommand]
    private async Task CleanupAsync()
    {
        var cutoff = DateTime.Today.AddDays(-30);

        var confirmed = await _dialogs.ConfirmAsync(
            $"将删除 {cutoff:yyyy-MM-dd} 之前的所有日志记录。\n\n此操作不可恢复，确定继续吗？",
            "清理历史日志",
            primaryText: "清理",
            danger: true);

        if (!confirmed) return;

        // DeleteWhereAsync 返回的是值类型，不能靠 RunBusyAsync<TResult> 判空，
        // 用 TryRunBusyAsync 拿「有没有成功」。
        var removed = 0;

        var done = await TryRunBusyAsync(
            async () => removed = await _logs.DeleteWhereAsync("`create_time` < @cutoff", new { cutoff }),
            "清理日志失败");

        if (!done) return;

        _notify.Success($"已清理 {removed} 条历史日志。");
        await LoadAsync();
    }

    // ---------------------------------------------------------------

    private async Task LoadAsync() => await RunBusyAsync(async () =>
    {
        var result = await _logs.QueryPagedAsync(BuildFilter());

        Items = result.Items;
        Total = result.Total;
    }, "加载操作日志失败");

    private LogQueryFilter BuildFilter() => new()
    {
        PageIndex = PageIndex,
        PageSize = PageSize,
        Keyword = Keyword,
        Level = SelectedLevel.AsEnum<LogLevelKind>(),
        Module = SelectedModule?.Value as string,
        From = FromDate,
        To = ToDate
    };
}
