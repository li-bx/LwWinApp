using LwWinApp.Core;
using LwWinApp.Services;

namespace LwWinApp.ViewModels;

/// <summary>
/// 页面生命周期回调。标签导航服务会在合适的时机调用这些方法，
/// 让 ViewModel 能在页面出现时加载数据、在标签关闭时释放资源。
/// </summary>
public interface INavigationAware
{
    /// <summary>页面进入时调用。parameter 来自 Frame.Navigate 的第二个参数。</summary>
    Task OnNavigatedToAsync(object? parameter);

    /// <summary>页面离开（同一标签内导航到别的页面）时调用。</summary>
    void OnNavigatedFrom();

    /// <summary>标签被关闭时调用。适合在这里退订事件、停掉定时器。</summary>
    void OnTabClosed();
}

/// <summary>
/// 页面暴露自己 ViewModel 的接口。
/// 标签导航服务通过它拿到 ViewModel，转发生命周期回调。
/// </summary>
public interface IViewHost
{
    INavigationAware? ViewModel { get; }
}

/// <summary>
/// <see cref="ViewModelBase.RunBusyAsync{TResult}"/> 的返回值：有没有成功，以及成功时的结果。
///
/// 刻意不用 <c>TResult?</c>：泛型上的 <c>T?</c> 对值类型不会变成 <c>Nullable&lt;T&gt;</c>，
/// 于是「操作失败了，没有结果」和「结果是默认值」就分不出来，
/// 传元组这类值类型进去时编译期还会直接报「无法将 null 转换为 (…)」。
/// 用一个显式的标记位，两种情况一眼可分。
/// </summary>
public readonly record struct BusyResult<T>(bool HasValue, T Value)
{
    public static BusyResult<T> None => new(false, default!);
}

/// <summary>
/// 所有 ViewModel 的基类。
///
/// 只放跨页面通用的东西：忙碌标记、标题、统一的异常处理入口。
/// 具体页面的状态用 <c>[ObservableProperty]</c> 在各自的 ViewModel 里声明，
/// 源生成器会自动补上 INotifyPropertyChanged。
/// </summary>
public abstract partial class ViewModelBase : ObservableObject, INavigationAware
{
    /// <summary>正在加载数据。绑定到 BusyOverlay 或 ProgressRing。</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>标签标题。留空则用 PageCatalog 里登记的名字。</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>当前页面的错误提示，绑定到 InfoBar。</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>页面是否已经完成了首次加载。</summary>
    public bool IsInitialized { get; protected set; }

    public virtual Task OnNavigatedToAsync(object? parameter) => Task.CompletedTask;

    public virtual void OnNavigatedFrom() { }

    public virtual void OnTabClosed() { }

    /// <summary>
    /// 统一的异步操作包装：开忙碌标记、跑操作、把异常翻译成用户看得懂的话。
    ///
    /// <see cref="BusinessException"/> 是业务校验失败，消息本身就是给用户看的，直接展示；
    /// 其他异常属于没预料到的问题，只展示一句通用提示，细节写进日志。
    /// </summary>
    protected async Task RunBusyAsync(Func<Task> action, string? errorTitle = null)
    {
        if (IsBusy) return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await action();
        }
        catch (BusinessException ex)
        {
            ErrorMessage = ex.Message;
            await ShowErrorAsync(ex.Message, errorTitle ?? "操作未完成");
        }
        catch (OperationCanceledException)
        {
            // 用户主动取消，不是错误，静默即可。
        }
        catch (Exception ex)
        {
            ErrorMessage = "操作失败，请稍后重试。";
            LogUnexpected(ex);
            await ShowErrorAsync($"{ErrorMessage}\n\n（{ex.Message}）", errorTitle ?? "出错了");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>同上，带返回值。</summary>
    protected async Task<BusyResult<TResult>> RunBusyAsync<TResult>(Func<Task<TResult>> action, string? errorTitle = null)
    {
        var result = BusyResult<TResult>.None;

        await RunBusyAsync(async () =>
        {
            result = new BusyResult<TResult>(true, await action());
        }, errorTitle);

        return result;
    }

    /// <summary>
    /// 同 <see cref="RunBusyAsync(Func{Task}, string?)"/>，但把「有没有成功」告诉调用方。
    /// 成功之后还要接着做事（刷新列表、弹成功提示）的场合用这个：
    /// 失败时对话框已经弹过了，调用方直接 return 即可，不会出现「失败了还提示成功」。
    /// </summary>
    protected async Task<bool> TryRunBusyAsync(Func<Task> action, string? errorTitle = null)
    {
        var succeeded = false;

        await RunBusyAsync(async () =>
        {
            await action();
            succeeded = true;
        }, errorTitle);

        return succeeded;
    }

    /// <summary>展示一句错误对话框。页面可能已经被关掉，所以要能容忍失败。</summary>
    protected async Task ShowErrorAsync(string message, string? title = null)
    {
        try
        {
            var dialogs = App.Services?.GetService<IDialogService>();
            if (dialogs is not null)
                await dialogs.ShowErrorAsync(message, title);
        }
        catch (Exception ex)
        {
            // 对话框弹不出来（窗口已关闭等）不能反过来把页面搞崩。
            LogUnexpected(ex);
        }
    }

    private void LogUnexpected(Exception ex)
    {
        try
        {
            App.Services?.GetService<ILoggerFactory>()
                ?.CreateLogger(GetType())
                .LogError(ex, "ViewModel {ViewModel} 处理失败", GetType().Name);
        }
        catch
        {
            // 日志系统还没起来时忽略。
        }
    }
}
