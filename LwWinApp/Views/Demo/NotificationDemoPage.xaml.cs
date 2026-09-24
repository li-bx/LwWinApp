using LwWinApp.Services;
using LwWinApp.ViewModels;
using LwWinApp.ViewModels.Demo;

namespace LwWinApp.Views.Demo;

/// <summary>消息通知示例页。</summary>
public sealed partial class NotificationDemoPage : PageBase
{
    public NotificationDemoPage()
    {
        InitializeComponent();
        Vm = Require<NotificationDemoViewModel>();

        // 这里**不**注册 InfoBar 宿主：宿主由 Shell 全局提供一份。
        // 详见页面上那段说明。
    }

    public NotificationDemoViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;

    private void OnTipActionButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;

        GetService<INotificationService>()?.Info("引导里的「去看看」被点了 —— 真实场景里这里通常是打开某个页面。", "引导");
    }
}
