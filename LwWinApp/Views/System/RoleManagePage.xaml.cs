using LwWinApp.Models.Entities;
using LwWinApp.ViewModels;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LwWinApp.Views.System;

/// <summary>角色管理页。</summary>
public sealed partial class RoleManagePage : PageBase
{
    public RoleManagePage()
    {
        InitializeComponent();
        Vm = Require<RoleManageViewModel>();
    }

    public RoleManageViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;

    private async void OnKeywordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        e.Handled = true;

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
        if (GetRow(sender) is not { } role) return;

        try
        {
            await Vm.EditCommand.ExecuteAsync(role);
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    private async void OnToggleStatusClick(object sender, RoutedEventArgs e)
    {
        if (GetRow(sender) is not { } role) return;

        try
        {
            await Vm.ToggleStatusCommand.ExecuteAsync(role);
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (GetRow(sender) is not { } role) return;

        try
        {
            await Vm.DeleteCommand.ExecuteAsync(role);
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    private static SysRole? GetRow(object sender)
        => (sender as FrameworkElement)?.Tag as SysRole;

    private static void LogEventError(Exception ex)
        => App.GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(RoleManagePage))
            .LogError(ex, "角色管理页的按钮事件处理失败");
}
