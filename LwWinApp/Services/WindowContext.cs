using LwWinApp.Utils;

namespace LwWinApp.Services;

/// <summary>
/// 主窗口的访问点。
///
/// 需要它的原因：ContentDialog、文件选择器都要拿到 XamlRoot / 窗口句柄，
/// 而服务类是单例，没法通过构造参数拿到窗口。App 启动时调 <see cref="Attach"/> 注入一次，
/// 之后所有服务都从这里取。
/// </summary>
public interface IWindowContext
{
    /// <summary>主窗口。尚未附加时为 null。</summary>
    Window? MainWindow { get; }

    /// <summary>窗口内容的根元素。主题切换必须以它为目标。</summary>
    FrameworkElement? RootElement { get; }

    /// <summary>ContentDialog 必须显式指定的 XamlRoot。</summary>
    XamlRoot? XamlRoot { get; }

    /// <summary>窗口句柄，文件选择器需要。</summary>
    IntPtr Handle { get; }

    bool IsReady { get; }

    /// <summary>由 App 在窗口创建后调用。</summary>
    void Attach(Window window);

    /// <summary>等窗口就绪。窗口还没建好时调用方可以 await 它。</summary>
    Task WaitUntilReadyAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class WindowContext : IWindowContext
{
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Window? MainWindow { get; private set; }

    public FrameworkElement? RootElement => MainWindow?.Content as FrameworkElement;

    public XamlRoot? XamlRoot => RootElement?.XamlRoot;

    public IntPtr Handle => MainWindow is null ? IntPtr.Zero : WindowHelper.GetHandle(MainWindow);

    public bool IsReady => MainWindow is not null;

    public void Attach(Window window)
    {
        MainWindow = window;
        _ready.TrySetResult();
    }

    public Task WaitUntilReadyAsync(CancellationToken ct = default)
    {
        // 已经就绪时直接返回，不要白等一个已完成的任务。
        return IsReady ? Task.CompletedTask : _ready.Task.WaitAsync(ct);
    }
}
