using LwWinApp.Services;
using LwWinApp.ViewModels;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LwWinApp.Views.System;

/// <summary>操作日志页。</summary>
public sealed partial class OperationLogPage : PageBase
{
    public OperationLogPage()
    {
        InitializeComponent();
        Vm = Require<OperationLogViewModel>();
    }

    public OperationLogViewModel Vm { get; }

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

    /// <summary>
    /// 双击看完整内容。
    /// 日志内容经常很长，表格里那一列再宽也放不下，双击弹出来看是最省事的做法。
    /// </summary>
    private async void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (Vm.SelectedLog is not { } log) return;

        e.Handled = true;

        try
        {
            var dialogs = GetService<IDialogService>();
            if (dialogs is null) return;

            await dialogs.ShowInfoAsync(
                $"{log.Message}\n\n————\n模块：{log.Module}\n等级：{log.LevelText}\n" +
                $"操作人：{log.UserNameDisplay}\nIP：{log.IpDisplay}\n时间：{log.CreateTimeText}",
                "日志详情");
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    private static void LogEventError(Exception ex)
        => App.GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(OperationLogPage))
            .LogError(ex, "操作日志页的事件处理失败");
}
