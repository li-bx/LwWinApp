using LwWinApp.Services;
using LwWinApp.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace LwWinApp.Views;

/// <summary>
/// 所有页面的基类。
///
/// 存在的意义是统一两件事：
/// 1. 把导航参数转发给 ViewModel；
/// 2. 提供 <see cref="App.GetService{T}"/> 这个取服务的入口。
///
/// 为什么页面不能构造函数注入：<c>Frame.Navigate(typeof(X))</c> 是 XAML 体系在
/// 创建页面实例，只认无参构造函数，不会走 DI 容器。所以 ViewModel 只能在这里
/// 从容器里主动取，或者由页面自己 new。
/// </summary>
public abstract class PageBase : Page, IViewHost
{
    protected PageBase()
    {
        // 页面在标签里应当填满可用空间，这里统一设一次，省得每个页面重复写。
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    /// <summary>本页面的 ViewModel。子类返回自己持有的实例。</summary>
    public abstract INavigationAware? ViewModel { get; }

    /// <summary>从容器取服务。容器还没建好时返回 null。</summary>
    protected static T? GetService<T>() where T : class => App.GetService<T>();

    /// <summary>
    /// 从容器取服务，取不到直接抛。
    ///
    /// 页面构造函数里用它取自己的 ViewModel：容器里没有就说明
    /// <c>App.ConfigureServices</c> 漏注册了，这种错误要在启动时就炸出来，
    /// 而不是等用户点了按钮才发现 ViewModel 是 null。
    /// </summary>
    protected static T Require<T>() where T : class
        => GetService<T>() ?? throw new InvalidOperationException(
            $"页面依赖的 {typeof(T).Name} 尚未注册，请检查 App.ConfigureServices。");

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (ViewModel is null) return;

        try
        {
            await ViewModel.OnNavigatedToAsync(e.Parameter);
        }
        catch (Exception ex)
        {
            // OnNavigatedTo 是 void，异常没法往外抛，只能就地处理，
            // 否则会成为未捕获异常直接把进程带崩。
            await ReportNavigationFailureAsync(ex);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel?.OnNavigatedFrom();
    }

    private async Task ReportNavigationFailureAsync(Exception ex)
    {
        try
        {
            GetService<ILoggerFactory>()?
                .CreateLogger(GetType())
                .LogError(ex, "页面 {Page} 加载失败", GetType().Name);

            var dialogs = GetService<IDialogService>();
            if (dialogs is not null)
            {
                await dialogs.ShowErrorAsync(
                    $"页面加载失败：{ex.Message}",
                    "出错了");
            }
        }
        catch
        {
            // 连错误提示都失败就只能咽下去，绝不能在这里再抛。
        }
    }
}
