using LwWinApp.Core;
using LwWinApp.Utils;
using Microsoft.UI.Windowing;

namespace LwWinApp.Services;

/// <summary>应用主题（亮色 / 暗色 / 跟随系统）。</summary>
public interface IThemeService
{
    ThemeMode Current { get; }

    /// <summary>当前实际生效的是不是暗色（跟随系统时会随系统变化）。</summary>
    bool IsDark { get; }

    event EventHandler<ThemeMode>? ThemeChanged;

    /// <summary>启动时调用一次：读取上次保存的选择并应用。</summary>
    void Initialize();

    /// <summary>切换主题并持久化。</summary>
    void Apply(ThemeMode mode);

    /// <summary>在亮/暗之间切换（跟随系统时切到与当前相反的固定主题）。</summary>
    void Toggle();

    /// <summary>
    /// 把一个非主窗口（弹窗窗口）也设成当前主题。
    /// 弹窗窗口不在主窗口的视觉树里，RequestedTheme 不会自动继承，
    /// 不显式设一次的话，深色主题下会弹出一个雪白的窗口。
    /// </summary>
    void ApplyToWindow(Window window);
}

/// <summary>
/// 主题服务。
///
/// 两个关键点（WinUI 3 的坑）：
/// 1. <c>Application.Current.RequestedTheme</c> 只能在窗口创建前设置，运行时改它不生效。
///    运行时切换必须设 <c>Window.Content</c> 这个根元素的 RequestedTheme。
/// 2. 根元素的 RequestedTheme 必须设在 Window.Content 上，不能设在更下层的 Page 上——
///    Mica 背景订阅的是 Window.Content.ActualThemeChanged，设在下层它不会跟着变。
///    同理，创建后不能替换 Window.Content，否则 Mica 会失去订阅。
/// 3. 标题栏属于非客户区，不跟随 RequestedTheme，要用 AppWindow.TitleBar.PreferredTheme 单独设。
/// </summary>
public sealed class ThemeService : IThemeService
{
    /// <summary>本地设置里存主题用的键。App 启动时用它写入配置里的默认主题。</summary>
    public const string SettingKey = "Ui.Theme";

    private readonly IWindowContext _windowContext;
    private readonly ILogger<ThemeService> _logger;

    public ThemeService(IWindowContext windowContext, ILogger<ThemeService> logger)
    {
        _windowContext = windowContext;
        _logger = logger;
    }

    public ThemeMode Current { get; private set; } = ThemeMode.Default;

    public bool IsDark => _windowContext.RootElement?.ActualTheme == ElementTheme.Dark;

    public event EventHandler<ThemeMode>? ThemeChanged;

    public void Initialize()
    {
        Current = LocalSettings.GetEnum(SettingKey, ThemeMode.Default);
        ApplyToUi(Current);

        // 这里也要发一次：外壳的主题图标、设置页的下拉框都是靠这个事件同步的，
        // 不发的话启动时它们会停在默认值上，和实际主题对不上。
        ThemeChanged?.Invoke(this, Current);
    }

    public void Apply(ThemeMode mode)
    {
        Current = mode;
        LocalSettings.SetEnum(SettingKey, mode);
        ApplyToUi(mode);
        ThemeChanged?.Invoke(this, mode);
    }

    public void Toggle()
        => Apply(IsDark ? ThemeMode.Light : ThemeMode.Dark);

    private void ApplyToUi(ThemeMode mode)
    {
        var root = _windowContext.RootElement;
        if (root is null)
        {
            // 窗口还没建好。Initialize 是在窗口创建后调的，
            // 走到这里说明调用时机不对，记一条日志便于排查。
            _logger.LogWarning("窗口尚未就绪，主题暂时无法应用：{Mode}", mode);
            return;
        }

        root.RequestedTheme = ToElementTheme(mode);

        // 标题栏和内容区是两套机制，要分别设置。
        ApplyTitleBarTheme(_windowContext.MainWindow, mode);

        _logger.LogDebug("主题已切换为 {Mode}（实际 {Actual}）", mode, root.ActualTheme);
    }

    public void ApplyToWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // 弹窗窗口每次都是新建的，创建时按当前主题设一次就够了；
        // 想让它跟着后续的切换走，得由创建方在打开时重新调这个（见 IChildWindowService）。
        if (window.Content is FrameworkElement root)
            root.RequestedTheme = ToElementTheme(Current);

        ApplyTitleBarTheme(window, Current);
    }

    private static ElementTheme ToElementTheme(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => ElementTheme.Light,
        ThemeMode.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    /// <summary>
    /// 设置标题栏亮暗。
    /// 优先用 WinAppSDK 的 PreferredTheme（会跟随系统模式），
    /// 拿不到就退回 Win32 的 DWMWA_USE_IMMERSIVE_DARK_MODE。
    /// </summary>
    private void ApplyTitleBarTheme(Window? window, ThemeMode mode)
    {
        if (window is null) return;

        try
        {
            var titleBar = window.AppWindow?.TitleBar;
            if (titleBar is not null)
            {
                titleBar.PreferredTheme = mode switch
                {
                    ThemeMode.Light => TitleBarTheme.Light,
                    ThemeMode.Dark => TitleBarTheme.Dark,
                    // 跟随系统时交给系统按应用模式决定，避免和系统设置打架。
                    _ => TitleBarTheme.UseDefaultAppMode
                };
                return;
            }
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            // 某些系统版本上 PreferredTheme 不可用，走下面的兜底。
        }

        // 兜底只能给「亮 / 暗」两选一：跟随系统时按主窗口实际生效的主题算。
        WindowHelper.SetDarkTitleBar(window, mode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            _ => IsDark
        });
    }
}
