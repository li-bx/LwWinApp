using LwWinApp.Core;
using LwWinApp.Utils;
using Microsoft.UI.Windowing;

namespace LwWinApp.Services;

/// <summary>窗口操作。</summary>
public interface IWindowService
{
    /// <summary>窗口是否已完成初始化。</summary>
    bool IsInitialized { get; }

    bool IsMaximized { get; }

    event EventHandler<bool>? MaximizedChanged;

    /// <summary>启动时调用：设标题、最小尺寸、初始尺寸并居中。</summary>
    void Initialize(Window window);

    /// <summary>
    /// 恢复上次退出时的窗口尺寸与位置。没有记录或恢复失败时返回 false（此时窗口保持居中）。
    /// </summary>
    bool TryRestoreWindowState();

    /// <summary>启用自定义标题栏。传入用作拖拽区域的元素。</summary>
    void EnableCustomTitleBar(UIElement dragRegion);

    void SetTitle(string title);

    void SetSubtitle(string? subtitle);

    void Minimize();

    void Maximize();

    void Restore();

    void ToggleMaximize();

    void Close();
}

/// <inheritdoc />
public sealed class WindowService : IWindowService
{
    private readonly ILogger<WindowService> _logger;

    private Window? _window;
    private OverlappedPresenter? _presenter;

    /// <summary>标题栏的拖拽区域。让位给系统按钮时改的是它的内边距。</summary>
    private UIElement? _dragRegion;

    /// <summary>标题栏内边距补枪的序号，用来作废过期的等待。</summary>
    private int _insetsToken;

    private string _title = AppConstants.AppTitle;
    private string? _subtitle;

    public WindowService(ILogger<WindowService> logger)
    {
        _logger = logger;
    }

    public bool IsInitialized => _window is not null;

    public bool IsMaximized => _presenter?.State == OverlappedPresenterState.Maximized;

    public event EventHandler<bool>? MaximizedChanged;

    public void Initialize(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
        _presenter = window.AppWindow?.Presenter as OverlappedPresenter;

        window.Title = _title;
        ApplyWindowIcon(window);

        // 最小尺寸靠子类化窗口过程实现，WinUI 没开放这个能力。
        WindowHelper.SetMinSize(window, AppConstants.MinWindowWidth, AppConstants.MinWindowHeight);

        // 圆角 + 初始尺寸。注意不要在这里调 SetDarkTitleBar，
        // 主题由 ThemeService 统一负责，两处都设会互相覆盖。
        WindowHelper.SetRoundedCorners(window);
        WindowHelper.SetSizeAndCenter(window, AppConstants.DefaultWindowWidth, AppConstants.DefaultWindowHeight);

        if (_presenter is not null)
        {
            _presenter.IsResizable = true;
            _presenter.IsMaximizable = true;
            _presenter.IsMinimizable = true;
        }

        if (window.AppWindow is { } appWindow)
            appWindow.Changed += OnAppWindowChanged;

        _logger.LogInformation("主窗口已初始化 {Width}×{Height}", AppConstants.DefaultWindowWidth, AppConstants.DefaultWindowHeight);
    }

    /// <summary>
    /// 给窗口挂上应用图标 —— 任务栏按钮和 Alt+Tab 用的是这一个。
    ///
    /// 光在 csproj 里设 <c>&lt;ApplicationIcon&gt;</c> 是不够的：那只把图标写进 exe 的资源段，
    /// 给资源管理器和快捷方式用；**正在运行的窗口在任务栏上读的是窗口自己的图标**，
    /// 不显式设一次，任务栏显示的就是 WinUI 框架那个默认图标。
    /// （实测：exe 里嵌的是蓝色图标，运行时往窗口上推一张红色的，任务栏立刻变红。）
    /// </summary>
    private void ApplyWindowIcon(Window window)
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

            if (!File.Exists(iconPath))
            {
                _logger.LogWarning("找不到图标文件，窗口将沿用系统默认图标：{Path}", iconPath);
                return;
            }

            WindowHelper.SetIcon(window, iconPath);
            _logger.LogInformation("窗口图标已设置：{Path}", iconPath);
        }
        catch (Exception ex)
        {
            // 图标设不上最多是难看，不该连窗口都起不来。
            _logger.LogWarning(ex, "设置窗口图标失败");
        }
    }

    public void EnableCustomTitleBar(UIElement dragRegion)
    {
        if (_window is null) return;

        try
        {
            _dragRegion = dragRegion;

            _window.ExtendsContentIntoTitleBar = true;
            _window.SetTitleBar(dragRegion);

            // 让系统按钮的高度和自定义标题栏一致（见 MainWindow.xaml 里那一行的 Height），
            // 否则按钮会挤在默认 32px 的位置，和居中的标题内容对不齐。
            try
            {
                if (_window.AppWindow?.TitleBar is { } titleBar)
                    titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
            {
                // 老系统上不支持，退回默认高度，不影响功能。
                _logger.LogDebug(ex, "设置标题栏高度失败，使用系统默认值");
            }

            RefreshCaptionButtonInsets();
        }
        catch (Exception ex)
        {
            // 自定义标题栏失败不该影响应用可用性，退回系统标题栏即可。
            _logger.LogWarning(ex, "启用自定义标题栏失败，继续使用系统标题栏");
            _window.ExtendsContentIntoTitleBar = false;
        }
    }

    public void SetTitle(string title)
    {
        _title = title;
        ApplyTitle();
    }

    public void SetSubtitle(string? subtitle)
    {
        _subtitle = subtitle;
        ApplyTitle();
    }

    public void Minimize() => _presenter?.Minimize();

    public void Maximize() => _presenter?.Maximize();

    public void Restore() => _presenter?.Restore();

    public void ToggleMaximize()
    {
        if (_presenter is null) return;

        // 最大化状态下必须用 Restore，再调 Maximize 是无效的。
        if (_presenter.State == OverlappedPresenterState.Maximized)
            _presenter.Restore();
        else
            _presenter.Maximize();
    }

    public void Close() => _window?.Close();

    // ---------------------------------------------------------------

    private void ApplyTitle()
    {
        if (_window is null) return;

        _window.Title = string.IsNullOrWhiteSpace(_subtitle)
            ? _title
            : $"{_title} - {_subtitle}";
    }

    /// <summary>
    /// 让自定义标题栏给系统的最小化/最大化/关闭按钮让出位置。
    /// 不自适应的话，放大到高 DPI 或系统语言变化时按钮会盖住我们的内容。
    ///
    /// 只给标题栏那一行让位，绝不动 <c>Window.Content</c>：
    /// 给根元素加右内边距会让整个界面右侧长期空出一条，正文也跟着缩进。
    ///
    /// 读不到可信宽度时不能就这么算了：还原窗口只会触发一次 <c>AppWindow.Changed</c>，
    /// 而那一枪打早了（见 <see cref="TryReadCaptionButtonInsets"/>），
    /// 不补一次的话标题栏右侧会一直歪着。
    /// </summary>
    private void RefreshCaptionButtonInsets()
    {
        // 作废上一次还在等待的补枪：状态又变了，旧值不该再写进来。
        var token = ++_insetsToken;

        // 最小化时窗口被系统缩成 160×28 的幽灵尺寸，标题栏压根不存在：
        // 既不用重新让位，读到的值也是垃圾。保留上一次的内边距就行，还原时会重算。
        // 这一条要在「读不到就补枪」之前判掉，否则每次最小化都要空等一秒再报超时。
        if (_presenter?.State == OverlappedPresenterState.Minimized) return;

        if (TryReadCaptionButtonInsets(out var insets))
        {
            ApplyCaptionButtonPadding(insets);
            return;
        }

        _ = WaitForCaptionButtonInsetsAsync(token);
    }

    /// <summary>
    /// 读系统按钮占掉的宽度。拿到不可信的值时返回 false，由调用方决定要不要重试。
    ///
    /// 两种「不可信」都实测过：
    /// <list type="number">
    ///   <item>最小化：窗口被系统缩成 160×28 的幽灵尺寸，标题栏根本不存在，
    ///         <c>RightInset</c> 会返回 −8 这样的负数；</item>
    ///   <item>还原动画跑完之前：读到的还是最小化那一套的残留值，约 100ms 后才更新，
    ///         而还原事件只在 0ms 处来一次。</item>
    /// </list>
    /// 负数一定是垃圾值。0 是真值 —— 全屏之类的状态下确实没有系统按钮，那时不该让位。
    /// </summary>
    private bool TryReadCaptionButtonInsets(out int insets)
    {
        insets = 0;

        if (_window?.AppWindow?.TitleBar is not { } titleBar) return false;
        if (_presenter?.State == OverlappedPresenterState.Minimized) return false;

        var value = titleBar.RightInset;
        if (value < 0) return false;

        insets = value;
        return true;
    }

    /// <summary>等标题栏内边距稳定下来再补一次，最多等 1 秒。</summary>
    private async Task WaitForCaptionButtonInsetsAsync(int token)
    {
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(50);

            if (token != _insetsToken) return;   // 期间又变了，交给新的一次收尾

            // 读到值的判断和写入都放回 UI 线程：Task.Delay 的续体落在哪个线程没有保证，
            // 而 AppWindow 和控件的属性都不能跨线程碰。
            var settled = await DispatcherHelper.RunAsync(() =>
            {
                if (!TryReadCaptionButtonInsets(out var insets)) return false;

                ApplyCaptionButtonPadding(insets);
                return true;
            });

            if (settled)
            {
                // 记录这一句，是为了给「还原后标题栏歪掉」这类问题留个现场：
                // 正常只会在最小化/最大化来回切的时候看到它。
                _logger.LogInformation("标题栏内边距等了 {Ms}ms 才读到可信值", (i + 1) * 50);
                return;
            }
        }

        _logger.LogWarning("等待标题栏内边距稳定超时，沿用上一次的值");
    }

    /// <summary>把让位宽度写进拖拽区域的内边距。</summary>
    private void ApplyCaptionButtonPadding(int insets)
    {
        if (_dragRegion is null) return;

        // 只有自带 Padding 的容器能这样让位；其它类型就放弃避让，
        // 总比整个布局错位强。
        switch (_dragRegion)
        {
            case Grid grid:
                grid.Padding = new Thickness(0, 0, insets, 0);
                break;

            case Border border:
                border.Padding = new Thickness(0, 0, insets, 0);
                break;

            default:
                _logger.LogDebug(
                    "拖拽区域类型 {Type} 不支持内边距，跳过标题栏避让",
                    _dragRegion.GetType().Name);
                break;
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPresenterChange || args.DidSizeChange)
        {
            // 最大化状态变化时右侧按钮宽度会变，需要重算。
            RefreshCaptionButtonInsets();
            MaximizedChanged?.Invoke(this, IsMaximized);
        }

        if (args.DidPositionChange || args.DidSizeChange)
        {
            PersistWindowState(sender);
        }
    }

    /// <summary>
    /// 记住窗口尺寸与位置，下次启动恢复。
    /// 最大化状态下不记尺寸，否则还原后会得到一个全屏大小的窗口。
    /// </summary>
    private void PersistWindowState(AppWindow appWindow)
    {
        if (appWindow.Presenter is not OverlappedPresenter presenter) return;

        LocalSettings.SetBool("Window.IsMaximized", presenter.State == OverlappedPresenterState.Maximized);

        if (presenter.State != OverlappedPresenterState.Restored) return;

        LocalSettings.SetInt("Window.Width", appWindow.Size.Width);
        LocalSettings.SetInt("Window.Height", appWindow.Size.Height);
        LocalSettings.SetInt("Window.X", appWindow.Position.X);
        LocalSettings.SetInt("Window.Y", appWindow.Position.Y);
    }

    /// <summary>恢复上次的窗口尺寸与位置。没有记录时返回 false，调用方走默认居中。</summary>
    public bool TryRestoreWindowState()
    {
        if (_window is null) return false;

        var width = LocalSettings.GetInt("Window.Width");
        var height = LocalSettings.GetInt("Window.Height");

        if (width <= 0 || height <= 0) return false;

        try
        {
            var appWindow = _window.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

            var x = LocalSettings.GetInt("Window.X", int.MinValue);
            var y = LocalSettings.GetInt("Window.Y", int.MinValue);

            if (x != int.MinValue && y != int.MinValue)
                appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            else
                WindowHelper.CenterOnScreen(_window);

            if (LocalSettings.GetBool("Window.IsMaximized"))
                _presenter?.Maximize();

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // 记下的坐标可能落在已拔掉的显示器上，恢复到位置失败就退回居中。
            _logger.LogWarning(ex, "恢复窗口位置失败，改用居中显示");
            WindowHelper.CenterOnScreen(_window);
            return false;
        }
    }
}
