using LwWinApp.Core;
using LwWinApp.Utils;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace LwWinApp.Services;

/// <summary>消息提示服务：页面内的 InfoBar 条 + 右下角浮出的轻提示。</summary>
public interface INotificationService
{
    /// <summary>指定 InfoBar 的宿主容器（通常是页面顶部的一个 StackPanel）。</summary>
    void AttachInfoBarHost(Panel host);

    /// <summary>指定轻提示的宿主容器（通常是 Shell 里盖在最上层的那个 StackPanel）。</summary>
    void AttachToastHost(Panel host);

    void Show(InfoBarSeverity severity, string message, string? title = null, TimeSpan? duration = null);

    void Info(string message, string? title = null);

    void Success(string message, string? title = null);

    void Warning(string message, string? title = null);

    void Error(string message, string? title = null);

    /// <summary>右下角浮出的轻提示，几秒后自动消失。</summary>
    void Toast(string message, ToastKind kind = ToastKind.Info, TimeSpan? duration = null);

    /// <summary>清掉当前所有 InfoBar。</summary>
    void Clear();
}

/// <inheritdoc />
public sealed class NotificationService : INotificationService
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan DefaultToastDuration = TimeSpan.FromSeconds(3);

    private readonly ILogger<NotificationService> _logger;

    private Panel? _infoBarHost;
    private Panel? _toastHost;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public void AttachInfoBarHost(Panel host) => _infoBarHost = host;

    public void AttachToastHost(Panel host) => _toastHost = host;

    public void Show(InfoBarSeverity severity, string message, string? title = null, TimeSpan? duration = null)
    {
        if (_infoBarHost is null)
        {
            // 宿主还没挂上（比如页面尚未加载完），退到日志，避免消息被静默吞掉。
            _logger.LogInformation("[通知/{Severity}] {Message}", severity, message);
            return;
        }

        var bar = new InfoBar
        {
            Severity = severity,
            Message = message,
            IsOpen = true,
            IsClosable = true,
            Margin = new Thickness(0, 0, 0, 8),

            // 宿主是浮层，底下压着的是页面内容。加一点投影，
            // 让「浮在上面」这件事看得出来，不然像是一块贴歪了的色块。
            Shadow = new ThemeShadow()
        };

        // 阴影要生效必须显式指定投射距离，和轻提示那边同一个道理。
        bar.Translation = new System.Numerics.Vector3(0, 0, 16);

        if (!string.IsNullOrWhiteSpace(title))
            bar.Title = title;

        _infoBarHost.Children.Add(bar);

        // 用户手动关掉时也要从容器里摘掉，否则容器里会越积越多。
        bar.Closed += (_, _) => _infoBarHost?.Children.Remove(bar);

        // 关闭按钮那个叉不吃 InfoBar.Foreground：模板给这个 Button 单独挂了一套资源
        // （ButtonForeground → AppBarButtonForeground，深色），压在实色底上就是看不见的叉；
        // hover / pressed 也归那套资源管（视觉状态直接动画 ContentPresenter.Foreground）。
        // InfoBar 没把按钮元素放出来，只能等模板套上之后自己去可视树里换掉它那层资源。
        bar.Loaded += (_, _) => PaintCloseButton(bar);

        var timeout = duration ?? DefaultDuration;
        if (timeout > TimeSpan.Zero)
            _ = AutoDismissAsync(bar, timeout);
    }

    public void Info(string message, string? title = null)
        => Show(InfoBarSeverity.Informational, message, title);

    public void Success(string message, string? title = null)
        => Show(InfoBarSeverity.Success, message, title);

    public void Warning(string message, string? title = null)
        => Show(InfoBarSeverity.Warning, message, title);

    public void Error(string message, string? title = null)
        // 错误不自动消失，用户需要时间读完再自己关。
        => Show(InfoBarSeverity.Error, message, title, duration: Timeout.InfiniteTimeSpan);

    public void Toast(string message, ToastKind kind = ToastKind.Info, TimeSpan? duration = null)
    {
        if (_toastHost is null)
        {
            _logger.LogInformation("[轻提示/{Kind}] {Message}", kind, message);
            return;
        }

        var (glyph, brushKey) = kind switch
        {
            ToastKind.Success => ("", "SystemFillColorSuccessBrush"),
            ToastKind.Warning => ("", "SystemFillColorCautionBrush"),
            ToastKind.Error => ("", "SystemFillColorCriticalBrush"),
            _ => ("", "SystemFillColorAttentionBrush")
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };

        content.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = 16,
            Foreground = GetBrush(brushKey),
            VerticalAlignment = VerticalAlignment.Center
        });

        content.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320,
            VerticalAlignment = VerticalAlignment.Center
        });

        var card = new Border
        {
            Child = content,
            Padding = new Thickness(14, 10, 14, 10),
            CornerRadius = new CornerRadius(8),
            Background = GetBrush("ToastCardBackgroundBrush") ?? GetBrush("SystemControlBackgroundChromeMediumLowBrush"),
            BorderBrush = GetBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 8, 0, 0),
            Shadow = new ThemeShadow(),
            Opacity = 0,
            RenderTransform = new TranslateTransform { Y = 16 }
        };

        // 阴影要生效必须显式指定投射距离。
        card.Translation = new System.Numerics.Vector3(0, 0, 16);

        _toastHost.Children.Add(card);
        PlayEntrance(card);

        var timeout = duration ?? DefaultToastDuration;
        _ = AutoDismissToastAsync(card, timeout);
    }

    public void Clear()
    {
        if (_infoBarHost is null) return;

        foreach (var bar in _infoBarHost.Children.OfType<InfoBar>().ToList())
            bar.IsOpen = false;

        _infoBarHost.Children.Clear();
    }

    // ---------------------------------------------------------------

    /// <summary>
    /// 关闭按钮上的三个键。前两个是按钮模板里 hover / pressed 视觉状态直接消费的
    /// （generic.xaml 里那两个 ObjectAnimationUsingKeyFrames 指向 ContentPresenter.Foreground），
    /// 光给 Button.Foreground 赋值管不到它们。
    /// </summary>
    private static readonly string[] CloseButtonForegroundKeys =
    {
        "ButtonForeground",
        "ButtonForegroundPointerOver",
        "ButtonForegroundPressed"
    };

    /// <summary>把 InfoBar 关闭按钮的前景改成白色 —— 实色底上默认那个深色的叉看不见。</summary>
    private static void PaintCloseButton(InfoBar bar)
    {
        if (TreeHelper.FindDescendant<Button>(bar, "CloseButton") is not { } close)
            return;

        var white = new SolidColorBrush(Microsoft.UI.Colors.White);

        // 属性赋值管「静止」态，资源键管 hover / pressed（见字段上的说明）。
        close.Foreground = white;

        foreach (var key in CloseButtonForegroundKeys)
            close.Resources[key] = white;
    }

    private async Task AutoDismissAsync(InfoBar bar, TimeSpan timeout)
    {
        try
        {
            await Task.Delay(timeout);

            // 延时期间用户可能已经关掉它了，重复关闭会抛异常。
            await DispatcherHelper.RunAsync(() =>
            {
                if (_infoBarHost?.Children.Contains(bar) == true)
                    bar.IsOpen = false;
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "自动关闭 InfoBar 时出错");
        }
    }

    private async Task AutoDismissToastAsync(Border card, TimeSpan timeout)
    {
        try
        {
            await Task.Delay(timeout);

            await DispatcherHelper.RunAsync(() =>
            {
                if (_toastHost?.Children.Contains(card) != true) return;

                var storyboard = new Storyboard();

                var fade = new DoubleAnimation
                {
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                    EnableDependentAnimation = true
                };
                Storyboard.SetTarget(fade, card);
                Storyboard.SetTargetProperty(fade, "Opacity");
                storyboard.Children.Add(fade);

                storyboard.Completed += (_, _) => _toastHost?.Children.Remove(card);
                storyboard.Begin();
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "自动关闭轻提示时出错");
        }
    }

    /// <summary>淡入 + 从下方轻微上移，让提示的出现不那么突兀。</summary>
    private static void PlayEntrance(Border card)
    {
        var storyboard = new Storyboard();

        var fade = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(220)),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(fade, card);
        Storyboard.SetTargetProperty(fade, "Opacity");
        storyboard.Children.Add(fade);

        var slide = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(220)),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(slide, card);
        Storyboard.SetTargetProperty(slide, "(UIElement.RenderTransform).(TranslateTransform.Y)");
        storyboard.Children.Add(slide);

        storyboard.Begin();
    }

    private static Brush? GetBrush(string resourceKey)
        => Application.Current.Resources.TryGetValue(resourceKey, out var value)
            ? value as Brush
            : null;
}
