using LwWinApp.Controls;
using LwWinApp.ViewModels;
using LwWinApp.ViewModels.Demo;

namespace LwWinApp.Views.Demo;

/// <summary>多级分组编辑表格示例页。</summary>
public sealed partial class GroupedTablePage : PageBase
{
    public GroupedTablePage()
    {
        InitializeComponent();
        Vm = Require<GroupedTableViewModel>();
    }

    public GroupedTableViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;

    /// <summary>
    /// 点分组行上的箭头。两条分组行用的是同一个处理器，靠 DataContext 分辨点的是哪一种 ——
    /// 行的类型只有三种，没必要为折叠各写一个命令。
    /// </summary>
    private void OnToggleGroupClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is IGroupedRow row)
            Vm.Toggle(row);
    }

    /// <summary>
    /// 拖表头上的列边界。Tag 里是「边界左边那一列的序号」（XAML 里写死的四位）。
    /// 位移只管往布局上累加，宽度下限、哪一列是活动列都由 TableLayout 自己判断。
    /// </summary>
    private void OnColumnGripDragged(object sender, ResizeGripEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string text && int.TryParse(text, out var boundary))
            Vm.Layout.DragBoundary(boundary, e.Delta);
    }

    /// <summary>
    /// 拖数据行的底边。改的是这一行自己的高度，别的行不动 ——
    /// 行高是行对象的属性，模板里 Height 绑着它，改完界面自己跟。
    /// </summary>
    private void OnRowGripDragged(object sender, ResizeGripEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GroupedItemRow row)
            row.AdjustRowHeight(e.Delta);
    }
}
