using System.ComponentModel;
using LwWinApp.Core;
using Microsoft.UI.Xaml.Input;

namespace LwWinApp.Views.Windows;

/// <summary>
/// 通用「弹窗窗口」—— 一个真正的窗口，不是 ContentDialog。
///
/// <para>
/// 什么时候该用它而不是 <see cref="Services.IDialogService"/>：
/// 内容多、要能拖动或调整大小、或者打开期间用户还想去操作主界面（非模态），
/// 这三种情况 ContentDialog 都做不到 —— 它始终挂在主窗口的 XamlRoot 上，
/// 大小由内容决定，而且是模态的。
/// </para>
///
/// <para>
/// 它自己只管「壳」：标题、正文、一排按钮、结果。窗口怎么显示（谁当属主、要不要锁住属主、
/// 尺寸放哪）由 <see cref="Services.IChildWindowService"/> 负责，不要自己 Activate。
/// </para>
///
/// <para>用法：</para>
/// <code>
/// var window = new DialogWindow
/// {
///     Title = "提交工单",
///     Description = "填好后点提交。",
///     Body = new TicketFormBody(...),
///     PrimaryButtonText = "提交",
///     CloseButtonText = "取消"
/// };
///
/// var result = await _windows.ShowModalAsync(window);   // 阻塞属主窗口直到它关闭
/// </code>
/// </summary>
public sealed partial class DialogWindow : Window
{
    /// <summary>关闭时完成这个 Task，模态调用方 await 它拿结果。</summary>
    private readonly TaskCompletionSource<DialogResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _completed;

    public DialogWindow()
    {
        InitializeComponent();

        // 结果在 Closed 里兑现，而不是在每个关闭入口里各写一遍：
        // 用户点标题栏的 X、任务管理器之外的所有关闭路径都会走到这个事件，
        // 漏掉任何一条都会让 await ShowModalAsync 的调用方永远卡住。
        Closed += (_, _) => Complete(Result);
    }

    /// <summary>关闭原因。没点按钮、直接用标题栏的 X 关掉的是 <c>None</c>。</summary>
    public DialogResult Result { get; private set; } = DialogResult.None;

    /// <summary>窗口关闭时完成的 Task。非模态打开时没人 await 它，也不影响。</summary>
    public Task<DialogResult> Completion => _completion.Task;

    /// <summary>
    /// 窗口宽度（逻辑像素），显示前由 <see cref="Services.IChildWindowService"/> 读取。
    ///
    /// 不能写成 <c>init</c>：XAML 的类型信息生成器要给这个类上的每个公开属性造 setter，
    /// init-only 的它写不出来，编译会直接失败。反正这个值也是对象初始值设定项里赋的，
    /// 用普通属性没有任何损失。
    /// </summary>
    public double PreferredWidth { get; set; } = AppConstants.DefaultDialogWidth;

    /// <summary>窗口高度（逻辑像素）。</summary>
    public double PreferredHeight { get; set; } = AppConstants.DefaultDialogHeight;

    /// <summary>正文。放控件、放 UserControl、放一整个页面都可以。</summary>
    public object? Body
    {
        get => BodyHost.Content;
        set => BodyHost.Content = value;
    }

    /// <summary>标题下面那行小字。留空则整行不占位置。</summary>
    public string? Description
    {
        get => DescriptionText.Text;
        set
        {
            DescriptionText.Text = value ?? string.Empty;
            DescriptionText.Visibility = string.IsNullOrWhiteSpace(value)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    /// <summary>主按钮文字。留空则整个按钮不出现。</summary>
    public string? PrimaryButtonText
    {
        get => PrimaryButton.Content as string;
        set
        {
            PrimaryButton.Content = value ?? string.Empty;
            PrimaryButton.Visibility = ToVisibility(value);
            UpdateButtonBar();
        }
    }

    /// <summary>关闭按钮文字。留空则整个按钮不出现（只剩标题栏的 X）。</summary>
    public string? CloseButtonText
    {
        get => CloseButton.Content as string;
        set
        {
            CloseButton.Content = value ?? string.Empty;
            CloseButton.Visibility = ToVisibility(value);
            UpdateButtonBar();
        }
    }

    /// <summary>主按钮是否可用。等异步校验完成、或者有必填项没填时用它。</summary>
    public bool IsPrimaryEnabled
    {
        get => PrimaryButton.IsEnabled;
        set => PrimaryButton.IsEnabled = value;
    }

    /// <summary>
    /// 主按钮被点了。把 <c>args.Cancel</c> 设成 true 可以拦下关闭，把窗口留住 ——
    /// 校验不通过时用它，用户输的内容一个字都不会丢。
    /// </summary>
    public event EventHandler<CancelEventArgs>? PrimaryButtonClick;

    /// <summary>用代码关掉窗口，结果就是传进来的这个。</summary>
    public void CloseWith(DialogResult result)
    {
        Result = result;
        Close();
    }

    // ---------------------------------------------------------------

    /// <summary>有文字就显示、没文字就收起来。名字不能叫 Visible —— <c>Window</c> 上已经有一个同名成员。</summary>
    private static Visibility ToVisibility(string? text)
        => string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>两个按钮都不显示时，把整个按钮条一起收掉，免得底部多出一条空白。</summary>
    private void UpdateButtonBar()
        => ButtonBar.Visibility = CloseButton.Visibility == Visibility.Visible
                                  || PrimaryButton.Visibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        if (PrimaryButtonClick is { } handler)
        {
            var args = new CancelEventArgs();
            handler(this, args);

            if (args.Cancel) return;
        }

        CloseWith(DialogResult.Primary);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseWith(DialogResult.Close);

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        // 没有「取消」按钮时按 Esc 就是直接关掉，因为此时也确实没有「取消」这个结果可言。
        CloseWith(CloseButton.Visibility == Visibility.Visible
            ? DialogResult.Close
            : DialogResult.None);
    }

    private void OnEnterInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (PrimaryButton is not { Visibility: Visibility.Visible, IsEnabled: true }) return;

        args.Handled = true;
        OnPrimaryClick(PrimaryButton, new RoutedEventArgs());
    }

    private void Complete(DialogResult result)
    {
        if (_completed) return;

        _completed = true;
        _completion.TrySetResult(result);
    }
}
