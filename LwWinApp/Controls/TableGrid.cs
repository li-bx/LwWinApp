using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LwWinApp.Controls;

/// <summary>
/// 按 <see cref="TableLayout"/> 排列列宽的 Grid：表头用一次，每个数据行再用一次。
///
/// 列定义由控件自己建，XAML 里只写 <c>Grid.Column</c>，宽度不用写 ——
/// 写两遍宽度是这类表格最容易出错的地方（改一处忘另一处，表头和数据就错位了）。
/// 活动列建成 * ，其余列用布局里的像素宽度。
/// </summary>
public sealed class TableGrid : Grid
{
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout),
        typeof(TableLayout),
        typeof(TableGrid),
        new PropertyMetadata(null, (d, _) => ((TableGrid)d).OnLayoutChanged()));

    /// <summary>共享的列宽。同一个表的所有 TableGrid 传同一个实例。</summary>
    public TableLayout? Layout
    {
        get => (TableLayout?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public TableGrid()
    {
        // 列定义建一次就够：列数不会变，只有宽度会变。
        for (var i = 0; i < TableLayout.ColumnCount; i++)
            ColumnDefinitions.Add(new ColumnDefinition());

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private TableLayout? _subscribed;

    private void OnLayoutChanged()
    {
        Subscribe();
        ApplyWidths();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subscribe();
        ApplyWidths();
    }

    /// <summary>
    /// 卸载就退订。ListView 会回收行，留着订阅等于把已经扔掉的行容器挂在布局上不放。
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnLayoutWidthsChanged;
            _subscribed = null;
        }
    }

    private void Subscribe()
    {
        if (ReferenceEquals(_subscribed, Layout)) return;

        if (_subscribed is not null)
            _subscribed.Changed -= OnLayoutWidthsChanged;

        _subscribed = Layout;

        if (_subscribed is not null)
            _subscribed.Changed += OnLayoutWidthsChanged;
    }

    private void OnLayoutWidthsChanged(object? sender, EventArgs e) => ApplyWidths();

    private void ApplyWidths()
    {
        if (Layout is null || ColumnDefinitions.Count < TableLayout.ColumnCount) return;

        for (var i = 0; i < TableLayout.ColumnCount; i++)
        {
            ColumnDefinitions[i].Width = i == TableLayout.ElasticColumn
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(Layout.GetFixedWidth(i));
        }
    }
}
