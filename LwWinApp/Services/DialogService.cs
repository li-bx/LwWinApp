using LwWinApp.Core;
using LwWinApp.Utils;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;

namespace LwWinApp.Services;

/// <summary>带百分比和说明文字的进度回调载荷。</summary>
public sealed record ProgressReport(double Percent, string? Message = null);

/// <summary>
/// 弹出对话框服务。所有 ContentDialog 都必须经过它创建。
///
/// 为什么必须集中：同一个线程上**同时只能打开一个 ContentDialog**，
/// 第二个 ShowAsync 会直接抛 COMException。而且没有可靠的「当前是否有对话框」探测手段
/// （GetOpenPopupsForXamlRoot 在弹窗延迟创建期间看不到它）。
/// 所以这里用信号量把对话框串行化——并发调用会排队，而不是崩掉。
/// </summary>
public interface IDialogService
{
    Task ShowInfoAsync(string message, string? title = null);

    Task ShowSuccessAsync(string message, string? title = null);

    Task ShowWarningAsync(string message, string? title = null);

    Task ShowErrorAsync(string message, string? title = null);

    /// <summary>确认框。danger = true 时主按钮用警示色，用于删除这类不可逆操作。</summary>
    Task<bool> ConfirmAsync(
        string message,
        string? title = null,
        string primaryText = "确定",
        string closeText = "取消",
        bool danger = false);

    /// <summary>
    /// 文本输入框。
    /// validate 返回 null 表示校验通过，返回非空字符串则显示为错误并阻止提交。
    /// </summary>
    Task<string?> PromptAsync(
        string message,
        string? title = null,
        string? placeholder = null,
        string? initialValue = null,
        Func<string, string?>? validate = null);

    /// <summary>
    /// 带进度条的可取消对话框，托管一个异步任务。
    /// 任务抛出的业务异常会在对话框关闭后原样抛出，调用方按平常的方式处理。
    /// </summary>
    Task ShowProgressAsync(
        string title,
        string message,
        Func<IProgress<ProgressReport>, CancellationToken, Task> work,
        CancellationToken ct = default);

    /// <summary>展示一个自定义 ContentDialog（已自动设置 XamlRoot 并参与串行化）。</summary>
    Task<ContentDialogResult> ShowCustomAsync(ContentDialog dialog, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class DialogService : IDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly ILogger<DialogService> _logger;

    /// <summary>把并发的对话框请求排成队列，一次只放行一个。</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DialogService(IWindowContext windowContext, ILogger<DialogService> logger)
    {
        _windowContext = windowContext;
        _logger = logger;
    }

    public Task ShowInfoAsync(string message, string? title = null)
        => ShowMessageAsync(DialogKind.Info, title ?? "提示", message);

    public Task ShowSuccessAsync(string message, string? title = null)
        => ShowMessageAsync(DialogKind.Success, title ?? "操作成功", message);

    public Task ShowWarningAsync(string message, string? title = null)
        => ShowMessageAsync(DialogKind.Warning, title ?? "警告", message);

    public Task ShowErrorAsync(string message, string? title = null)
        => ShowMessageAsync(DialogKind.Error, title ?? "出错了", message);

    public async Task<bool> ConfirmAsync(
        string message,
        string? title = null,
        string primaryText = "确定",
        string closeText = "取消",
        bool danger = false)
    {
        var dialog = await PrepareAsync(new ContentDialog
        {
            Title = title ?? "请确认",
            Content = BuildContent(DialogKind.Question, message),
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText,

            // 危险操作刻意不给默认按钮：回车不该替用户按下「删除」。
            // 这个选择还有一个必须的副作用 —— 危险操作不允许把主按钮设成默认按钮。
            // 见下面 danger 分支的注释，两件事是绑在一起的。
            DefaultButton = danger ? ContentDialogButton.None : ContentDialogButton.Primary
        });

        if (danger)
        {
            // 删除这类操作把主按钮染成警示色，让人多看一秒。
            //
            // 这段只有在 DefaultButton != Primary 时才起效：ContentDialog 的模板里有一个
            // PrimaryAsDefaultButton 视觉状态，会把主按钮的 Style 直接设成 AccentButtonStyle。
            // VisualState 的 Setter 写的是控件上的局部值，优先级高于从这里传下去的
            // PrimaryButtonStyle，所以我们设的危险色会被整个盖掉 ——
            // 表现出来就是危险确认和普通确认长得一模一样。
            dialog.PrimaryButtonStyle = Application.Current.Resources.TryGetValue("DangerButtonStyle", out var style)
                ? style as Style
                : null;
        }

        return await ShowCoreAsync(dialog) == ContentDialogResult.Primary;
    }

    public async Task<string?> PromptAsync(
        string message,
        string? title = null,
        string? placeholder = null,
        string? initialValue = null,
        Func<string, string?>? validate = null)
    {
        var input = new TextBox
        {
            PlaceholderText = placeholder ?? string.Empty,
            Text = initialValue ?? string.Empty,
            AcceptsReturn = false
        };

        var errorText = new TextBlock
        {
            Foreground = GetBrush("SystemFillColorCriticalBrush"),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 6, 0, 0)
        };

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(input);
        panel.Children.Add(errorText);

        var dialog = await PrepareAsync(new ContentDialog
        {
            Title = title ?? "请输入",
            Content = panel,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        });

        if (validate is not null)
        {
            // 校验不通过时把对话框留住（args.Cancel = true），让用户就地改。
            dialog.PrimaryButtonClick += (_, args) =>
            {
                var error = validate(input.Text);
                if (error is null) return;

                errorText.Text = error;
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            };
        }

        var result = await ShowCoreAsync(dialog);
        return result == ContentDialogResult.Primary ? input.Text : null;
    }

    public async Task ShowProgressAsync(
        string title,
        string message,
        Func<IProgress<ProgressReport>, CancellationToken, Task> work,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var statusText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var percentText = new TextBlock
        {
            Text = "0%",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0),
            Opacity = 0.7
        };

        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0 };

        var panel = new StackPanel { MinWidth = 320, Spacing = 2 };
        panel.Children.Add(statusText);
        panel.Children.Add(bar);
        panel.Children.Add(percentText);

        var dialog = await PrepareAsync(new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "取消",
            // 不显示关闭按钮：任务跑完由代码关闭，避免留下一个半死不活的对话框。
            CloseButtonText = string.Empty,
            DefaultButton = ContentDialogButton.None
        });

        dialog.PrimaryButtonClick += (_, args) =>
        {
            // 拦下默认的关闭行为，先请求取消，等任务真正退出再关。
            args.Cancel = true;
            statusText.Text = "正在取消，请稍候…";
            dialog.IsPrimaryButtonEnabled = false;
            cts.Cancel();
        };

        var progress = new Progress<ProgressReport>(report =>
        {
            var value = Math.Clamp(report.Percent, 0, 100);
            bar.Value = value;
            percentText.Text = $"{value:0.#}%";

            if (!string.IsNullOrWhiteSpace(report.Message))
                statusText.Text = report.Message;
        });

        Exception? failure = null;

        // 用 Opened 事件驱动任务：任务一结束就 Hide()，
        // ShowAsync 随之返回。这样不需要把 IAsyncOperation 转成 Task。
        dialog.Opened += async (_, _) =>
        {
            try
            {
                await work(progress, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 用户主动取消，不算错误。
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                if (dialog.IsLoaded) dialog.Hide();
            }
        };

        await ShowCoreAsync(dialog);

        // 任务里的异常在对话框关掉之后再抛，调用方照常 try/catch。
        if (failure is not null) throw failure;
    }

    public async Task<ContentDialogResult> ShowCustomAsync(ContentDialog dialog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        // 必须走 PrepareAsync：调用方 new 出来的对话框不可能自己知道 XamlRoot，
        // 漏掉这一步 ShowAsync 会抛「This element does not have a XamlRoot」，
        // 而堆栈指向调用方的业务方法，很容易被误判成业务代码的问题。
        await PrepareAsync(dialog, ct);

        return await ShowCoreAsync(dialog, ct);
    }

    // ---------------------------------------------------------------

    private async Task ShowMessageAsync(DialogKind kind, string title, string message)
    {
        var dialog = await PrepareAsync(new ContentDialog
        {
            Title = title,
            Content = BuildContent(kind, message),
            CloseButtonText = "知道了",
            DefaultButton = ContentDialogButton.Close
        });

        await ShowCoreAsync(dialog);
    }

    private async Task<ContentDialog> PrepareAsync(ContentDialog dialog, CancellationToken ct = default)
    {
        await _windowContext.WaitUntilReadyAsync(ct);

        // XamlRoot 必须在 ShowAsync 之前设好，否则对话框根本不会显示。
        dialog.XamlRoot = _windowContext.XamlRoot
            ?? throw new InvalidOperationException("主窗口尚未就绪，无法显示对话框。");

        return dialog;
    }

    /// <summary>串行化执行：拿到信号量 → 显示 → 放行。</summary>
    private async Task<ContentDialogResult> ShowCoreAsync(ContentDialog dialog, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>带图标和文字的消息内容。</summary>
    private static StackPanel BuildContent(DialogKind kind, string message)
    {
        var (glyph, brushKey) = kind switch
        {
            DialogKind.Success => ("", "SystemFillColorSuccessBrush"),
            DialogKind.Warning => ("", "SystemFillColorCautionBrush"),
            DialogKind.Error => ("", "SystemFillColorCriticalBrush"),
            DialogKind.Question => ("", "SystemFillColorAttentionBrush"),
            _ => ("", "SystemFillColorAttentionBrush")
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            MinWidth = 300
        };

        panel.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = 26,
            Foreground = GetBrush(brushKey),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0)
        });

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
            VerticalAlignment = VerticalAlignment.Center,
            LineHeight = 22
        });

        return panel;
    }

    /// <summary>取主题画刷。取不到就返回 null，让控件用默认色而不是崩掉。</summary>
    private static Brush? GetBrush(string resourceKey)
        => Application.Current.Resources.TryGetValue(resourceKey, out var value)
            ? value as Brush
            : null;
}
