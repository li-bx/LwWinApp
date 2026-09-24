using LwWinApp.Core;
using LwWinApp.Services;

namespace LwWinApp.ViewModels.Demo;

/// <summary>分步任务里的一步。</summary>
public sealed partial class ProgressStep : ObservableObject
{
    public ProgressStep(string title)
    {
        Title = title;
    }

    public string Title { get; }

    [ObservableProperty]
    private StepState _state = StepState.Pending;

    [ObservableProperty]
    private string? _detail;

    public string StateGlyph => State switch
    {
        StepState.Running => "",   // 进行中
        StepState.Done => "",      // 完成
        StepState.Failed => "",    // 失败
        StepState.Skipped => "",   // 跳过
        _ => ""                    // 待执行
    };

    partial void OnStateChanged(StepState value) => OnPropertyChanged(nameof(StateGlyph));
}

/// <summary>分步任务的每一步所处的状态。</summary>
public enum StepState
{
    Pending,
    Running,
    Done,
    Failed,
    Skipped
}

/// <summary>
/// 进度与加载示例。
///
/// 三种进度语义在这里分清楚，别混用：
/// <list type="bullet">
/// <item>知道总量 → 确定值 <c>ProgressBar</c>（0~100 有具体值）</item>
/// <item>不知道总量 → 不确定值 <c>ProgressBar</c> 或 <c>ProgressRing</c></item>
/// <item>整页在等 → <c>BusyOverlay</c> 盖住内容，顺便挡住误操作</item>
/// </list>
/// </summary>
public sealed partial class ProgressDemoViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notify;

    /// <summary>页面级长任务的取消源。null 表示当前没有任务在跑。</summary>
    private CancellationTokenSource? _taskCts;

    public ProgressDemoViewModel(IDialogService dialogs, INotificationService notify)
    {
        _dialogs = dialogs;
        _notify = notify;

        Title = "进度与加载";

        Steps =
        [
            new ProgressStep("连接数据库"),
            new ProgressStep("校验数据完整性"),
            new ProgressStep("写入临时表"),
            new ProgressStep("重建索引"),
            new ProgressStep("清理过期记录")
        ];
    }

    // ---- 确定值进度条 ----

    [ObservableProperty]
    private double _progressValue = 35;

    public string ProgressText => $"{ProgressValue:0}%";

    partial void OnProgressValueChanged(double value) => OnPropertyChanged(nameof(ProgressText));

    // ---- 不确定值 ----

    [ObservableProperty]
    private bool _isIndeterminateActive;

    // ---- 页面级忙碌遮罩 ----

    [ObservableProperty]
    private string _busyText = "正在加载数据…";

    /// <summary>遮罩是否可见。由 RunBusyAsync 之外的独立任务控制，所以单独一个属性。</summary>
    [ObservableProperty]
    private bool _isOverlayActive;

    [ObservableProperty]
    private bool _canCancelTask;

    // ---- 分步进度 ----

    public IReadOnlyList<ProgressStep> Steps { get; }

    [ObservableProperty]
    private bool _isStepTaskRunning;

    [ObservableProperty]
    private int _completedSteps;

    /// <summary>已经跑完（成功或失败）的步数，用来画那条分步进度条。</summary>
    public double StepPercent => Steps.Count == 0 ? 0 : CompletedSteps * 100d / Steps.Count;

    public string StepSummary => IsStepTaskRunning
        ? $"进行中：{CompletedSteps} / {Steps.Count}"
        : CompletedSteps == Steps.Count
            ? $"全部完成：{CompletedSteps} / {Steps.Count}"
            : $"已停止：{CompletedSteps} / {Steps.Count}";

    partial void OnCompletedStepsChanged(int value)
    {
        OnPropertyChanged(nameof(StepPercent));
        OnPropertyChanged(nameof(StepSummary));
    }

    partial void OnIsStepTaskRunningChanged(bool value) => OnPropertyChanged(nameof(StepSummary));

    // ---------------------------------------------------------------
    // 命令
    // ---------------------------------------------------------------

    /// <summary>用按钮手动调进度条，演示「确定值」到底长什么样。</summary>
    [RelayCommand]
    private void Step()
    {
        ProgressValue = ProgressValue >= 100 ? 0 : Math.Min(100, ProgressValue + 10);
    }

    [RelayCommand]
    private void ResetProgress() => ProgressValue = 0;

    [RelayCommand]
    private void ToggleIndeterminate()
    {
        IsIndeterminateActive = !IsIndeterminateActive;

        _notify.Info(IsIndeterminateActive
            ? "不确定进度条已启动 —— 适合「不知道要多久」的等待。"
            : "不确定进度条已停止。");
    }

    /// <summary>页面级遮罩 + 长任务。2 秒后自动结束。</summary>
    [RelayCommand]
    private async Task StartOverlayTaskAsync()
    {
        if (_taskCts is not null) return;

        using var cts = new CancellationTokenSource();
        _taskCts = cts;

        IsOverlayActive = true;
        CanCancelTask = true;
        BusyText = "正在加载数据…";

        try
        {
            for (var step = 1; step <= 4; step++)
            {
                await Task.Delay(500, cts.Token);
                BusyText = $"正在加载第 {step} / 4 批…";
            }

            _notify.Success("数据加载完成。");
        }
        catch (OperationCanceledException)
        {
            _notify.Warning("加载已取消。");
        }
        finally
        {
            IsOverlayActive = false;
            CanCancelTask = false;
            _taskCts = null;
        }
    }

    [RelayCommand]
    private void CancelOverlayTask() => _taskCts?.Cancel();

    /// <summary>分步任务：每步有独立状态，失败的那步会停在那里。</summary>
    [RelayCommand]
    private async Task RunStepsAsync()
    {
        if (IsStepTaskRunning) return;

        IsStepTaskRunning = true;
        CompletedSteps = 0;

        foreach (var step in Steps)
        {
            step.State = StepState.Pending;
            step.Detail = null;
        }

        var failed = false;

        foreach (var step in Steps)
        {
            if (failed)
            {
                step.State = StepState.Skipped;
                step.Detail = "前一步失败，已跳过";
                continue;
            }

            step.State = StepState.Running;
            step.Detail = "执行中…";

            await Task.Delay(700);

            // 演示失败路径：第三步固定失败。真实场景里当然是按实际结果判断。
            if (step.Title == "重建索引")
            {
                step.State = StepState.Failed;
                step.Detail = "索引被占用，重建失败";
                failed = true;
                continue;
            }

            step.State = StepState.Done;
            step.Detail = "完成";
            CompletedSteps++;
        }

        IsStepTaskRunning = false;

        if (failed)
        {
            _notify.Error("任务在第 4 步失败：索引被占用。");
            return;
        }

        _notify.Success($"全部 {Steps.Count} 步执行完成。");
    }

    /// <summary>把分步任务搬进 ContentDialog —— 适合「必须等它做完」的操作。</summary>
    [RelayCommand]
    private async Task RunStepsInDialogAsync()
    {
        try
        {
            await _dialogs.ShowProgressAsync(
                "正在重建索引",
                "准备中…",
                async (progress, ct) =>
                {
                    for (var step = 0; step < Steps.Count; step++)
                    {
                        ct.ThrowIfCancellationRequested();

                        await Task.Delay(600, ct);
                        progress.Report(new ProgressReport(
                            (step + 1) * 100d / Steps.Count,
                            $"第 {step + 1} / {Steps.Count} 步：{Steps[step].Title}"));
                    }
                });

            _notify.Success("索引重建完成。");
        }
        catch (OperationCanceledException)
        {
            _notify.Warning("索引重建已取消。");
        }
    }
}
