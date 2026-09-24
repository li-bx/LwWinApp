using LwWinApp.Models.Entities;
using LwWinApp.ViewModels;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LwWinApp.Views.System;

/// <summary>用户管理页。</summary>
public sealed partial class UserManagePage : PageBase
{
    public UserManagePage()
    {
        InitializeComponent();
        Vm = Require<UserManageViewModel>();
    }

    public UserManageViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;

    private async void OnKeywordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        e.Handled = true;

        // 事件处理器是 void，异常必须在这里就地兜住，
        // 否则会变成未捕获异常把进程带走。命令里已经处理了业务异常，
        // 这里只防意外。
        try
        {
            await Vm.SearchCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (GetRow(sender) is not { } user) return;

        try
        {
            await Vm.EditCommand.ExecuteAsync(user);
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    private async void OnToggleStatusClick(object sender, RoutedEventArgs e)
    {
        if (GetRow(sender) is not { } user) return;

        try
        {
            await Vm.ToggleStatusCommand.ExecuteAsync(user);
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    private async void OnResetPasswordClick(object sender, RoutedEventArgs e)
    {
        if (GetRow(sender) is not { } user) return;

        try
        {
            await Vm.ResetPasswordCommand.ExecuteAsync(user);
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (GetRow(sender) is not { } user) return;

        try
        {
            await Vm.DeleteCommand.ExecuteAsync(user);
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    /// <summary>
    /// 行内按钮的点击目标。
    /// 按钮在 DataTemplate 里，DataContext 是那一行的实体，所以直接从 Tag 上取 ——
    /// 比用 ListView.SelectedItem 可靠：点删除按钮时用户未必先选中过那一行。
    /// </summary>
    private static SysUser? GetRow(object sender)
        => (sender as FrameworkElement)?.Tag as SysUser;

    private static void LogEventError(Exception ex)
        => App.GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(UserManagePage))
            .LogError(ex, "用户管理页的按钮事件处理失败");
}
