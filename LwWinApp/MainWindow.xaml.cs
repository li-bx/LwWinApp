using LwWinApp.Core;
using LwWinApp.Navigation;
using LwWinApp.Services;
using LwWinApp.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LwWinApp;

/// <summary>
/// 主窗口，也就是整个应用的外壳。
///
/// 结构上就三件事：
/// <list type="number">
/// <item>标题栏（自绘，左侧标题 + 右侧主题/设置/关于）；</item>
/// <item>左侧一级导航（<see cref="NavigationView"/>，菜单按 <see cref="PageCatalog"/> 生成）；</item>
/// <item>顶部二级导航（<see cref="TabView"/>，每个标签内嵌独立 Frame，由 TabNavigationService 管）。</item>
/// </list>
///
/// 这里刻意不写业务：菜单是静态结构，标签是服务在管，页面状态在各自的 ViewModel 里。
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly ITabNavigationService? _tabs;
    private readonly INotificationService? _notify;

    /// <summary>页面 Key → 左侧菜单项。用来把标签切换同步成菜单高亮。</summary>
    private readonly Dictionary<string, NavigationViewItem> _navItems =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 正在由代码设置选中项。没有这个标记的话，
    /// 「打开标签 → 同步菜单选中 → 菜单又反过来开标签」会绕成死循环。
    /// </summary>
    private bool _syncingSelection;

    public MainWindow()
    {
        InitializeComponent();

        Vm = App.GetService<ShellViewModel>()
             ?? throw new InvalidOperationException(
                 "ShellViewModel 未注册，请检查 App.ConfigureServices。");

        _tabs = App.GetService<ITabNavigationService>();
        _notify = App.GetService<INotificationService>();

        BuildNavigationMenu();
        AttachTabNavigation();

        // 全局通知宿主只挂一次。NotificationService 是单例，
        // 页面如果各自挂宿主，后挂的会顶掉先挂的，消息就只往最后一个页面跑。
        _notify?.AttachInfoBarHost(InfoBarHost);
        _notify?.AttachToastHost(ToastHost);

        // 标题栏要等可视树加载完才能交给系统（那时才能拿到正确的按钮内边距）。
        RootGrid.Loaded += OnRootLoaded;
    }

    public ShellViewModel Vm { get; }

    public string AppTitle => AppConstants.AppTitle;

    // ---------------------------------------------------------------
    // 左侧一级导航
    // ---------------------------------------------------------------

    /// <summary>
    /// 按 <see cref="PageCatalog.Groups"/> 生成左侧菜单。
    ///
    /// 不在 XAML 里写死菜单，是为了让「菜单结构」和「页面登记表」永远是同一份数据：
    /// 加页面只改 PageCatalog 一处，菜单、标签、快捷键全都跟着变。
    /// </summary>
    private void BuildNavigationMenu()
    {
        ShellNav.MenuItems.Add(CreateNavItem(PageCatalog.Home));
        ShellNav.MenuItems.Add(new NavigationViewItemSeparator());

        foreach (var group in PageCatalog.Groups)
        {
            var groupItem = new NavigationViewItem
            {
                Content = group.Title,
                Icon = new FontIcon { Glyph = group.Glyph },
                Tag = group.Key,

                // 分组本身不是页面，点它只展开/收起，不产生选中。
                SelectsOnInvoked = false,
                IsExpanded = true
            };

            foreach (var page in group.Children)
                groupItem.MenuItems.Add(CreateNavItem(page));

            // 「设置」放到底部，和上面的功能区分开。
            if (string.Equals(group.Key, "settings", StringComparison.OrdinalIgnoreCase))
                ShellNav.FooterMenuItems.Add(groupItem);
            else
                ShellNav.MenuItems.Add(groupItem);
        }
    }

    private NavigationViewItem CreateNavItem(PageDefinition page)
    {
        var item = new NavigationViewItem
        {
            Content = page.Title,
            Tag = page.Key,
            Icon = new FontIcon { Glyph = page.Glyph }
        };

        ToolTipService.SetToolTip(item, page.Title);
        _navItems[page.Key] = item;

        return item;
    }

    private async void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_syncingSelection) return;
        if (args.SelectedItem is not NavigationViewItem { Tag: string key }) return;

        try
        {
            await OpenPageAsync(key);
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    // ---------------------------------------------------------------
    // 顶部二级导航
    // ---------------------------------------------------------------

    private void AttachTabNavigation()
    {
        if (_tabs is null) return;

        _tabs.Attach(ShellTabs);

        // 标签被打开或切换时，把左侧菜单的高亮跟过去，
        // 否则从「+」或快捷键开的页面在菜单上会看不出当前位置。
        _tabs.PageOpened += (_, key) => SyncNavSelection(key);
        _tabs.ActivePageChanged += (_, key) => SyncNavSelection(key);
    }

    private void SyncNavSelection(string? pageKey)
    {
        if (string.IsNullOrWhiteSpace(pageKey)) return;
        if (!_navItems.TryGetValue(pageKey, out var item)) return;
        if (ReferenceEquals(ShellNav.SelectedItem, item)) return;

        _syncingSelection = true;

        try
        {
            ShellNav.SelectedItem = item;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private Task OpenPageAsync(string pageKey)
        => _tabs?.OpenAsync(pageKey) ?? Task.CompletedTask;

    // ---------------------------------------------------------------
    // 快捷键
    // ---------------------------------------------------------------

    private void OnCloseTabAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (_tabs?.ActivePageKey is { } key)
            _tabs.Close(key);
    }

    private void OnNextTabAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        MoveTab(1);
    }

    private void OnPreviousTabAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        MoveTab(-1);
    }

    private void MoveTab(int delta)
    {
        var count = ShellTabs.TabItems.Count;
        if (count < 2) return;

        var index = ShellTabs.SelectedIndex;
        if (index < 0) index = 0;

        // 到头的绕回另一端，和浏览器一致。
        ShellTabs.SelectedIndex = ((index + delta) % count + count) % count;
    }

    private void OnGotoTabAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        var count = ShellTabs.TabItems.Count;
        if (count == 0) return;

        var number = (int)sender.Key - (int)VirtualKey.Number1 + 1;

        // 9 表示「最后一个」，和浏览器一致。
        var index = number == 9 ? count - 1 : number - 1;

        if (index >= 0 && index < count)
            ShellTabs.SelectedIndex = index;
    }

    // ---------------------------------------------------------------

    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded 会在某些布局变化时重复触发，这件事只做一次。
        RootGrid.Loaded -= OnRootLoaded;

        try
        {
            // 自定义标题栏要等可视树就绪后再启用，这时才拿得到系统按钮的实际宽度。
            App.GetService<IWindowService>()?.EnableCustomTitleBar(TitleBar);

            // 打开常驻首页。这一步同时会把菜单高亮同步过去。
            await OpenPageAsync(PageCatalog.Home.Key);
        }
        catch (Exception ex)
        {
            LogEventError(ex);
        }
    }

    private static void LogEventError(Exception ex)
        => App.GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(MainWindow))
            .LogError(ex, "外壳的事件处理失败");
}
