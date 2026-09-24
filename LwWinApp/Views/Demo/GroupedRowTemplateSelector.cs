using LwWinApp.ViewModels.Demo;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LwWinApp.Views.Demo;

/// <summary>
/// 分组表格的行模板选择器：大类、小类、数据行三种行塞在同一个 ListView 里，
/// 由它按行对象的类型发模板。
///
/// 为什么不用 ListView 自带的分组：<c>CollectionViewSource.IsSourceGrouped</c> 只支持**一级**
/// 分组，两级做不出来；而且它生成的分组头是模板固定的，折叠不了。
/// 自己拍平 + 模板选择器，层级和折叠都完全可控。
/// </summary>
public sealed class GroupedRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? MajorTemplate { get; set; }

    public DataTemplate? SubTemplate { get; set; }

    public DataTemplate? ItemTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
        => item switch
        {
            MajorGroupRow => MajorTemplate,
            SubGroupRow => SubTemplate,
            _ => ItemTemplate
        };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
