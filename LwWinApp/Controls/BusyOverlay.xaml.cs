namespace LwWinApp.Controls;

/// <summary>
/// 页面级忙碌遮罩。
///
/// 用法：放在页面根 <c>Grid</c> 的最后一个孩子上，绑定 ViewModel 的 IsBusy：
/// <code>
/// &lt;controls:BusyOverlay Grid.RowSpan="3" IsActive="{x:Bind ViewModel.IsBusy, Mode=OneWay}" /&gt;
/// </code>
/// </summary>
public sealed partial class BusyOverlay : UserControl
{
    public BusyOverlay()
    {
        InitializeComponent();
        ApplyState();
    }

    /// <summary>是否显示遮罩。</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(BusyOverlay), new PropertyMetadata(false, OnIsActiveChanged));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>遮罩上显示的文字。</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(BusyOverlay),
        new PropertyMetadata("正在加载…", OnTextChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((BusyOverlay)d).ApplyState();

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((BusyOverlay)d).MessageText.Text = e.NewValue as string ?? string.Empty;

    private void ApplyState()
    {
        var active = IsActive;

        Root.Visibility = active ? Visibility.Visible : Visibility.Collapsed;

        // 隐藏时必须停掉动画：不显式停，ProgressRing 会在后台一直转，
        // 页面越多 CPU 占用越明显。
        Ring.IsActive = active;

        MessageText.Text = Text;
    }
}
