using LwWinApp.ViewModels;
using LwWinApp.ViewModels.Demo;

namespace LwWinApp.Views.Demo;

/// <summary>弹出对话框示例页。</summary>
public sealed partial class DialogDemoPage : PageBase
{
    public DialogDemoPage()
    {
        InitializeComponent();
        Vm = Require<DialogDemoViewModel>();
    }

    public DialogDemoViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;
}
