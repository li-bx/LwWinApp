using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Services;

namespace LwWinApp.ViewModels.Demo;

/// <summary>一条历史通知，用来回看刚才发了什么。</summary>
public sealed class NotificationRecord
{
    public NotificationRecord(string kind, string message, string title)
    {
        Kind = kind;
        Message = message;
        Title = title;
        Time = DateTime.Now;
    }

    public string Kind { get; }

    public string Message { get; }

    public string Title { get; }

    public DateTime Time { get; }

    public string TimeText => Time.ToString("HH:mm:ss");
}

/// <summary>
/// 消息通知示例。
///
/// 把「页内消息条」「右下角轻提示」「引导气泡」三种反馈的适用场景摆在一起对比：
/// <list type="bullet">
/// <item><b>InfoBar</b>：需要用户看到并可处理的消息，留在页面上不消失（错误不自动关）</item>
/// <item><b>Toast</b>：操作成功这类「知道就行」的反馈，几秒后自己走掉</item>
/// <item><b>TeachingTip</b>：指向某个控件的引导，用户明确关掉才消失</item>
/// </list>
/// </summary>
public sealed partial class NotificationDemoViewModel : ViewModelBase
{
    private readonly INotificationService _notify;

    public NotificationDemoViewModel(INotificationService notify)
    {
        _notify = notify;
        Title = "消息通知";

        SeverityOptions =
        [
            new SelectOption("信息（Informational）", 0),
            new SelectOption("成功（Success）", 1),
            new SelectOption("警告（Warning）", 2),
            new SelectOption("错误（Error）", 3)
        ];
        _selectedSeverity = SeverityOptions[0];

        ToastOptions =
        [
            new SelectOption("信息", ToastKind.Info),
            new SelectOption("成功", ToastKind.Success),
            new SelectOption("警告", ToastKind.Warning),
            new SelectOption("错误", ToastKind.Error)
        ];
        _selectedToastKind = ToastOptions[1];

        DurationOptions =
        [
            new SelectOption("2 秒", 2d),
            new SelectOption("4 秒（默认）", 4d),
            new SelectOption("8 秒", 8d),
            new SelectOption("不自动关闭", 0d)
        ];
        _selectedDuration = DurationOptions[1];
    }

    // ---- InfoBar ----

    public IReadOnlyList<SelectOption> SeverityOptions { get; }

    [ObservableProperty]
    private SelectOption? _selectedSeverity;

    [ObservableProperty]
    private string _customMessage = "这是一条自定义的消息，用来试试不同严重级别的样子。";

    [ObservableProperty]
    private bool _withTitle = true;

    [RelayCommand]
    private void ShowInfoBar()
    {
        var severity = SelectedSeverity?.Value as int? ?? 0;
        var title = WithTitle ? $"{SelectedSeverity?.Text?.Split('（')[0]}提示" : null;
        var message = string.IsNullOrWhiteSpace(CustomMessage) ? "（没有填写内容）" : CustomMessage;
        var duration = SelectedDuration?.Value as double? ?? 4d;

        var kind = severity switch
        {
            1 => InfoBarSeverity.Success,
            2 => InfoBarSeverity.Warning,
            3 => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational
        };

        // 时间为 0 表示「不自动关闭」。
        _notify.Show(kind, message, title, duration > 0 ? TimeSpan.FromSeconds(duration) : Timeout.InfiniteTimeSpan);

        Add(new NotificationRecord(title ?? "（无标题）", message, KindText(kind)));
    }

    /// <summary>一次性把四种级别都发出来，直观对比配色。</summary>
    [RelayCommand]
    private void ShowAllSeverities()
    {
        _notify.Info("用户「张三」的资料已更新。", "信息");
        _notify.Success("导出完成，共 1,248 条记录。", "成功");
        _notify.Warning("磁盘剩余空间不足 10%，请及时清理。", "警告");
        _notify.Error("无法连接到 192.168.1.100:3306，请检查网络。", "错误");

        Add(new NotificationRecord("全部级别", "已发出 4 条不同级别的消息", "—"));
    }

    [RelayCommand]
    private void ClearInfoBars()
    {
        _notify.Clear();
        Add(new NotificationRecord("清空", "已清空页面上所有消息条", "—"));
    }

    // ---- Toast 轻提示 ----

    public IReadOnlyList<SelectOption> ToastOptions { get; }

    [ObservableProperty]
    private SelectOption? _selectedToastKind;

    [ObservableProperty]
    private string _toastMessage = "已保存";

    public IReadOnlyList<SelectOption> DurationOptions { get; }

    [ObservableProperty]
    private SelectOption? _selectedDuration;

    [RelayCommand]
    private void ShowToast()
    {
        var kind = SelectedToastKind.AsEnum<ToastKind>() ?? ToastKind.Info;
        var message = string.IsNullOrWhiteSpace(ToastMessage) ? "（没有填写内容）" : ToastMessage;

        _notify.Toast(message, kind);

        Add(new NotificationRecord("轻提示/" + KindText(kind), message, "右下角浮出"));
    }

    /// <summary>连续发几条，演示它们会依次堆叠而不是互相覆盖。</summary>
    [RelayCommand]
    private async Task ShowToastSequenceAsync()
    {
        for (var index = 1; index <= 4; index++)
        {
            _notify.Toast($"第 {index} 条：正在处理…", ToastKind.Info);
            await Task.Delay(350);
        }

        _notify.Toast("全部处理完成。", ToastKind.Success);

        Add(new NotificationRecord("轻提示序列", "连续发出 5 条轻提示", "右下角浮出"));
    }

    // ---- 历史记录 ----

    [ObservableProperty]
    private IReadOnlyList<NotificationRecord> _history = [];

    private void Add(NotificationRecord record)
    {
        // 只留最近 20 条：这是个演示页，没必要做无上限的增长。
        History = [record, .. History.Take(19)];
    }

    [RelayCommand]
    private void ClearHistory()
    {
        History = [];
        _notify.Clear();
    }

    private static string KindText(InfoBarSeverity severity) => severity switch
    {
        InfoBarSeverity.Success => "成功",
        InfoBarSeverity.Warning => "警告",
        InfoBarSeverity.Error => "错误",
        _ => "信息"
    };

    private static string KindText(ToastKind kind) => kind switch
    {
        ToastKind.Success => "成功",
        ToastKind.Warning => "警告",
        ToastKind.Error => "错误",
        _ => "信息"
    };
}
