using Microsoft.UI.Dispatching;

namespace LwWinApp.Utils;

/// <summary>
/// UI 线程调度。后台线程要更新界面时必须经过它，
/// 直接跨线程改控件属性在 WinUI 里会抛异常。
/// </summary>
public static class DispatcherHelper
{
    private static DispatcherQueue? _dispatcher;

    /// <summary>由 App 启动时注入主窗口的 DispatcherQueue。</summary>
    public static void Initialize(DispatcherQueue dispatcher) => _dispatcher = dispatcher;

    /// <summary>当前是否在 UI 线程上。</summary>
    public static bool IsOnUIThread => _dispatcher?.HasThreadAccess ?? true;

    /// <summary>切到 UI 线程执行（已经在 UI 线程时直接执行）。</summary>
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = _dispatcher;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        dispatcher.TryEnqueue(() => action());
    }

    /// <summary>切到 UI 线程执行并等待完成。</summary>
    public static Task RunAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = _dispatcher;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(new InvalidOperationException("无法将操作投递到 UI 线程，调度队列可能已关闭。"));
        }

        return tcs.Task;
    }

    /// <summary>切到 UI 线程执行并返回结果。</summary>
    public static Task<T> RunAsync<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        var dispatcher = _dispatcher;
        if (dispatcher is null || dispatcher.HasThreadAccess)
            return Task.FromResult(func());

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(new InvalidOperationException("无法将操作投递到 UI 线程，调度队列可能已关闭。"));
        }

        return tcs.Task;
    }

    /// <summary>延时后再切回 UI 线程执行，用于「稍等一会再刷新」这类场景。</summary>
    public static async Task DelayRunAsync(TimeSpan delay, Action action)
    {
        await Task.Delay(delay);
        await RunAsync(action);
    }
}
