using LwWinApp.ViewModels;

namespace LwWinApp.Views.Settings;

/// <summary>
/// 关于页。
/// 运行环境那几项直接复用 <see cref="SettingsViewModel"/>：同一个数据源没必要再包一层。
/// 页面自己按 transient 解析 VM，所以和「系统设置」页各持一份，互不影响。
/// </summary>
public sealed partial class AboutPage : PageBase
{
    public AboutPage()
    {
        InitializeComponent();
        Vm = Require<SettingsViewModel>();
    }

    public SettingsViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;

    public string VersionText => $"版本 {Vm.AppVersion}";

    /// <summary>技术栈清单。写成常量数组，改依赖时顺手改这里。</summary>
    public IReadOnlyList<string> TechStack { get; } =
    [
        "WinUI 3 + Windows App SDK —— 界面层，无 MSIX 打包，双击 exe 即可运行",
        "CommunityToolkit.Mvvm —— 源生成器实现 ObservableProperty / RelayCommand",
        "Microsoft.Extensions.Hosting —— 依赖注入、配置、日志一体化",
        "Dapper + MySqlConnector —— 手写 SQL 的轻量 ORM，泛型仓储驱动",
        "Mica 云母材质 + Segoe Fluent Icons —— 跟随系统的现代视觉",
        "自建 ListView 表格 / Pager / BusyOverlay —— 不依赖第三方控件包"
    ];
}
