using LwWinApp.Core;
using LwWinApp.Utils;
using LwWinApp.Views.Windows;
using Microsoft.UI.Windowing;

namespace LwWinApp.Services;

/// <summary>
/// 弹窗窗口服务 —— 负责把 <see cref="DialogWindow"/> 显示出来，以及模态/非模态这件事。
///
/// <para>
/// 为什么需要它：WinUI 3 没有「模态窗口」这个概念，<c>Window.ShowDialog</c> 不存在，
/// 窗口之间也没有父子关系可言。<see cref="IDialogService"/> 那套 ContentDialog 是另一回事
/// —— 它挂在主窗口的 XamlRoot 上，本质上还是主窗口里的一块浮层。
/// 真窗口要靠三件事拼出来：
/// </para>
/// <list type="number">
///   <item>用 Win32 的 <c>GWLP_HWNDPARENT</c> 认属主，让子窗口永远浮在属主之上；</item>
///   <item>打开前把属主窗口 <c>EnableWindow(false)</c>，它就不再接收任何输入 —— 这就是模态；</item>
///   <item>关掉之后再把属主恢复，并把焦点还回去。</item>
/// </list>
///
/// <para>
/// 一个 <see cref="DialogWindow"/> 实例只能打开一次：窗口被 Close 之后系统的那一份就销毁了，
/// 再 Activate 也不会回来。要再开一个就 new 一个新的。
/// </para>
/// </summary>
public interface IChildWindowService
{
    /// <summary>当前开着的非模态窗口数量。</summary>
    int ModelessWindowCount { get; }

    /// <summary>非模态窗口数量变化（开着的时候用户把某个窗口关掉了也算）。</summary>
    event EventHandler? ModelessWindowCountChanged;

    /// <summary>
    /// 以模态方式显示：属主窗口在它关闭之前完全不接收输入，关掉后 <c>await</c> 才返回。
    /// 返回值来自窗口自己（点了主按钮 / 取消按钮 / 标题栏的 X）。
    /// </summary>
    Task<DialogResult> ShowModalAsync(DialogWindow window);

    /// <summary>
    /// 以非模态方式显示：不阻塞属主窗口，用户可以一边开着它一边操作主界面，
    /// 同一时间也可以开好几个。不需要返回值 —— 没人会 await 它。
    /// </summary>
    void ShowModeless(DialogWindow window);

    /// <summary>关掉所有由本服务打开的非模态窗口。</summary>
    void CloseAllModeless();
}

/// <inheritdoc />
public sealed class ChildWindowService : IChildWindowService
{
    private readonly IWindowContext _windowContext;
    private readonly IThemeService _themeService;
    private readonly ILogger<ChildWindowService> _logger;

    /// <summary>还开着的非模态窗口。</summary>
    private readonly List<DialogWindow> _modeless = [];

    /// <summary>
    /// 还开着的模态窗口，按打开的先后顺序。
    /// 用列表而不是一个计数，是因为「谁被锁住了」要能按栈顶查出来 —— 见 <see cref="ResolveOwner"/>。
    /// </summary>
    private readonly List<DialogWindow> _modals = [];

    public ChildWindowService(
        IWindowContext windowContext,
        IThemeService themeService,
        ILogger<ChildWindowService> logger)
    {
        _windowContext = windowContext;
        _themeService = themeService;
        _logger = logger;
    }

    public int ModelessWindowCount => _modeless.Count;

    public event EventHandler? ModelessWindowCountChanged;

    public async Task<DialogResult> ShowModalAsync(DialogWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var owner = ResolveOwner();

        Prepare(window, owner, isModal: true);
        _modals.Add(window);

        try
        {
            // 模态的全部秘密就在这一行：被禁用的窗口收不到任何鼠标和键盘输入，
            // 用户点它、按 Tab 切过去都只会听到一声提示音。属主自己不需要知道这件事。
            WindowHelper.SetEnabled(owner, false);

            window.Activate();

            return await window.Completion;
        }
        finally
        {
            // 无论正常关闭、抛异常，还是属主连带被关掉，都要把属主解禁 ——
            // 漏一次，整个主窗口就永远点不动了，只能重启应用。
            _modals.Remove(window);
            RestoreOwner(owner);
        }
    }

    public void ShowModeless(DialogWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var owner = _windowContext.MainWindow;

        Prepare(window, owner, isModal: false);

        _modeless.Add(window);

        // 用户随时可能点标题栏的 X 把它关掉，不订阅就永远摘不掉，
        // 页面上的「已打开 N 个」会只增不减。
        window.Closed += (_, _) => RemoveModeless(window);

        window.Activate();
        RaiseModelessChanged();
    }

    public void CloseAllModeless()
    {
        if (_modeless.Count == 0) return;

        // 先整体摘掉再逐个关：否则每关一个都要改一次计数，
        // 界面上的「已打开 N 个」会从 5 一路跳到 0。
        var windows = _modeless.ToList();
        _modeless.Clear();

        foreach (var window in windows)
        {
            try
            {
                window.Close();
            }
            catch (Exception ex)
            {
                // 已经关掉的窗口再关一次会抛，这里只记日志。
                _logger.LogDebug(ex, "关闭非模态窗口失败");
            }
        }

        RaiseModelessChanged();
    }

    // ---------------------------------------------------------------

    /// <summary>
    /// 模态窗口的属主：正在开着的那个模态窗口（如果有），否则是主窗口。
    ///
    /// 这样嵌套打开时锁住的层级才是对的 —— 在最外层之上再开一个，被锁住的先是外层，
    /// 关掉内层时解禁的也只是外层，主窗口一直是锁着的。
    /// 用「主窗口 + 计数」的写法在这里就会出错：关掉内层会把主窗口一起解禁，模态就漏了。
    /// </summary>
    private Window ResolveOwner()
    {
        if (_modals.Count > 0) return _modals[^1];

        return _windowContext.MainWindow
            ?? throw new InvalidOperationException("主窗口尚未就绪，无法打开弹窗窗口。");
    }

    /// <summary>摆好一个窗口该有的样子。必须在 Activate 之前调用。</summary>
    private void Prepare(DialogWindow window, Window? owner, bool isModal)
    {
        // 主题：弹窗窗口不在主窗口的视觉树里，RequestedTheme 不会自动继承。
        _themeService.ApplyToWindow(window);

        ApplyIcon(window);

        WindowHelper.SetRoundedCorners(window);
        WindowHelper.SetMinSize(window, AppConstants.MinDialogWidth, AppConstants.MinDialogHeight);

        // 属主要在显示之前认好，否则任务栏上会先冒出一个独立按钮，再变成从属关系。
        WindowHelper.SetOwner(window, owner);

        if (window.AppWindow?.Presenter is OverlappedPresenter presenter)
        {
            // 模态窗口不是用来摆弄的：留着标题栏的关闭按钮就够了。
            // 非模态是长期开着的工具窗口，最小化、最大化、改大小都该有。
            presenter.IsMinimizable = !isModal;
            presenter.IsMaximizable = !isModal;
            presenter.IsResizable = !isModal;
        }

        WindowHelper.SetSize(window, (int)window.PreferredWidth, (int)window.PreferredHeight);
        WindowHelper.CenterOnOwner(window, owner);
    }

    /// <summary>
    /// 给窗口挂上应用图标。跟 WindowService 里那一段同理：无打包模式下
    /// <c>AppWindow.SetIcon</c> 会静默失败，只能自己发 WM_SETICON。
    /// 不设的话弹窗窗口在任务栏和 Alt+Tab 上是 WinUI 的默认图标，和主窗口对不上。
    /// </summary>
    private void ApplyIcon(DialogWindow window)
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

        if (!File.Exists(iconPath))
        {
            _logger.LogWarning("找不到图标文件，弹窗窗口将沿用系统默认图标：{Path}", iconPath);
            return;
        }

        WindowHelper.SetIcon(window, iconPath);
    }

    /// <summary>把属主窗口解禁，并把焦点还给它。</summary>
    private void RestoreOwner(Window owner)
    {
        try
        {
            WindowHelper.SetEnabled(owner, true);

            // 不把焦点拉回来，用户关掉弹窗之后焦点会落到别的程序上，
            // 接着敲键盘就打到别的窗口里去了。
            owner.Activate();
        }
        catch (Exception ex)
        {
            // 属主可能已经先一步被关掉了 —— 关主窗口会连带关掉所有属主是它的窗口，
            // 那时模态窗口的 await 也会返回，于是走到这里。这不算错误。
            _logger.LogDebug(ex, "恢复属主窗口失败，可能它已经关闭了");
        }
    }

    private void RemoveModeless(DialogWindow window)
    {
        if (!_modeless.Remove(window)) return;

        RaiseModelessChanged();
    }

    private void RaiseModelessChanged()
        => ModelessWindowCountChanged?.Invoke(this, EventArgs.Empty);
}
