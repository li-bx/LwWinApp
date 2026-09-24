using LwWinApp.ViewModels;

namespace LwWinApp.Views.Settings;

/// <summary>系统设置。所有交互都走命令，没有事件处理。</summary>
public sealed partial class SettingsPage : PageBase
{
    public SettingsPage()
    {
        InitializeComponent();
        Vm = Require<SettingsViewModel>();
    }

    public SettingsViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;
}
