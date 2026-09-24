using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace LwWinApp.Controls;

/// <summary>把手上报的位移。</summary>
public sealed class ResizeGripEventArgs : EventArgs
{
    public ResizeGripEventArgs(double delta) => Delta = delta;

    /// <summary>相对上一次事件移动了多少（DIP）。向右、向下为正。</summary>
    public double Delta { get; }
}

/// <summary>
/// 拖拽把手：贴在列边界或数据行的底边上，按住拖动就能改列宽 / 行高。
///
/// 为什么做成控件而不是在页面里挂三个指针事件：
/// 光标形状只能从 UIElement 的派生类里设（ProtectedCursor 是 protected），
/// 那就顺手把指针捕获、位移换算、光标切换都收在这里，页面那边只处理 Delta。
///
/// 把手里的子元素在鼠标移上来或拖动时点亮（改 Opacity），光标和这束亮线
/// 就是这个把手「可以拖」的全部提示 —— 所以子元素的颜色交给 XAML 用主题画刷给。
/// </summary>
public sealed class ResizeGrip : Grid
{
    private const double IdleOpacity = 0;
    private const double ActiveOpacity = 1;

    private bool _dragging;

    /// <summary>
    /// 上一次的指针位置。用窗口坐标而不是自身坐标：
    /// 拖列边界时把手会跟着边界一起往右走，用自身坐标算出来的位移永远是 0。
    /// </summary>
    private Point _last;

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation),
        typeof(Orientation),
        typeof(ResizeGrip),
        new PropertyMetadata(Orientation.Vertical, (d, _) => ((ResizeGrip)d).ApplyCursor()));

    /// <summary>把手是竖的（拖列宽）还是横的（拖行高）。决定光标和读数方向。</summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>拖动中，每移动一次报一次位移。起点不在事件里，调用方只管累加。</summary>
    public event EventHandler<ResizeGripEventArgs>? Dragged;

    /// <summary>松手（或指针被抢走）。要收尾的话挂这个。</summary>
    public event EventHandler? DragCompleted;

    public ResizeGrip()
    {
        // 没有背景的 Panel 收不到指针事件，透明画刷也得给一个。
        Background = new SolidColorBrush(Colors.Transparent);
        ApplyCursor();

        Loaded += (_, _) => SetChildOpacity(IdleOpacity);
        PointerEntered += (_, _) => SetChildOpacity(ActiveOpacity);
        PointerExited += (_, _) =>
        {
            if (!_dragging) SetChildOpacity(IdleOpacity);
        };

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => EndDrag();
    }

    /// <summary>
    /// Panel 默认不建 AutomationPeer，也就是在 UI Automation 树里根本不存在 ——
    /// 打开 UI 自动化工具、读屏、或者 tools/uia.ps1 都找不到它。
    /// 这是个可交互的把手，补给它一个，顺带给个名字（不然报出来是个没有名字的空白块）。
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer()
        => new FrameworkElementAutomationPeer(this);

    private void ApplyCursor()
    {
        ProtectedCursor = InputSystemCursor.Create(Orientation == Orientation.Vertical
            ? InputSystemCursorShape.SizeWestEast
            : InputSystemCursorShape.SizeNorthSouth);

        AutomationProperties.SetName(this, Orientation == Orientation.Vertical ? "调整列宽" : "调整行高");
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging) return;

        _dragging = true;
        _last = e.GetCurrentPoint(null!).Position;
        CapturePointer(e.Pointer);
        SetChildOpacity(ActiveOpacity);

        // 标掉，免得 ListViewItem 再按一次下做它自己的按下反馈。
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;

        var position = e.GetCurrentPoint(null!).Position;
        var delta = Orientation == Orientation.Vertical
            ? position.X - _last.X
            : position.Y - _last.Y;

        if (delta == 0) return;

        _last = position;
        Dragged?.Invoke(this, new ResizeGripEventArgs(delta));
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;

        EndDrag();
        e.Handled = true;
    }

    private void EndDrag()
    {
        if (!_dragging) return;

        _dragging = false;
        ReleasePointerCaptures();
        SetChildOpacity(IdleOpacity);
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void SetChildOpacity(double value)
    {
        foreach (var child in Children)
            child.Opacity = value;
    }
}
