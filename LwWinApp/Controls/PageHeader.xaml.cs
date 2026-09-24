using Microsoft.UI.Xaml.Markup;

namespace LwWinApp.Controls;

/// <summary>
/// 页头。一个页面顶部那块「标题 + 说明 + 右侧操作」的固定版式。
///
/// 用法（子内容自动成为右侧操作区）：
/// <code>
/// &lt;controls:PageHeader Title="用户管理" Description="维护系统账号、部门与角色">
///     &lt;StackPanel Orientation="Horizontal" Spacing="8"&gt;
///         &lt;Button Content="新增" /&gt;
///     &lt;/StackPanel&gt;
/// &lt;/controls:PageHeader&gt;
/// </code>
/// </summary>
[ContentProperty(Name = nameof(Actions))]
public sealed partial class PageHeader : UserControl
{
    public PageHeader()
    {
        InitializeComponent();
        ApplyTitle();
        ApplyDescription();
        ApplyActions();
    }

    /// <summary>页面标题。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader),
        new PropertyMetadata(string.Empty, (d, _) => ((PageHeader)d).ApplyTitle()));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>标题下的一行说明。留空则整行不占位置。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(PageHeader),
        new PropertyMetadata(null, (d, _) => ((PageHeader)d).ApplyDescription()));

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>右侧操作区的内容。</summary>
    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions), typeof(object), typeof(PageHeader),
        new PropertyMetadata(null, (d, _) => ((PageHeader)d).ApplyActions()));

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    private void ApplyTitle() => TitleText.Text = Title ?? string.Empty;

    private void ApplyDescription()
    {
        var description = Description;

        DescriptionText.Text = description ?? string.Empty;
        DescriptionText.Visibility = string.IsNullOrWhiteSpace(description)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ApplyActions() => ActionsPresenter.Content = Actions;
}
