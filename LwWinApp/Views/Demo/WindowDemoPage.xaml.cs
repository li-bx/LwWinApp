using LwWinApp.ViewModels;
using LwWinApp.ViewModels.Demo;

namespace LwWinApp.Views.Demo;

/// <summary>弹窗窗口示例页。</summary>
public sealed partial class WindowDemoPage : PageBase
{
    public WindowDemoPage()
    {
        InitializeComponent();
        Vm = Require<WindowDemoViewModel>();
    }

    public WindowDemoViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;
}
