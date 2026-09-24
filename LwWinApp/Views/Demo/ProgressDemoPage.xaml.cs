using LwWinApp.ViewModels;
using LwWinApp.ViewModels.Demo;

namespace LwWinApp.Views.Demo;

/// <summary>进度与加载示例页。</summary>
public sealed partial class ProgressDemoPage : PageBase
{
    public ProgressDemoPage()
    {
        InitializeComponent();
        Vm = Require<ProgressDemoViewModel>();
    }

    public ProgressDemoViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;
}
