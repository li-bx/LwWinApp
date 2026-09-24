using LwWinApp.Views;
using LwWinApp.Views.Demo;
using LwWinApp.Views.Settings;
using LwWinApp.Views.System;

namespace LwWinApp.Navigation;

/// <summary>一个可打开的页面（二级导航的叶子节点）。</summary>
public sealed record PageDefinition(string Key, string Title, string Glyph, Type PageType)
{
    /// <summary>是否允许关闭标签。仪表盘这类常驻页设为 false，标签上不出现关闭按钮。</summary>
    public bool CanClose { get; init; } = true;

    /// <summary>标签页上是否显示图标。</summary>
    public bool ShowIconInTab { get; init; } = true;
}

/// <summary>一级导航分组，对应左侧菜单里可展开的一项。</summary>
public sealed record NavigationGroup(string Key, string Title, string Glyph, IReadOnlyList<PageDefinition> Children);

/// <summary>
/// 页面登记表 —— 二级导航的唯一事实来源。
///
/// 左侧菜单结构（XAML）和标签页要打开的页面类型（这里）靠 Key 关联：
/// 菜单项把 Key 放在 Tag 上，点中后交给 TabNavigationService 到这里查页面类型。
///
/// 加新页面只要两步：在 Views 下建页面，然后在下面 Groups 里加一行。
/// </summary>
public static class PageCatalog
{
    // ---- 页面 Key 常量，避免各处写裸字符串拼错 ----
    public const string Dashboard = "dashboard";

    public const string UserManage = "system.user";
    public const string RoleManage = "system.role";
    public const string DeptTree = "system.dept";
    public const string OperationLog = "system.log";

    public const string ControlsDemo = "demo.controls";
    public const string DialogDemo = "demo.dialog";
    public const string WindowDemo = "demo.window";
    public const string ProgressDemo = "demo.progress";
    public const string NotificationDemo = "demo.notify";
    public const string FormValidation = "demo.form";
    public const string GroupedTable = "demo.grouped";

    public const string Settings = "settings.general";
    public const string About = "settings.about";

    /// <summary>左侧一级导航的完整结构。</summary>
    public static IReadOnlyList<NavigationGroup> Groups { get; } =
    [
        new NavigationGroup("system", "系统管理", "",
        [
            new PageDefinition(UserManage, "用户管理", "", typeof(UserManagePage)),
            new PageDefinition(RoleManage, "角色管理", "", typeof(RoleManagePage)),
            new PageDefinition(DeptTree, "部门架构", "", typeof(DeptTreePage)),
            new PageDefinition(OperationLog, "操作日志", "", typeof(OperationLogPage))
        ]),

        new NavigationGroup("demo", "框架示例", "",
        [
            new PageDefinition(ControlsDemo, "常用控件", "", typeof(ControlsDemoPage)),
            new PageDefinition(DialogDemo, "弹出对话框", "", typeof(DialogDemoPage)),
            new PageDefinition(WindowDemo, "弹窗窗口", "", typeof(WindowDemoPage)),
            new PageDefinition(ProgressDemo, "进度与加载", "", typeof(ProgressDemoPage)),
            new PageDefinition(NotificationDemo, "消息通知", "", typeof(NotificationDemoPage)),
            new PageDefinition(GroupedTable, "分组编辑表格", "", typeof(GroupedTablePage)),
            new PageDefinition(FormValidation, "表单校验", "", typeof(FormValidationPage))
        ]),

        new NavigationGroup("settings", "设置", "",
        [
            new PageDefinition(Settings, "系统设置", "", typeof(SettingsPage)),
            new PageDefinition(About, "关于", "", typeof(AboutPage))
        ])
    ];

    /// <summary>常驻首页。启动时打开，且不允许关闭。</summary>
    public static PageDefinition Home { get; } = new(
        Dashboard, "仪表盘", "", typeof(DashboardPage))
    {
        CanClose = false,
        ShowIconInTab = false
    };

    private static readonly Dictionary<string, PageDefinition> Lookup = BuildLookup();

    /// <summary>所有页面（含首页），按导航顺序。（XAML 里没法直接遍历，供代码场景用。）</summary>
    public static IReadOnlyList<PageDefinition> AllPages { get; } =
        [Home, .. Groups.SelectMany(g => g.Children)];

    public static bool TryGet(string? key, out PageDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            definition = Home;
            return false;
        }

        return Lookup.TryGetValue(key, out definition!);
    }

    /// <summary>按 Key 取页面定义，找不到返回首页而不是抛异常。</summary>
    public static PageDefinition Get(string? key)
        => TryGet(key, out var definition) ? definition : Home;

    /// <summary>取某页面所属的一级分组，用于左侧菜单同步高亮。</summary>
    public static NavigationGroup? FindGroupOf(string pageKey)
        => Groups.FirstOrDefault(g => g.Children.Any(c => c.Key == pageKey));

    private static Dictionary<string, PageDefinition> BuildLookup()
    {
        var map = new Dictionary<string, PageDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [Home.Key] = Home
        };

        foreach (var page in Groups.SelectMany(g => g.Children))
        {
            // Key 写重复会让导航行为变得难以预料，启动时就直接暴露出来。
            if (!map.TryAdd(page.Key, page))
                throw new InvalidOperationException($"页面 Key 重复：{page.Key}");
        }

        return map;
    }
}
