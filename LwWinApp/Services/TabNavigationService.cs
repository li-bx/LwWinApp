using LwWinApp.Navigation;
using LwWinApp.Utils;
using LwWinApp.ViewModels;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace LwWinApp.Services;

/// <summary>
/// 二级导航：顶部标签页管理。左侧一级菜单选定后，由它负责把对应页面开成标签。
/// </summary>
public interface ITabNavigationService
{
    /// <summary>当前激活的页面 Key，全部关闭后为 null。</summary>
    string? ActivePageKey { get; }

    IReadOnlyList<string> OpenPageKeys { get; }

    /// <summary>激活的标签发生变化。</summary>
    event EventHandler<string?>? ActivePageChanged;

    /// <summary>标签被打开时触发，左侧菜单据此同步高亮。</summary>
    event EventHandler<string>? PageOpened;

    /// <summary>由 MainWindow 在初始化时把 TabView 交给它。</summary>
    void Attach(TabView tabView);

    /// <summary>打开页面。已经开过就直接激活，不重复创建。</summary>
    Task OpenAsync(string pageKey, object? parameter = null);

    bool Close(string pageKey);

    /// <summary>关闭除指定页面外的所有标签。</summary>
    void CloseOthers(string pageKey);

    /// <summary>关闭所有可关闭的标签。</summary>
    void CloseAll();

    bool IsOpen(string pageKey);
}

/// <summary>
/// 标签导航实现。
///
/// 每个标签持有**自己的 Frame**，这样标签之间互不干扰，
/// 切回来时页面状态（滚动位置、已填的筛选条件）都还在。
///
/// 刻意用 <c>TabItems.Add</c> 手工添加，而不是 <c>TabItemsSource</c> + DataTemplate：
/// TabView 会复用回收 TabViewItem 容器，而放在 DataTemplate 里的 Frame 属于容器状态，
/// 会被重建或复用，表现为「切换标签后显示的是别的页面」。
/// </summary>
public sealed class TabNavigationService : ITabNavigationService
{
    private readonly ILogger<TabNavigationService> _logger;

    /// <summary>Key → 标签条目。关闭时从这里移除，Frame 和页面实例才能被回收。</summary>
    private readonly Dictionary<string, TabEntry> _tabs = new(StringComparer.OrdinalIgnoreCase);

    private TabView? _tabView;

    public TabNavigationService(ILogger<TabNavigationService> logger)
    {
        _logger = logger;
    }

    public string? ActivePageKey { get; private set; }

    public IReadOnlyList<string> OpenPageKeys => _tabs.Keys.ToList();

    public event EventHandler<string?>? ActivePageChanged;
    public event EventHandler<string>? PageOpened;

    public void Attach(TabView tabView)
    {
        ArgumentNullException.ThrowIfNull(tabView);

        // 支持重复 Attach（比如窗口重建），先把旧的退订掉避免重复响应。
        if (_tabView is not null)
        {
            _tabView.TabCloseRequested -= OnTabCloseRequested;
            _tabView.SelectionChanged -= OnSelectionChanged;
            _tabView.AddTabButtonClick -= OnAddTabButtonClick;
        }

        _tabView = tabView;
        _tabView.TabCloseRequested += OnTabCloseRequested;
        _tabView.SelectionChanged += OnSelectionChanged;
        _tabView.AddTabButtonClick += OnAddTabButtonClick;
    }

    public async Task OpenAsync(string pageKey, object? parameter = null)
    {
        var tabView = _tabView ?? throw new InvalidOperationException(
            "标签导航尚未初始化，请先调用 Attach 传入 TabView。");

        var definition = PageCatalog.Get(pageKey);

        // 已经开着：激活它，并把参数再送一次，让页面可以按新参数刷新。
        if (_tabs.TryGetValue(definition.Key, out var existing))
        {
            tabView.SelectedItem = existing.Item;

            if (parameter is not null && GetViewModel(existing.Frame) is { } viewModel)
                await viewModel.OnNavigatedToAsync(parameter);

            return;
        }

        var frame = new Frame();

        // 页面实例由 XAML 体系创建（只认无参构造函数），ViewModel 由页面自己从容器取。
        if (!frame.Navigate(definition.PageType, parameter))
        {
            _logger.LogError("导航到 {Page} 失败，页面类型 {Type}", definition.Key, definition.PageType.FullName);
            return;
        }

        var item = new TabViewItem
        {
            Header = BuildHeader(definition),
            Content = frame,
            IsClosable = definition.CanClose,
            Tag = definition.Key
        };

        ToolTipService.SetToolTip(item, definition.Title);

        _tabs[definition.Key] = new TabEntry(definition, item, frame);
        tabView.TabItems.Add(item);
        tabView.SelectedItem = item;

        _logger.LogDebug("已打开标签 {Page}", definition.Key);
        PageOpened?.Invoke(this, definition.Key);
    }

    public bool Close(string pageKey)
    {
        if (_tabView is null) return false;
        if (!_tabs.TryGetValue(pageKey, out var entry)) return false;

        if (!entry.Definition.CanClose)
        {
            _logger.LogDebug("{Page} 是常驻页面，不允许关闭", pageKey);
            return false;
        }

        // 先通知 ViewModel 释放资源，再拆掉页面，顺序反了就拿不到它了。
        GetViewModel(entry.Frame)?.OnTabClosed();

        _tabView.TabItems.Remove(entry.Item);
        _tabs.Remove(pageKey);

        // 断开 Frame 与页面实例的引用，让它们能被 GC 回收。
        // 不清理的话每开一个标签就永久多留一份页面状态，开得越多内存越高。
        entry.Frame.Content = null;
        entry.Frame.BackStack.Clear();
        entry.Frame.ForwardStack.Clear();

        _logger.LogDebug("已关闭标签 {Page}", pageKey);
        return true;
    }

    public void CloseOthers(string pageKey)
    {
        foreach (var key in _tabs.Keys.Where(k => !string.Equals(k, pageKey, StringComparison.OrdinalIgnoreCase)).ToList())
            Close(key);
    }

    public void CloseAll()
    {
        foreach (var key in _tabs.Keys.ToList())
            Close(key);
    }

    public bool IsOpen(string pageKey) => _tabs.ContainsKey(pageKey);

    // ---------------------------------------------------------------

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab?.Tag is string key)
            Close(key);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var key = (_tabView?.SelectedItem as TabViewItem)?.Tag as string;
        if (key == ActivePageKey) return;

        ActivePageKey = key;
        ActivePageChanged?.Invoke(this, key);
    }

    /// <summary>
    /// 标签栏的「+」按钮：弹一个菜单列出所有页面，选中就打开。
    /// 比让用户回左侧菜单找更顺手。
    /// </summary>
    private void OnAddTabButtonClick(TabView sender, object args)
    {
        var flyout = new MenuFlyout();

        foreach (var group in PageCatalog.Groups)
        {
            var subMenu = new MenuFlyoutSubItem { Text = group.Title };

            foreach (var page in group.Children)
            {
                var item = new MenuFlyoutItem
                {
                    Text = page.Title,
                    Icon = new FontIcon { Glyph = page.Glyph }
                };

                // 已经开着的页面在菜单里显示为禁用，避免重复点击。
                item.IsEnabled = !IsOpen(page.Key);
                item.Click += async (_, _) => await OpenAsync(page.Key);
                subMenu.Items.Add(item);
            }

            flyout.Items.Add(subMenu);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        var closeAll = new MenuFlyoutItem
        {
            Text = "关闭全部标签",
            Icon = new FontIcon { Glyph = "" },
            IsEnabled = _tabs.Values.Any(t => t.Definition.CanClose)
        };
        closeAll.Click += (_, _) => CloseAll();
        flyout.Items.Add(closeAll);

        // 弹出的位置要对准「+」按钮，所以锚点得是按钮本身，不能是整个 TabView。
        // 这个事件给的 sender 是整个 TabView，ShowAt(TabView) 会贴着标签栏这条长条弹，
        // 菜单位置跟按钮差着十万八千里。
        //
        // 而 TabView 只公开了 AddTabButtonCommand / AddTabButtonClick / IsAddTabButtonVisible，
        // 没有把按钮元素放出来，所以只能按模板部件名「AddButton」从可视树里找。
        // 找不到就退回 TabView —— 位置难看，但功能还在，不会因为 SDK 改了模板名就点不出菜单。
        var anchor = TreeHelper.FindDescendant<FrameworkElement>(sender, "AddButton") ?? (FrameworkElement)sender;

        flyout.ShowAt(anchor, new FlyoutShowOptions
        {
            // 默认的 Auto 会按可用空间自己挑位置，在按钮这种小目标上方向不固定。
            // 固定成「下边缘左对齐」= 菜单从按钮左下角往下展开，这是标签栏加号该有的样子。
            // 按钮贴着右边时不会被裁掉：FlyoutBase 默认会把自己约束在窗口范围内。
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
        });
    }

    /// <summary>标签头：图标 + 文字。</summary>
    private static StackPanel BuildHeader(PageDefinition definition)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (definition.ShowIconInTab && !string.IsNullOrEmpty(definition.Glyph))
        {
            panel.Children.Add(new FontIcon
            {
                Glyph = definition.Glyph,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = definition.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        return panel;
    }

    private static INavigationAware? GetViewModel(Frame frame)
        => (frame.Content as IViewHost)?.ViewModel;

    private sealed record TabEntry(PageDefinition Definition, TabViewItem Item, Frame Frame);
}
