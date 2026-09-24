using LwWinApp.ViewModels;

namespace LwWinApp.Views;

/// <summary>仪表盘。纯展示页：没有事件处理，全部交互都走命令绑定。</summary>
public sealed partial class DashboardPage : PageBase
{
    public DashboardPage()
    {
        InitializeComponent();
        Vm = Require<DashboardViewModel>();
    }

    public DashboardViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;
}
