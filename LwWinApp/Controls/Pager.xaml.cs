using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LwWinApp.Controls;

/// <summary>
/// 分页器。
///
/// 语义约定：
/// <list type="bullet">
/// <item><see cref="PageIndex"/> 从 1 开始，和 <c>QueryFilter.PageIndex</c> 一致。</item>
/// <item>本控件不查数据、不做越界纠正 —— 它只负责改页码并把新值通过双向绑定推给 ViewModel。
/// 纠正越界是数据层的责任（列表被删空时才轮得到），放在这里会变成两个地方同时改数据。</item>
/// </list>
/// </summary>
public sealed partial class Pager : UserControl
{
    /// <summary>重新设置 ItemsSource/选中项时用它挡住 SelectionChanged，避免自己触发自己。</summary>
    private bool _syncing;

    /// <summary>
    /// 每页条数选项的本地副本。
    /// 不直接用外面传进来的那个列表，是因为缺项时要往里补当前值——
    /// 改别人的集合会埋下很难查的副作用。
    /// </summary>
    private readonly List<int> _pageSizes = [];

    public Pager()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    // ---------------------------------------------------------------
    // 依赖属性
    // ---------------------------------------------------------------

    /// <summary>符合条件的总记录数。</summary>
    public static readonly DependencyProperty TotalProperty = DependencyProperty.Register(
        nameof(Total), typeof(long), typeof(Pager), new PropertyMetadata(0L, OnDisplayPropertyChanged));

    public long Total
    {
        get => (long)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

    /// <summary>当前页码，从 1 开始。双向绑定到 ViewModel。</summary>
    public static readonly DependencyProperty PageIndexProperty = DependencyProperty.Register(
        nameof(PageIndex), typeof(int), typeof(Pager), new PropertyMetadata(1, OnDisplayPropertyChanged));

    public int PageIndex
    {
        get => (int)GetValue(PageIndexProperty);
        set => SetValue(PageIndexProperty, value);
    }

    /// <summary>每页条数。双向绑定到 ViewModel。</summary>
    public static readonly DependencyProperty PageSizeProperty = DependencyProperty.Register(
        nameof(PageSize), typeof(int), typeof(Pager), new PropertyMetadata(15, OnDisplayPropertyChanged));

    public int PageSize
    {
        get => (int)GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    /// <summary>
    /// 每页条数的候选项，来自配置。
    /// 声明成只读接口而不是 IList&lt;int&gt;：控件只用它来填充下拉框，不往里写。
    /// 绑定时类型必须严格一致，ViewModel 那边给的就是 IReadOnlyList。
    /// </summary>
    public static readonly DependencyProperty PageSizeOptionsProperty = DependencyProperty.Register(
        nameof(PageSizeOptions), typeof(IReadOnlyList<int>), typeof(Pager),
        new PropertyMetadata(null, OnPageSizeOptionsChanged));

    public IReadOnlyList<int>? PageSizeOptions
    {
        get => (IReadOnlyList<int>?)GetValue(PageSizeOptionsProperty);
        set => SetValue(PageSizeOptionsProperty, value);
    }

    // ---------------------------------------------------------------

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Pager)d).Refresh();

    private static void OnPageSizeOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pager = (Pager)d;

        pager._pageSizes.Clear();

        if (e.NewValue is IReadOnlyList<int> options)
            pager._pageSizes.AddRange(options.Where(size => size > 0).Distinct().OrderBy(size => size));

        pager._syncing = true;
        try
        {
            pager.PageSizeBox.ItemsSource = pager._pageSizes;

            // 当前每页条数不在候选里时补一项，否则下拉框会显示空白，看着像坏了。
            if (!pager._pageSizes.Contains(pager.PageSize))
            {
                pager._pageSizes.Add(pager.PageSize);
                pager._pageSizes.Sort();
            }
        }
        finally
        {
            pager._syncing = false;
        }

        pager.Refresh();
    }

    /// <summary>总页数。没有数据时按 1 页算，界面上不出现「0 / 0」。</summary>
    private int TotalPages => Total <= 0 || PageSize <= 0
        ? 1
        : (int)Math.Ceiling(Total / (double)PageSize);

    /// <summary>把所有可视元素同步到当前属性值。任何一处变化都走这里，避免状态分散。</summary>
    private void Refresh()
    {
        if (SummaryText is null) return;   // XAML 还没加载完时属性就可能已经被赋值

        var totalPages = TotalPages;
        var pageIndex = Math.Clamp(PageIndex < 1 ? 1 : PageIndex, 1, totalPages);

        var first = Total == 0 ? 0 : ((long)(pageIndex - 1) * PageSize) + 1;
        var last = Total == 0 ? 0 : Math.Min(Total, (long)pageIndex * PageSize);

        SummaryText.Text = Total == 0
            ? "共 0 条"
            : $"共 {Total} 条，当前显示第 {first}-{last} 条";

        PageIndicator.Text = $"{pageIndex} / {totalPages}";

        // 边界页码时禁用对应按钮，用户不会去点一个显然没用的按钮。
        FirstButton.IsEnabled = pageIndex > 1;
        PreviousButton.IsEnabled = pageIndex > 1;
        NextButton.IsEnabled = pageIndex < totalPages;
        LastButton.IsEnabled = pageIndex < totalPages;

        _syncing = true;
        try
        {
            if (!_pageSizes.Contains(PageSize))
            {
                _pageSizes.Add(PageSize);
                _pageSizes.Sort();
            }

            PageSizeBox.SelectedItem = PageSize;

            JumpBox.Maximum = totalPages;
            JumpBox.Value = pageIndex;
        }
        finally
        {
            _syncing = false;
        }
    }

    // ---------------------------------------------------------------
    // 交互
    // ---------------------------------------------------------------

    private void OnPageSizeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        if (PageSizeBox.SelectedItem is not int size || size == PageSize) return;

        // 每页条数变了，原来的页码基本没有意义，回到第一页。
        PageSize = size;
        PageIndex = 1;
    }

    private void OnJumpClick(object sender, RoutedEventArgs e) => ApplyJump();

    private void OnJumpKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        e.Handled = true;
        ApplyJump();
    }

    private void ApplyJump()
    {
        var target = (int)JumpBox.Value;

        if (target < 1 || target > TotalPages || target == PageIndex)
        {
            // 越界的输入直接还原成当前页，不要静默忽略——用户会以为点了没反应。
            Refresh();
            return;
        }

        PageIndex = target;
    }

    private void OnFirstClick(object sender, RoutedEventArgs e) => PageIndex = 1;

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        if (PageIndex > 1) PageIndex--;
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (PageIndex < TotalPages) PageIndex++;
    }

    private void OnLastClick(object sender, RoutedEventArgs e)
    {
        if (PageIndex < TotalPages) PageIndex = TotalPages;
    }
}
