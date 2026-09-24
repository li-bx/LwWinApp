namespace LwWinApp.Controls;

/// <summary>
/// 表格的列宽状态。
///
/// 表头是一次 Grid 布局，每个数据行又是一次 —— 两边要严丝合缝地对齐，宽度就只能有一份。
/// 所以宽度放在这里，表头和数据行的 <see cref="TableGrid"/> 都套用同一份：
/// 拖一次列边界，所有行跟着动，不存在「表头和数据行各写一遍宽度」的同步问题。
///
/// 这个类里没有任何 UI 类型（GridLength、光标都留在 TableGrid / ResizeGrip 里），
/// 所以 ViewModel 可以放心持有它。
/// </summary>
public sealed class TableLayout
{
    /// <summary>列数。</summary>
    public const int ColumnCount = 5;

    /// <summary>
    /// 活动列的下标（备注那列）。它不存固定宽度，由 <see cref="TableGrid"/> 建成 * ，
    /// 吃掉其余列占完之后剩下的宽度 —— 窗口拉宽拉窄，表格始终铺满，右边界也永远齐。
    /// </summary>
    public const int ElasticColumn = 3;

    /// <summary>列宽下限。再窄表头文字就完全看不出来了。</summary>
    public const double MinColumnWidth = 60;

    /// <summary>
    /// 固定宽度的列（活动列之外的四列），按列序存放。
    /// 下标换算见 <see cref="IndexOf"/>。
    /// </summary>
    private readonly double[] _fixedWidths;

    /// <param name="w0">第 1 列 物料编码。</param>
    /// <param name="w1">第 2 列 物料名称。</param>
    /// <param name="w2">第 3 列 规格型号。</param>
    /// <param name="w4">第 5 列 状态。</param>
    public TableLayout(double w0, double w1, double w2, double w4)
    {
        _fixedWidths = [w0, w1, w2, w4];
    }

    /// <summary>列宽变了。套用这份宽度的每个 TableGrid 都会收到。</summary>
    public event EventHandler? Changed;

    /// <summary>取某一列的固定宽度。活动列没有固定宽度，返回 0（它的宽度是算出来的）。</summary>
    public double GetFixedWidth(int column)
        => column == ElasticColumn ? 0 : _fixedWidths[IndexOf(column)];

    /// <summary>
    /// 拖第 <paramref name="boundary"/> 条列边界，<paramref name="boundary"/> 是边界<b>左边</b>那一列的序号。
    ///
    /// 一般就是「把左边那列拖宽」。只有紧挨活动列右边的那条边界是反的：
    /// 活动列自己吃剩余宽度，所以那里调的是右边那列，位移也要取反 ——
    /// 这样不管拖哪条边界，看上去都是边界跟着鼠标走。
    /// </summary>
    public void DragBoundary(int boundary, double delta)
    {
        var column = boundary;

        if (boundary == ElasticColumn)
        {
            column = ElasticColumn + 1;
            delta = -delta;
        }

        // 最后一列的右边没有边界可拖。
        if (column < 0 || column >= ColumnCount) return;

        var index = IndexOf(column);
        var width = Math.Clamp(_fixedWidths[index] + delta, MinColumnWidth, double.MaxValue);

        // 已经顶到下限了就别再通知一遍：拖动时每个像素都会走到这里，
        // 白刷几十次界面没有意义。
        if (Math.Abs(width - _fixedWidths[index]) < 0.01) return;

        _fixedWidths[index] = width;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>把列序号换算成固定宽度数组里的下标（活动列不占位置）。</summary>
    private static int IndexOf(int column)
        => column < ElasticColumn ? column : column - 1;
}
