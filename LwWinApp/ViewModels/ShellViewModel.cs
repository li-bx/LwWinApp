using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Navigation;
using LwWinApp.Services;
using LwWinApp.Utils;

namespace LwWinApp.ViewModels;

/// <summary>
/// 外壳（主窗口）的 ViewModel：左上角标题、主题切换、当前页状态。
///
/// 左侧菜单本身不在这里构建 —— 那是纯静态结构，MainWindow 直接从
/// <see cref="PageCatalog"/> 生成即可，多绕一层反而更难跟。
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly IThemeService _theme;
    private readonly ITabNavigationService _tabs;
    private readonly IWindowService _window;
    private readonly IAppSettingsService _settings;

    public ShellViewModel(
        IThemeService theme,
        ITabNavigationService tabs,
        IWindowService window,
        IAppSettingsService settings)
    {
        _theme = theme;
        _tabs = tabs;
        _window = window;
        _settings = settings;

        ThemeOptions =
        [
            new SelectOption("跟随系统", ThemeMode.Default),
            new SelectOption("浅色", ThemeMode.Light),
            new SelectOption("深色", ThemeMode.Dark)
        ];

        _selectedTheme = ThemeOptions.FirstOrDefault(o => o.AsEnum<ThemeMode>() == _theme.Current)
                         ?? ThemeOptions[0];

        IsDark = _theme.IsDark;

        _tabs.ActivePageChanged += OnActivePageChanged;
        _theme.ThemeChanged += OnThemeChanged;
    }

    /// <summary>标题栏上显示的副标题，跟着当前标签走。</summary>
    [ObservableProperty]
    private string _subtitle = PageCatalog.Home.Title;

    /// <summary>当前是否深色。用来切换标题栏上那个太阳/月亮图标。</summary>
    [ObservableProperty]
    private bool _isDark;

    /// <summary>状态栏文字。</summary>
    [ObservableProperty]
    private string _statusText = $"就绪 · v{AppConstants.AppVersion}";

    [ObservableProperty]
    private SelectOption _selectedTheme;

    public IReadOnlyList<SelectOption> ThemeOptions { get; }

    /// <summary>主题图标：深色时显示太阳（点了会变亮），浅色时显示月亮。</summary>
    public string ThemeGlyph => IsDark ? "" : "";

    /// <summary>状态栏右侧：当前连的是哪个库。只显示地址，不含账号密码。</summary>
    public string DatabaseHint => $"数据库 {_settings.DatabaseTarget.Display}";

    partial void OnSelectedThemeChanged(SelectOption value)
    {
        if (value.AsEnum<ThemeMode>() is { } mode)
            _theme.Apply(mode);
    }

    partial void OnIsDarkChanged(bool value) => OnPropertyChanged(nameof(ThemeGlyph));

    /// <summary>标题栏上的主题按钮：在浅色和深色之间直接对调，不经过「跟随系统」。</summary>
    [RelayCommand]
    private void ToggleTheme() => _theme.Apply(IsDark ? ThemeMode.Light : ThemeMode.Dark);

    [RelayCommand]
    private Task OpenPageAsync(string? pageKey)
        => string.IsNullOrWhiteSpace(pageKey) ? Task.CompletedTask : _tabs.OpenAsync(pageKey);

    [RelayCommand]
    private Task OpenSettingsAsync() => _tabs.OpenAsync(PageCatalog.Settings);

    [RelayCommand]
    private Task OpenAboutAsync() => _tabs.OpenAsync(PageCatalog.About);

    // ---------------------------------------------------------------

    private void OnActivePageChanged(object? sender, string? pageKey)
    {
        // 事件的触发点在 UI 线程（标签选中变化），但服务也可能从后台线程调，
        // 统一挪到 UI 线程上更新属性。
        DispatcherHelper.RunAsync(() =>
        {
            var definition = PageCatalog.Get(pageKey);

            Subtitle = definition.Title;
            _window.SetSubtitle(definition.Title);

            StatusText = $"{definition.Title} · v{AppConstants.AppVersion}";
        });
    }

    private void OnThemeChanged(object? sender, ThemeMode mode)
    {
        DispatcherHelper.RunAsync(() =>
        {
            IsDark = _theme.IsDark;

            // 用户可能是在设置页改的主题，把标题栏下拉的选中项同步过来。
            var match = ThemeOptions.FirstOrDefault(o => o.AsEnum<ThemeMode>() == mode);
            if (match is not null && !ReferenceEquals(match, SelectedTheme))
                SelectedTheme = match;
        });
    }
}
