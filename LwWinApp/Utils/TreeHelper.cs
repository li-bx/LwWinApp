using Microsoft.UI.Xaml.Media;

namespace LwWinApp.Utils;

/// <summary>
/// 可视树遍历。
///
/// 什么时候需要它：控件的模板内部有你要的东西，但控件本身没把它公开出来。
/// 典型例子是 <c>TabView</c> 的「+」按钮 —— 它只公开了命令、事件、可见性三样，
/// 拿不到按钮元素本身，而弹出菜单需要那个元素当锚点。
/// </summary>
public static class TreeHelper
{
    /// <summary>
    /// 在 <paramref name="root"/> 的子树里找第一个 <typeparamref name="T"/>。
    ///
    /// <paramref name="name"/> 给定时只认名字相符的那个。
    /// 找不到返回 null —— 模板换了不该让调用方崩，由调用方决定怎么降级。
    /// </summary>
    /// <remarks>
    /// 深度优先，取先遇到的。模板部件的名字来自 SDK 的 <c>generic.xaml</c>，
    /// 升级 Windows App SDK 后建议复查一次名字有没有变。
    /// </remarks>
    public static T? FindDescendant<T>(DependencyObject? root, string? name = null)
        where T : FrameworkElement
    {
        if (root is null) return null;

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T typed && (name is null || typed.Name == name))
                return typed;

            if (FindDescendant<T>(child, name) is { } found)
                return found;
        }

        return null;
    }
}
