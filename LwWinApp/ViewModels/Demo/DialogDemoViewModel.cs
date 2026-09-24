using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Services;
using LwWinApp.Utils;

namespace LwWinApp.ViewModels.Demo;

/// <summary>
/// 弹出对话框示例。
///
/// 所有对话框都走 <see cref="IDialogService"/>，不直接 new ContentDialog。
/// 原因写在那个类的注释里，简单说就是「同一时刻只能开一个」，
/// 服务负责排队，页面自己弹很容易撞上。
/// </summary>
public sealed partial class DialogDemoViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notify;
    private readonly IFilePickerService _filePicker;

    public DialogDemoViewModel(
        IDialogService dialogs,
        INotificationService notify,
        IFilePickerService filePicker)
    {
        _dialogs = dialogs;
        _notify = notify;
        _filePicker = filePicker;

        Title = "弹出对话框";

        PriorityOptions =
        [
            new SelectOption("普通", 0),
            new SelectOption("较高", 1),
            new SelectOption("紧急", 2)
        ];
        _selectedPriority = PriorityOptions[0];
    }

    /// <summary>最近一次操作的结果，显示在页面底部。</summary>
    [ObservableProperty]
    private string _lastResult = "还没有执行任何操作。";

    // ---- 简单消息类 ----

    [RelayCommand]
    private async Task ShowInfoAsync()
    {
        await _dialogs.ShowInfoAsync("这是一条普通的提示信息，用于告知用户某件事已经完成。", "提示");
        LastResult = "已关闭：信息提示";
    }

    [RelayCommand]
    private async Task ShowSuccessAsync()
    {
        await _dialogs.ShowSuccessAsync("数据已成功保存到数据库。", "操作成功");
        LastResult = "已关闭：成功提示";
    }

    [RelayCommand]
    private async Task ShowWarningAsync()
    {
        await _dialogs.ShowWarningAsync(
            "当前操作会覆盖已有配置，请确认后再继续。",
            "操作有风险");
        LastResult = "已关闭：警告提示";
    }

    [RelayCommand]
    private async Task ShowErrorAsync()
    {
        await _dialogs.ShowErrorAsync(
            "无法连接到数据库服务器。\n请检查网络，或联系管理员确认连接串是否正确。",
            "连接失败");
        LastResult = "已关闭：错误提示";
    }

    // ---- 确认类 ----

    [RelayCommand]
    private async Task ShowConfirmAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "确定要执行这次演示操作吗？",
            "请确认",
            primaryText: "执行",
            closeText: "算了");

        LastResult = confirmed ? "确认框结果：用户点了「执行」" : "确认框结果：用户点了「算了」";
    }

    /// <summary>危险确认：主按钮染成警示色。删除、清空这类不可逆操作都该用它。</summary>
    [RelayCommand]
    private async Task ShowDangerConfirmAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "将删除该租户下的全部数据，包括 1.2 万条业务记录。\n\n此操作不可恢复，确定继续吗？",
            "删除租户",
            primaryText: "永久删除",
            danger: true);

        LastResult = confirmed ? "危险确认结果：已确认删除" : "危险确认结果：已取消";
    }

    // ---- 输入类 ----

    [RelayCommand]
    private async Task ShowInputAsync()
    {
        var name = await _dialogs.PromptAsync(
            "请输入新的部门名称。",
            "新增部门",
            placeholder: "例如：研发中心",
            initialValue: "研发中心",
            // 校验返回 null 表示通过；返回文字则显示在对话框里并阻止关闭。
            validate: value => string.IsNullOrWhiteSpace(value)
                ? "名称不能为空。"
                : value.Length > 20
                    ? "名称不能超过 20 个字。"
                    : null);

        LastResult = name is null ? "输入框结果：用户取消" : $"输入框结果：「{name}」";
    }

    [RelayCommand]
    private async Task ShowValidatedInputAsync()
    {
        var phone = await _dialogs.PromptAsync(
            "请输入接收告警短信的手机号。",
            "短信通知",
            placeholder: "11 位手机号",
            validate: value => RegexHelper.IsPhone(value)
                ? null
                : "手机号格式不正确，请重新输入。");

        LastResult = phone is null
            ? "校验输入结果：用户取消"
            : $"校验输入结果：{StringHelper.MaskPhone(phone)}";
    }

    // ---- 进度对话框 ----

    [RelayCommand]
    private async Task ShowProgressAsync()
    {
        try
        {
            await _dialogs.ShowProgressAsync(
                "正在导出数据",
                "准备中…",
                async (progress, ct) =>
                {
                    // 分 20 步走完，每步 120ms，总共约 2.4 秒 —— 刚好够看清进度条在动。
                    for (var step = 0; step <= 20; step++)
                    {
                        ct.ThrowIfCancellationRequested();

                        await Task.Delay(120, ct);

                        progress.Report(new ProgressReport(step * 5, $"正在处理第 {step} / 20 批…"));
                    }
                });

            LastResult = "进度对话框：任务正常完成";
            _notify.Success("导出完成。");
        }
        catch (OperationCanceledException)
        {
            // 用户点了取消。这不是错误，对话框已经自己关掉了。
            LastResult = "进度对话框：任务被用户取消";
            _notify.Warning("任务已取消。");
        }
    }

    /// <summary>演示「任务中等候取消」的收尾：取消后仍然做清理工作。</summary>
    [RelayCommand]
    private async Task ShowCancellableWorkAsync()
    {
        try
        {
            await _dialogs.ShowProgressAsync(
                "正在同步",
                "与服务器建立连接…",
                async (progress, ct) =>
                {
                    await Task.Delay(600, ct);
                    progress.Report(new ProgressReport(10, "正在拉取远端清单…"));

                    for (var step = 1; step <= 10; step++)
                    {
                        ct.ThrowIfCancellationRequested();

                        await Task.Delay(400, ct);
                        progress.Report(new ProgressReport(10 + step * 9, $"同步第 {step} 批记录…"));
                    }
                });

            LastResult = "同步任务：完成";
        }
        catch (OperationCanceledException)
        {
            LastResult = "同步任务：已取消（清理逻辑已执行）";
        }
    }

    // ---- 文件选择器 ----

    [RelayCommand]
    private async Task PickFileAsync()
    {
        var file = await _filePicker.PickFileAsync("选择", ".csv", ".txt", ".json");

        if (file is null)
        {
            LastResult = "文件选择：用户取消";
            return;
        }

        // 大小要从 BasicProperties 取，StorageFile 本身不带。
        var properties = await file.GetBasicPropertiesAsync();

        LastResult = $"文件选择：{file.Name}（{StringHelper.FormatSize((long)properties.Size)}）";
    }

    [RelayCommand]
    private async Task PickFolderAsync()
    {
        var folder = await _filePicker.PickFolderAsync("选择此文件夹");

        LastResult = folder is null ? "文件夹选择：用户取消" : $"文件夹选择：{folder.Path}";
    }

    [RelayCommand]
    private async Task SaveFileAsync()
    {
        var file = await _filePicker.PickSaveFileAsync($"导出_{DateTimeHelper.ForFileName()}", "保存", ".csv");

        LastResult = file is null ? "保存文件：用户取消" : $"保存文件：{file.Path}";
    }

    // ---- 用一个自定义下拉框串起「先选参数再弹框」的流程 ----

    public IReadOnlyList<SelectOption> PriorityOptions { get; }

    [ObservableProperty]
    private SelectOption? _selectedPriority;

    [ObservableProperty]
    private string _ticketTitle = "生产环境接口超时";

    [RelayCommand]
    private async Task SubmitTicketAsync()
    {
        var priority = SelectedPriority?.Text ?? "普通";

        var confirmed = await _dialogs.ConfirmAsync(
            $"标题：{TicketTitle}\n优先级：{priority}\n\n确认提交这张工单吗？",
            "提交工单",
            primaryText: "提交");

        if (!confirmed)
        {
            LastResult = "工单提交：已取消";
            return;
        }

        await _dialogs.ShowSuccessAsync(
            $"工单「{TicketTitle}」已提交，优先级 {priority}。",
            "提交成功");

        LastResult = $"工单提交：成功（{priority}）";
    }
}
